namespace Bergdahl.LinkDash.Core.Models;

/// <summary>
/// A single link shown on the dashboard.
/// </summary>
public class Link
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Optional Material Design Icons name in Home Assistant form, e.g. "mdi:home" or "mdi:email",
    /// or "favicon" to show the site's own icon.
    /// </summary>
    public string? Icon { get; set; }
}
