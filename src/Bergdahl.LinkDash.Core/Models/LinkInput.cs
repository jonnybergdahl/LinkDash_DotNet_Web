namespace Bergdahl.LinkDash.Core.Models;

/// <summary>
/// Values used when creating or updating a link.
/// </summary>
public record LinkInput(string Title, string Url, string? Description = null, string? Icon = null);
