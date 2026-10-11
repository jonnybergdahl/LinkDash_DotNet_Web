using System.Net;
using Bergdahl.LinkDash.Web.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bergdahl.LinkDash.Tests.Web;

/// <summary>
/// First sign-in without a configured administrator, where the user creates the account.
/// </summary>
public class CreateAccountTests
{
    private const string Password = "my new password";

    private LinkDashWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new LinkDashWebApplicationFactory(configureAdmin: false);
        _client = CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private string AccountFilePath => _factory.Services.GetRequiredService<AdminAccountStore>().FilePath;

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private Task<HttpResponseMessage> CreateAccountAsync(HttpClient client, string username, string password, string confirm, string? returnUrl = null) =>
        client.PostFormAsync("/Account/CreateAccount", "/Account/CreateAccount", new()
        {
            ["Username"] = username,
            ["Password"] = password,
            ["ConfirmPassword"] = confirm,
            ["ReturnUrl"] = returnUrl ?? ""
        });

    [Test]
    public async Task Login_WithoutAccountShowsCreateAccount()
    {
        var response = await _client.GetAsync("/Account/Login?ReturnUrl=%2FSettings%2FLinks");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Account/CreateAccount?returnUrl=%2FSettings%2FLinks"));
        var html = await _client.GetStringAsync("/Account/CreateAccount");
        Assert.That(html, Does.Contain("Create account"));
        Assert.That((await _client.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
    }

    [Test]
    public async Task CreateAccount_SavesAccountAndSignsIn()
    {
        var response = await CreateAccountAsync(_client, " Jonny ", Password, Password, "/Settings/Links");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Settings/Links"));
        Assert.That((await _client.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var file = await File.ReadAllTextAsync(AccountFilePath);
        Assert.That(file, Does.Contain("\"Jonny\""));
        Assert.That(file, Does.Not.Contain(Password), "Only the hash is stored");

        using var other = CreateClient();
        Assert.That((await other.SignInAsync("jonny", Password)).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That((await other.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CreateAccount_IsOnlyPossibleOnce()
    {
        await CreateAccountAsync(_client, "first", Password, Password);
        using var other = CreateClient();

        var page = await other.GetAsync("/Account/CreateAccount");
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(page.Headers.Location?.OriginalString, Does.StartWith("/Account/Login"));

        // A form loaded before the account existed must not replace it.
        var response = await other.PostFormAsync("/Account/Login", "/Account/CreateAccount", new()
        {
            ["Username"] = "second",
            ["Password"] = "another password",
            ["ConfirmPassword"] = "another password"
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Does.StartWith("/Account/Login"));
        Assert.That(await File.ReadAllTextAsync(AccountFilePath), Does.Contain("\"first\""));
        Assert.That((await other.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
    }

    [TestCase("admin", "short", "short", "at least 6 characters")]
    [TestCase("admin", Password, "something else", "The passwords do not match.")]
    [TestCase("", Password, Password, "User name is required.")]
    public async Task CreateAccount_WithInvalidInputShowsError(string username, string password, string confirm, string error)
    {
        var response = await CreateAccountAsync(_client, username, password, confirm);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain(error));
        Assert.That(File.Exists(AccountFilePath), Is.False);
    }

    [Test]
    public async Task DeletingAccountFile_BringsBackCreateAccount()
    {
        await CreateAccountAsync(_client, "admin", Password, Password);

        File.Delete(AccountFilePath);

        Assert.That((await _client.GetAsync("/Settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect), "Old session is signed out");
        var login = await _client.GetAsync("/Account/Login");
        Assert.That(login.Headers.Location?.OriginalString, Does.StartWith("/Account/CreateAccount"));
    }
}
