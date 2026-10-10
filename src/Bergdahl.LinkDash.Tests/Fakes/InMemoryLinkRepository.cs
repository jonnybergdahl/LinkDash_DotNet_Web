using System.Text.Json;
using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Storage;

namespace Bergdahl.LinkDash.Tests.Fakes;

/// <summary>
/// Repository that keeps the document in memory. Every load returns a deep copy, mirroring
/// the file-based repository, so tests catch changes that are made but never saved.
/// </summary>
public class InMemoryLinkRepository : ILinkRepository
{
    private string _json = JsonSerializer.Serialize(new LinkData());

    public int SaveCount { get; private set; }

    public LinkData Current => JsonSerializer.Deserialize<LinkData>(_json)!;

    public Task<LinkData> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Current);

    public Task SaveAsync(LinkData data, CancellationToken cancellationToken = default)
    {
        _json = JsonSerializer.Serialize(data);
        SaveCount++;
        return Task.CompletedTask;
    }
}
