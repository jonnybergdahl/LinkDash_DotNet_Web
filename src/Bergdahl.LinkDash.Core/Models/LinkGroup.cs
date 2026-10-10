namespace Bergdahl.LinkDash.Core.Models;

/// <summary>
/// A named group of links.
/// </summary>
public class LinkGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional Material Design Icons name in Home Assistant form, e.g. "mdi:briefcase". No icon is shown when empty.
    /// </summary>
    public string? Icon { get; set; }

    public List<Link> Links { get; set; } = [];
}
