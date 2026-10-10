using Bergdahl.LinkDash.Core.Models;

namespace Bergdahl.LinkDash.Web.Models;

/// <summary>
/// The links settings page: one tab per group, showing the links of the selected group.
/// </summary>
public class LinksViewModel
{
    public IReadOnlyList<LinkGroup> Groups { get; init; } = [];

    /// <summary>
    /// Null only when there are no groups.
    /// </summary>
    public LinkGroup? SelectedGroup { get; init; }
}
