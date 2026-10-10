namespace Bergdahl.LinkDash.Core.Configuration;

/// <summary>
/// Application settings bound from the "LinkDash" configuration section.
/// </summary>
public class LinkDashOptions
{
    public const string SectionName = "LinkDash";

    /// <summary>
    /// Path to the JSON file holding groups and links. Relative paths are resolved
    /// against the application content root.
    /// </summary>
    public string DataFilePath { get; set; } = "App_Data/links.json";
}
