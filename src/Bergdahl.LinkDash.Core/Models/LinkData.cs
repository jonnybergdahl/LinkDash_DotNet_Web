namespace Bergdahl.LinkDash.Core.Models;

/// <summary>
/// Root document persisted to the JSON data file.
/// </summary>
public class LinkData
{
    public List<LinkGroup> Groups { get; set; } = [];
}
