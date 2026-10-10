using Bergdahl.LinkDash.Core.Models;

namespace Bergdahl.LinkDash.Core.Storage;

/// <summary>
/// Loads and saves the complete link document.
/// </summary>
public interface ILinkRepository
{
    Task<LinkData> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LinkData data, CancellationToken cancellationToken = default);
}
