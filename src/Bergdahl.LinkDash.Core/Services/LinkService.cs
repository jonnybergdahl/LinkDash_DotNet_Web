using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Storage;

namespace Bergdahl.LinkDash.Core.Services;

/// <summary>
/// Default <see cref="ILinkService"/>. Every change is a load-modify-save of the whole
/// document, serialized with a lock so concurrent requests cannot lose updates.
/// Register as a singleton so the lock is shared.
/// </summary>
public class LinkService(ILinkRepository repository) : ILinkService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<IReadOnlyList<LinkGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var data = await repository.LoadAsync(cancellationToken);
        return data.Groups;
    }

    public async Task<LinkGroup?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var data = await repository.LoadAsync(cancellationToken);
        return FindGroup(data, groupId);
    }

    public async Task<Link?> GetLinkAsync(Guid groupId, Guid linkId, CancellationToken cancellationToken = default)
    {
        var data = await repository.LoadAsync(cancellationToken);
        return FindGroup(data, groupId)?.Links.FirstOrDefault(l => l.Id == linkId);
    }

    public async Task<LinkGroup> AddGroupAsync(string name, string? icon = null, CancellationToken cancellationToken = default)
    {
        var group = new LinkGroup { Name = ValidateGroupName(name), Icon = ValidateIcon(icon) };
        await UpdateAsync(data =>
        {
            data.Groups.Add(group);
            return true;
        }, cancellationToken);
        return group;
    }

    public Task<bool> UpdateGroupAsync(Guid groupId, string name, string? icon = null, CancellationToken cancellationToken = default)
    {
        var validName = ValidateGroupName(name);
        var validIcon = ValidateIcon(icon);
        return UpdateAsync(data =>
        {
            var group = FindGroup(data, groupId);
            if (group is null)
            {
                return false;
            }

            group.Name = validName;
            group.Icon = validIcon;
            return true;
        }, cancellationToken);
    }

    public Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        UpdateAsync(data => data.Groups.RemoveAll(g => g.Id == groupId) > 0, cancellationToken);

    public Task<bool> MoveGroupAsync(Guid groupId, MoveDirection direction, CancellationToken cancellationToken = default) =>
        UpdateAsync(data => Move(data.Groups, data.Groups.FindIndex(g => g.Id == groupId), direction), cancellationToken);

    public async Task<Link?> AddLinkAsync(Guid groupId, LinkInput input, CancellationToken cancellationToken = default)
    {
        var link = new Link();
        Apply(link, input);
        var added = await UpdateAsync(data =>
        {
            var group = FindGroup(data, groupId);
            if (group is null)
            {
                return false;
            }

            group.Links.Add(link);
            return true;
        }, cancellationToken);
        return added ? link : null;
    }

    public Task<bool> UpdateLinkAsync(Guid groupId, Guid linkId, LinkInput input, CancellationToken cancellationToken = default)
    {
        // Validate up front so invalid input fails before the file is touched.
        Apply(new Link(), input);
        return UpdateAsync(data =>
        {
            var link = FindGroup(data, groupId)?.Links.FirstOrDefault(l => l.Id == linkId);
            if (link is null)
            {
                return false;
            }

            Apply(link, input);
            return true;
        }, cancellationToken);
    }

    public Task<bool> DeleteLinkAsync(Guid groupId, Guid linkId, CancellationToken cancellationToken = default) =>
        UpdateAsync(data => FindGroup(data, groupId)?.Links.RemoveAll(l => l.Id == linkId) > 0, cancellationToken);

    public Task<bool> MoveLinkAsync(Guid groupId, Guid linkId, MoveDirection direction, CancellationToken cancellationToken = default) =>
        UpdateAsync(data =>
        {
            var links = FindGroup(data, groupId)?.Links;
            return links is not null && Move(links, links.FindIndex(l => l.Id == linkId), direction);
        }, cancellationToken);

    /// <summary>
    /// Runs <paramref name="change"/> on a freshly loaded document and saves it when the change reports success.
    /// </summary>
    private async Task<bool> UpdateAsync(Func<LinkData, bool> change, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var data = await repository.LoadAsync(cancellationToken);
            if (!change(data))
            {
                return false;
            }

            await repository.SaveAsync(data, cancellationToken);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static LinkGroup? FindGroup(LinkData data, Guid groupId) =>
        data.Groups.FirstOrDefault(g => g.Id == groupId);

    /// <summary>
    /// Moves the item at <paramref name="index"/> one step. Returns <c>false</c> only when the item was not found;
    /// moving past either end is a successful no-op.
    /// </summary>
    private static bool Move<T>(List<T> items, int index, MoveDirection direction)
    {
        if (index < 0)
        {
            return false;
        }

        var target = direction == MoveDirection.Up ? index - 1 : index + 1;
        if (target >= 0 && target < items.Count)
        {
            (items[index], items[target]) = (items[target], items[index]);
        }

        return true;
    }

    private static string ValidateGroupName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Group name is required.");
        }

        if (trimmed.Length > LinkValidation.MaxGroupNameLength)
        {
            throw new ArgumentException($"Group name can be at most {LinkValidation.MaxGroupNameLength} characters.");
        }

        return trimmed;
    }

    private static void Apply(Link link, LinkInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var title = input.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new ArgumentException("Link title is required.");
        }

        if (title.Length > LinkValidation.MaxTitleLength)
        {
            throw new ArgumentException($"Link title can be at most {LinkValidation.MaxTitleLength} characters.");
        }

        if (!LinkValidation.IsValidUrl(input.Url))
        {
            throw new ArgumentException("Link URL must be an absolute http or https address.");
        }

        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description?.Length > LinkValidation.MaxDescriptionLength)
        {
            throw new ArgumentException($"Description can be at most {LinkValidation.MaxDescriptionLength} characters.");
        }

        var icon = ValidateIcon(input.Icon);

        link.Title = title;
        link.Url = input.Url.Trim();
        link.Description = description;
        link.Icon = icon;
    }

    private static string? ValidateIcon(string? icon)
    {
        if (!LinkValidation.IsValidIcon(icon))
        {
            throw new ArgumentException("Icon must be a Material Design Icons name such as \"mdi:home\" (lower case letters, digits and dashes).");
        }

        return LinkValidation.NormalizeIcon(icon);
    }
}
