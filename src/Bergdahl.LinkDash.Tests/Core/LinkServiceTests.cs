using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Tests.Fakes;

namespace Bergdahl.LinkDash.Tests.Core;

public class LinkServiceTests
{
    private InMemoryLinkRepository _repository = null!;
    private LinkService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryLinkRepository();
        _service = new LinkService(_repository);
    }

    [Test]
    public async Task AddGroup_TrimsNameAndSaves()
    {
        var group = await _service.AddGroupAsync("  Work  ");

        Assert.That(group.Name, Is.EqualTo("Work"));
        Assert.That(_repository.Current.Groups.Single().Id, Is.EqualTo(group.Id));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void AddGroup_RejectsEmptyName(string name)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.AddGroupAsync(name));
        Assert.That(_repository.SaveCount, Is.Zero);
    }

    [Test]
    public void AddGroup_RejectsTooLongName()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.AddGroupAsync(new string('x', LinkValidation.MaxGroupNameLength + 1)));
    }

    [Test]
    public async Task UpdateGroup_RenamesGroup()
    {
        var group = await _service.AddGroupAsync("Old");

        var updated = await _service.UpdateGroupAsync(group.Id, "New");

        Assert.That(updated, Is.True);
        Assert.That((await _service.GetGroupAsync(group.Id))!.Name, Is.EqualTo("New"));
    }

    [Test]
    public async Task AddGroup_NormalizesIcon()
    {
        var group = await _service.AddGroupAsync("Work", " briefcase ");

        Assert.That(_repository.Current.Groups.Single(g => g.Id == group.Id).Icon, Is.EqualTo("mdi:briefcase"));
    }

    [Test]
    public void AddGroup_RejectsInvalidIcon()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.AddGroupAsync("Work", "Not An Icon"));
        Assert.That(_repository.SaveCount, Is.Zero);
    }

    [Test]
    public async Task UpdateGroup_ChangesAndClearsIcon()
    {
        var group = await _service.AddGroupAsync("Work", "mdi:briefcase");

        await _service.UpdateGroupAsync(group.Id, "Work", "mdi:home");
        Assert.That((await _service.GetGroupAsync(group.Id))!.Icon, Is.EqualTo("mdi:home"));

        await _service.UpdateGroupAsync(group.Id, "Work", "");
        Assert.That((await _service.GetGroupAsync(group.Id))!.Icon, Is.Null);
    }

    [Test]
    public async Task UpdateGroup_ReturnsFalseForUnknownGroup()
    {
        Assert.That(await _service.UpdateGroupAsync(Guid.NewGuid(), "Name"), Is.False);
        Assert.That(_repository.SaveCount, Is.Zero);
    }

    [Test]
    public async Task DeleteGroup_RemovesGroupAndItsLinks()
    {
        var keep = await _service.AddGroupAsync("Keep");
        var remove = await _service.AddGroupAsync("Remove");
        await _service.AddLinkAsync(remove.Id, new LinkInput("Example", "https://example.com"));

        Assert.That(await _service.DeleteGroupAsync(remove.Id), Is.True);
        Assert.That(_repository.Current.Groups.Select(g => g.Id), Is.EqualTo(new[] { keep.Id }));
        Assert.That(await _service.DeleteGroupAsync(remove.Id), Is.False);
    }

    [Test]
    public async Task MoveGroup_SwapsWithNeighbour()
    {
        var a = await _service.AddGroupAsync("A");
        var b = await _service.AddGroupAsync("B");
        var c = await _service.AddGroupAsync("C");

        await _service.MoveGroupAsync(c.Id, MoveDirection.Up);
        Assert.That(GroupNames(), Is.EqualTo(new[] { "A", "C", "B" }));

        await _service.MoveGroupAsync(a.Id, MoveDirection.Down);
        Assert.That(GroupNames(), Is.EqualTo(new[] { "C", "A", "B" }));

        Assert.That(b.Id, Is.EqualTo(_repository.Current.Groups[2].Id));
    }

    [Test]
    public async Task MoveGroup_AtEdgeIsNoOp()
    {
        var a = await _service.AddGroupAsync("A");
        var b = await _service.AddGroupAsync("B");

        Assert.That(await _service.MoveGroupAsync(a.Id, MoveDirection.Up), Is.True);
        Assert.That(await _service.MoveGroupAsync(b.Id, MoveDirection.Down), Is.True);
        Assert.That(GroupNames(), Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public async Task MoveGroup_ReturnsFalseForUnknownGroup()
    {
        Assert.That(await _service.MoveGroupAsync(Guid.NewGuid(), MoveDirection.Up), Is.False);
    }

    [Test]
    public async Task AddLink_NormalizesInput()
    {
        var group = await _service.AddGroupAsync("Work");

        var link = await _service.AddLinkAsync(group.Id, new LinkInput(" Mail ", " https://mail.example.com ", "  ", " mail "));

        Assert.That(link, Is.Not.Null);
        var saved = _repository.Current.Groups.Single().Links.Single();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Id, Is.EqualTo(link!.Id));
            Assert.That(saved.Title, Is.EqualTo("Mail"));
            Assert.That(saved.Url, Is.EqualTo("https://mail.example.com"));
            Assert.That(saved.Description, Is.Null);
            Assert.That(saved.Icon, Is.EqualTo("mdi:mail"));
        });
    }

    [Test]
    public async Task AddLink_ReturnsNullForUnknownGroup()
    {
        Assert.That(await _service.AddLinkAsync(Guid.NewGuid(), new LinkInput("Example", "https://example.com")), Is.Null);
        Assert.That(_repository.SaveCount, Is.Zero);
    }

    [TestCase("", "https://example.com", null)]
    [TestCase("Title", "javascript:alert(1)", null)]
    [TestCase("Title", "example.com", null)]
    [TestCase("Title", "https://example.com", "Not An Icon")]
    public async Task AddLink_RejectsInvalidInput(string title, string url, string? icon)
    {
        var group = await _service.AddGroupAsync("Work");
        var savesBefore = _repository.SaveCount;

        Assert.ThrowsAsync<ArgumentException>(() => _service.AddLinkAsync(group.Id, new LinkInput(title, url, null, icon)));
        Assert.That(_repository.SaveCount, Is.EqualTo(savesBefore));
    }

    [Test]
    public async Task UpdateLink_ChangesAllFields()
    {
        var group = await _service.AddGroupAsync("Work");
        var link = await _service.AddLinkAsync(group.Id, new LinkInput("Old", "https://old.example.com", "Old description", "home"));

        var updated = await _service.UpdateLinkAsync(group.Id, link!.Id, new LinkInput("New", "https://new.example.com"));

        Assert.That(updated, Is.True);
        var saved = await _service.GetLinkAsync(group.Id, link.Id);
        Assert.Multiple(() =>
        {
            Assert.That(saved!.Title, Is.EqualTo("New"));
            Assert.That(saved.Url, Is.EqualTo("https://new.example.com"));
            Assert.That(saved.Description, Is.Null);
            Assert.That(saved.Icon, Is.Null);
        });
    }

    [Test]
    public async Task UpdateLink_ReturnsFalseForUnknownLink()
    {
        var group = await _service.AddGroupAsync("Work");

        Assert.That(await _service.UpdateLinkAsync(group.Id, Guid.NewGuid(), new LinkInput("T", "https://example.com")), Is.False);
    }

    [Test]
    public async Task DeleteLink_RemovesOnlyThatLink()
    {
        var group = await _service.AddGroupAsync("Work");
        var a = await _service.AddLinkAsync(group.Id, new LinkInput("A", "https://a.example.com"));
        var b = await _service.AddLinkAsync(group.Id, new LinkInput("B", "https://b.example.com"));

        Assert.That(await _service.DeleteLinkAsync(group.Id, a!.Id), Is.True);
        Assert.That(_repository.Current.Groups.Single().Links.Select(l => l.Id), Is.EqualTo(new[] { b!.Id }));
        Assert.That(await _service.DeleteLinkAsync(group.Id, a.Id), Is.False);
    }

    [Test]
    public async Task MoveLink_SwapsWithNeighbour()
    {
        var group = await _service.AddGroupAsync("Work");
        await _service.AddLinkAsync(group.Id, new LinkInput("A", "https://a.example.com"));
        var b = await _service.AddLinkAsync(group.Id, new LinkInput("B", "https://b.example.com"));

        Assert.That(await _service.MoveLinkAsync(group.Id, b!.Id, MoveDirection.Up), Is.True);
        Assert.That(_repository.Current.Groups.Single().Links.Select(l => l.Title), Is.EqualTo(new[] { "B", "A" }));
    }

    [Test]
    public async Task ConcurrentAdds_AreNotLost()
    {
        var group = await _service.AddGroupAsync("Work");

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => _service.AddLinkAsync(group.Id, new LinkInput($"Link {i}", $"https://example.com/{i}")))));

        Assert.That(_repository.Current.Groups.Single().Links, Has.Count.EqualTo(50));
    }

    private IEnumerable<string> GroupNames() => _repository.Current.Groups.Select(g => g.Name);
}
