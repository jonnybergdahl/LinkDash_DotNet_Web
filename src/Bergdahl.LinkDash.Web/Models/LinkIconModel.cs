using Bergdahl.LinkDash.Core.Models;

namespace Bergdahl.LinkDash.Web.Models;

/// <summary>
/// Input for <c>_LinkIcon.cshtml</c>, which shows a link's MDI icon or its site's favicon.
/// </summary>
/// <param name="Link">The link.</param>
/// <param name="CssClass">Class of the round icon container, e.g. "link-icon" or "table-icon".</param>
public record LinkIconModel(Link Link, string CssClass);
