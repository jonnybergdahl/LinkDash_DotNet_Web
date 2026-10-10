using Bergdahl.LinkDash.Core.Services;

namespace Bergdahl.LinkDash.Web.Models;

/// <summary>
/// Input for the shared icon picker field (<c>_IconField.cshtml</c>).
/// </summary>
/// <param name="Name">Form field name, e.g. "Icon" or "NewGroup.Icon".</param>
/// <param name="Value">Current icon, e.g. "mdi:home".</param>
/// <param name="EmptyIcon">Icon previewed while the field is empty.</param>
/// <param name="EmptyHint">Hint text explaining what an empty field means.</param>
public record IconFieldModel(string Name, string? Value, string EmptyIcon = LinkValidation.DefaultIcon, string? EmptyHint = null);
