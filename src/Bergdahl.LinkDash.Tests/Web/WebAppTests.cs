using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bergdahl.LinkDash.Tests.Web;

public partial class WebAppTests
{
    private LinkDashWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new LinkDashWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private ILinkService Service => _factory.Services.GetRequiredService<ILinkService>();

    [Test]
    public async Task Home_ShowsEmptyStateWithoutData()
    {
        var html = await _client.GetStringAsync("/");

        Assert.That(html, Does.Contain("No links yet"));
    }

    [Test]
    public async Task Home_ShowsGroupsAndLinks()
    {
        var group = await Service.AddGroupAsync("Work");
        await Service.AddLinkAsync(group.Id, new LinkInput("Mail <inbox>", "https://mail.example.com", "Company mail", "mail"));

        var html = await _client.GetStringAsync("/");

        Assert.That(html, Does.Contain("Work"));
        Assert.That(html, Does.Contain("href=\"https://mail.example.com\""));
        Assert.That(html, Does.Contain("Mail &lt;inbox&gt;"), "Titles must be HTML encoded");
        Assert.That(html, Does.Contain("Company mail"));
    }

    [Test]
    public async Task Home_ServesFavicon()
    {
        var response = await _client.GetAsync("/favicon.ico");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Settings_ServesIconIndex()
    {
        var icons = await _client.GetFromJsonAsync<string[][]>("/lib/mdi/icons.json");

        Assert.That(icons, Has.Some.Matches<string[]>(icon => icon[0] == "router"));
    }

    [Test]
    public async Task SettingsGroups_ListsGroupsInTable()
    {
        var group = await Service.AddGroupAsync("Work", "mdi:briefcase");
        await Service.AddLinkAsync(group.Id, new LinkInput("Mail", "https://mail.example.com"));

        var html = await _client.GetStringAsync("/Settings");

        Assert.That(html, Does.Contain("class=\"data-table\""));
        Assert.That(html, Does.Contain($"id=\"group-{group.Id}\""));
        Assert.That(html, Does.Contain("mdi-briefcase"));
        Assert.That(html, Does.Contain($"href=\"/Settings/EditGroup/{group.Id}\""));
        Assert.That(html, Does.Contain($"action=\"/Settings/DeleteGroup/{group.Id}\""));
        Assert.That(html, Does.Contain("href=\"/Settings/AddGroup\""));
        Assert.That(html, Does.Contain("href=\"/Settings/Links\""), "Settings menu");
    }

    [Test]
    public async Task SettingsLinks_ShowsTabPerGroupAndFirstGroupsLinks()
    {
        var work = await Service.AddGroupAsync("Work");
        var home = await Service.AddGroupAsync("Home");
        var link = await Service.AddLinkAsync(work.Id, new LinkInput("Mail <inbox>", "https://mail.example.com", null, "mdi:email"));
        var other = await Service.AddLinkAsync(home.Id, new LinkInput("Router", "https://192.168.1.1"));

        var html = await _client.GetStringAsync("/Settings/Links");

        Assert.That(html, Does.Contain($"aria-current=\"page\" href=\"/Settings/Links?groupId={work.Id}\""));
        Assert.That(html, Does.Contain($"href=\"/Settings/Links?groupId={home.Id}\""));
        Assert.That(html, Does.Contain($"id=\"link-{link!.Id}\""));
        Assert.That(html, Does.Contain("Mail &lt;inbox&gt;"));
        Assert.That(html, Does.Contain("href=\"https://mail.example.com\""));
        Assert.That(html, Does.Contain($"href=\"/Settings/EditLink?groupId={work.Id}&amp;linkId={link.Id}\""));
        Assert.That(html, Does.Contain($"href=\"/Settings/AddLink?groupId={work.Id}\""));
        Assert.That(html, Does.Not.Contain($"id=\"link-{other!.Id}\""), "Only the selected group's links");
    }

    [Test]
    public async Task SettingsLinks_SelectsGroupFromQuery()
    {
        await Service.AddGroupAsync("Work");
        var home = await Service.AddGroupAsync("Home");
        var link = await Service.AddLinkAsync(home.Id, new LinkInput("Router", "https://192.168.1.1"));

        var html = await _client.GetStringAsync($"/Settings/Links?groupId={home.Id}");

        Assert.That(html, Does.Contain($"aria-current=\"page\" href=\"/Settings/Links?groupId={home.Id}\""));
        Assert.That(html, Does.Contain($"id=\"link-{link!.Id}\""));
    }

    [Test]
    public async Task AddLink_WithoutGroupsRedirectsToGroups()
    {
        var response = await _client.GetAsync("/Settings/AddLink");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Settings"));
    }

    [Test]
    public async Task AddLink_SavesToChosenGroup()
    {
        await Service.AddGroupAsync("First");
        var second = await Service.AddGroupAsync("Second");

        var response = await PostFormAsync("/Settings/AddLink", "/Settings/AddLink", new()
        {
            ["GroupId"] = second.Id.ToString(),
            ["Title"] = "Example",
            ["Url"] = "https://example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Does.StartWith($"/Settings/Links?groupId={second.Id}#link-"));
        Assert.That((await Service.GetGroupAsync(second.Id))!.Links.Single().Title, Is.EqualTo("Example"));
    }

    [Test]
    public async Task AddLink_WithUnknownGroupShowsError()
    {
        await Service.AddGroupAsync("Work");

        var response = await PostFormAsync("/Settings/AddLink", "/Settings/AddLink", new()
        {
            ["GroupId"] = Guid.NewGuid().ToString(),
            ["Title"] = "Example",
            ["Url"] = "https://example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Choose a group."));
    }

    [Test]
    public async Task AddGroup_PersistsToJsonFileAndRedirects()
    {
        var response = await PostFormAsync("/Settings/AddGroup", "/Settings/AddGroup", new() { ["Name"] = "Family" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(await File.ReadAllTextAsync(_factory.DataFilePath), Does.Contain("Family"));
    }

    [Test]
    public async Task AddGroup_SavesIconAndShowsItOnDashboard()
    {
        var response = await PostFormAsync("/Settings/AddGroup", "/Settings/AddGroup", new()
        {
            ["Name"] = "Network",
            ["Icon"] = "mdi:router-wireless"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That((await Service.GetGroupsAsync()).Single().Icon, Is.EqualTo("mdi:router-wireless"));
        Assert.That(await _client.GetStringAsync("/"), Does.Contain("mdi mdi-router-wireless"));
    }

    [Test]
    public async Task EditGroup_WithInvalidIconShowsError()
    {
        var group = await Service.AddGroupAsync("Work", "mdi:briefcase");

        var response = await PostFormAsync($"/Settings/EditGroup/{group.Id}", "/Settings/EditGroup", new()
        {
            ["Id"] = group.Id.ToString(),
            ["Name"] = "Work",
            ["Icon"] = "Not An Icon"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Use a Material Design Icons name"));
        Assert.That((await Service.GetGroupAsync(group.Id))!.Icon, Is.EqualTo("mdi:briefcase"));
    }

    [Test]
    public async Task AddGroup_WithEmptyNameShowsError()
    {
        var response = await PostFormAsync("/Settings/AddGroup", "/Settings/AddGroup", new() { ["Name"] = "" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Group name is required."));
        Assert.That(File.Exists(_factory.DataFilePath), Is.False);
    }

    [Test]
    public async Task Post_WithoutAntiforgeryTokenIsRejected()
    {
        var group = await Service.AddGroupAsync("Work");

        var response = await _client.PostAsync($"/Settings/DeleteGroup/{group.Id}", new FormUrlEncodedContent([]));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await Service.GetGroupAsync(group.Id), Is.Not.Null);
    }

    [Test]
    public async Task AddLink_RejectsJavascriptUrl()
    {
        var group = await Service.AddGroupAsync("Work");

        var response = await PostFormAsync($"/Settings/AddLink?groupId={group.Id}", "/Settings/AddLink", new()
        {
            ["GroupId"] = group.Id.ToString(),
            ["Title"] = "Evil",
            ["Url"] = "javascript:alert(1)"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Service.GetGroupAsync(group.Id))!.Links, Is.Empty);
    }

    [Test]
    public async Task EditLink_UpdatesLink()
    {
        var group = await Service.AddGroupAsync("Work");
        var link = await Service.AddLinkAsync(group.Id, new LinkInput("Old", "https://old.example.com"));

        var response = await PostFormAsync($"/Settings/EditLink?groupId={group.Id}&linkId={link!.Id}", "/Settings/EditLink", new()
        {
            ["GroupId"] = group.Id.ToString(),
            ["LinkId"] = link.Id.ToString(),
            ["Title"] = "New",
            ["Url"] = "https://new.example.com",
            ["Icon"] = "star"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        var saved = await Service.GetLinkAsync(group.Id, link.Id);
        Assert.That(saved!.Title, Is.EqualTo("New"));
        Assert.That(saved.Icon, Is.EqualTo("mdi:star"));
    }

    [Test]
    public async Task MoveAndDeleteGroup_Work()
    {
        var a = await Service.AddGroupAsync("A");
        var b = await Service.AddGroupAsync("B");

        await PostFormAsync("/Settings", $"/Settings/MoveGroup/{b.Id}?direction=Up", []);
        Assert.That((await Service.GetGroupsAsync()).Select(g => g.Name), Is.EqualTo(new[] { "B", "A" }));

        await PostFormAsync("/Settings", $"/Settings/DeleteGroup/{a.Id}", []);
        Assert.That((await Service.GetGroupsAsync()).Select(g => g.Name), Is.EqualTo(new[] { "B" }));
    }

    [Test]
    public async Task EditUnknownGroup_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/Settings/EditGroup/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// Loads <paramref name="pageUrl"/> to obtain an antiforgery token and cookie, then posts the form.
    /// </summary>
    private async Task<HttpResponseMessage> PostFormAsync(string pageUrl, string postUrl, Dictionary<string, string> fields)
    {
        var page = await _client.GetAsync(pageUrl);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenRegex().Match(html).Groups[1].Value;

        // The test client keeps the antiforgery cookie from the GET between requests.
        return await _client.PostAsync(postUrl,
            new FormUrlEncodedContent(fields.Append(new("__RequestVerificationToken", token))));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
