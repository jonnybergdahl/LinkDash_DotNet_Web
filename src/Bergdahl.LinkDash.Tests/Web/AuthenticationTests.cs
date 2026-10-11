using System.Net;
using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Web.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bergdahl.LinkDash.Tests.Web;

public class AuthenticationTests
{
    private LinkDashWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new LinkDashWebApplicationFactory();
        _client = CreateClient(_factory);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private ILinkService Service => _factory.Services.GetRequiredService<ILinkService>();

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Test]
    public async Task Dashboard_IsPublic()
    {
        var response = await _client.GetAsync("/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("Sign out"));
    }

    [TestCase("/Settings")]
    [TestCase("/Settings/Links")]
    [TestCase("/Settings/AddGroup")]
    public async Task Settings_RedirectsAnonymousUsersToLogin(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString,
            Does.EndWith($"/Account/Login?ReturnUrl={Uri.EscapeDataString(url)}"));
    }

    [Test]
    public async Task Settings_RejectsAnonymousPosts()
    {
        var group = await Service.AddGroupAsync("Work");

        // A valid antiforgery token from the public login page must not be enough.
        var response = await _client.PostFormAsync("/Account/Login", $"/Settings/DeleteGroup/{group.Id}", []);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Does.Contain("/Account/Login"));
        Assert.That(await Service.GetGroupAsync(group.Id), Is.Not.Null);
    }

    [Test]
    public async Task Login_WithCorrectPasswordReturnsToRequestedPage()
    {
        var response = await _client.PostFormAsync("/Account/Login?ReturnUrl=%2FSettings%2FLinks", "/Account/Login", new()
        {
            ["Username"] = " Admin ",
            ["Password"] = LinkDashWebApplicationFactory.AdminPassword,
            ["ReturnUrl"] = "/Settings/Links"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Settings/Links"));
        var settings = await _client.GetAsync("/Settings");
        Assert.That(settings.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await settings.Content.ReadAsStringAsync(), Does.Contain("Sign out"));
    }

    [TestCase("https://evil.example.com/")]
    [TestCase("//evil.example.com/")]
    public async Task Login_IgnoresExternalReturnUrl(string returnUrl)
    {
        var response = await _client.PostFormAsync("/Account/Login", "/Account/Login", new()
        {
            ["Username"] = LinkDashWebApplicationFactory.AdminUsername,
            ["Password"] = LinkDashWebApplicationFactory.AdminPassword,
            ["ReturnUrl"] = returnUrl
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Settings"));
    }

    [TestCase(LinkDashWebApplicationFactory.AdminUsername, "wrong password")]
    [TestCase("someone", LinkDashWebApplicationFactory.AdminPassword)]
    public async Task Login_WithWrongCredentialsShowsError(string username, string password)
    {
        var response = await _client.SignInAsync(username, password);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var html = await response.Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("Wrong user name or password."));
        Assert.That(html, Does.Not.Contain(password), "The password must not be echoed back");
        Assert.That((await _client.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
    }

    [Test]
    public async Task Logout_SignsOut()
    {
        await _client.SignInAsync();

        var response = await _client.PostFormAsync("/Settings", "/Account/Logout", []);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That((await _client.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
    }

    [Test]
    public async Task ChangingPassword_SignsOutExistingSessions()
    {
        await _client.SignInAsync();
        Assert.That((await _client.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var options = _factory.Services.GetRequiredService<IOptionsMonitor<AdminOptions>>().CurrentValue;
        options.PasswordHash = AdminCredentials.HashPassword("a new password");

        Assert.That((await _client.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
    }

    [Test]
    public async Task Login_TooManyAttemptsAreRejected()
    {
        HttpResponseMessage response;
        var attempts = 0;
        do
        {
            response = await _client.SignInAsync(password: "wrong password");
            attempts++;
        } while (response.StatusCode == HttpStatusCode.OK && attempts < 20);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(attempts, Is.EqualTo(11));
    }
}
