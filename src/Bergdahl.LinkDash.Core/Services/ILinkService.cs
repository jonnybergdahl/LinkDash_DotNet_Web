using Bergdahl.LinkDash.Core.Models;

namespace Bergdahl.LinkDash.Core.Services;

/// <summary>
/// Operations on groups and links. Methods that target an existing item return
/// <c>false</c> or <c>null</c> when the item cannot be found, and throw
/// <see cref="ArgumentException"/> when the input is invalid.
/// </summary>
public interface ILinkService
{
    Task<IReadOnlyList<LinkGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<LinkGroup?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<Link?> GetLinkAsync(Guid groupId, Guid linkId, CancellationToken cancellationToken = default);

    Task<LinkGroup> AddGroupAsync(string name, string? icon = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets both the name and the icon; a null or empty icon removes it.
    /// </summary>
    Task<bool> UpdateGroupAsync(Guid groupId, string name, string? icon = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<bool> MoveGroupAsync(Guid groupId, MoveDirection direction, CancellationToken cancellationToken = default);

    Task<Link?> AddLinkAsync(Guid groupId, LinkInput input, CancellationToken cancellationToken = default);

    Task<bool> UpdateLinkAsync(Guid groupId, Guid linkId, LinkInput input, CancellationToken cancellationToken = default);

    Task<bool> DeleteLinkAsync(Guid groupId, Guid linkId, CancellationToken cancellationToken = default);

    Task<bool> MoveLinkAsync(Guid groupId, Guid linkId, MoveDirection direction, CancellationToken cancellationToken = default);
}
