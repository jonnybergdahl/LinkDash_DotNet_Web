using Bergdahl.LinkDash.Core.Configuration;
using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bergdahl.LinkDash.Tests.Core;

public class JsonFileLinkRepositoryTests
{
    private string _directory = null!;
    private string _filePath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "LinkDashTests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "nested", "links.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task Load_ReturnsEmptyDocumentWhenFileIsMissing()
    {
        var data = await CreateRepository().LoadAsync();

        Assert.That(data.Groups, Is.Empty);
        Assert.That(File.Exists(_filePath), Is.False);
    }

    [Test]
    public async Task Save_CreatesDirectoryAndRoundTrips()
    {
        var repository = CreateRepository();
        var group = new LinkGroup
        {
            Name = "Work",
            Links = [new Link { Title = "Mail", Url = "https://mail.example.com", Description = "Inbox", Icon = "mdi:email" }]
        };

        await repository.SaveAsync(new LinkData { Groups = [group] });
        var loaded = await CreateRepository().LoadAsync();

        var link = loaded.Groups.Single().Links.Single();
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Groups.Single().Id, Is.EqualTo(group.Id));
            Assert.That(loaded.Groups.Single().Name, Is.EqualTo("Work"));
            Assert.That(link.Title, Is.EqualTo("Mail"));
            Assert.That(link.Url, Is.EqualTo("https://mail.example.com"));
            Assert.That(link.Description, Is.EqualTo("Inbox"));
            Assert.That(link.Icon, Is.EqualTo("mdi:email"));
            Assert.That(File.Exists(_filePath + ".tmp"), Is.False);
        });
    }

    [Test]
    public async Task Save_WritesReadableCamelCaseJson()
    {
        await CreateRepository().SaveAsync(new LinkData { Groups = [new LinkGroup { Name = "Work" }] });

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.That(json, Does.Contain("\"groups\"").And.Contain("\"name\": \"Work\""));
    }

    [Test]
    public async Task Load_HandlesEmptyFileAndNullLists()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, "");
        Assert.That((await CreateRepository().LoadAsync()).Groups, Is.Empty);

        await File.WriteAllTextAsync(_filePath, """{ "groups": [ { "name": "A", "links": null }, null ] }""");
        var data = await CreateRepository().LoadAsync();
        Assert.That(data.Groups, Has.Count.EqualTo(1));
        Assert.That(data.Groups[0].Links, Is.Empty);
    }

    [Test]
    public async Task Load_ThrowsOnInvalidJsonInsteadOfDiscardingIt()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, "{ not json");

        Assert.ThrowsAsync<InvalidDataException>(() => CreateRepository().LoadAsync());
        Assert.That(await File.ReadAllTextAsync(_filePath), Is.EqualTo("{ not json"));
    }

    [Test]
    public void Constructor_RequiresPath()
    {
        Assert.Throws<ArgumentException>(() => new JsonFileLinkRepository(
            Options.Create(new LinkDashOptions { DataFilePath = " " }),
            NullLogger<JsonFileLinkRepository>.Instance));
    }

    private JsonFileLinkRepository CreateRepository() => new(
        Options.Create(new LinkDashOptions { DataFilePath = _filePath }),
        NullLogger<JsonFileLinkRepository>.Instance);
}
