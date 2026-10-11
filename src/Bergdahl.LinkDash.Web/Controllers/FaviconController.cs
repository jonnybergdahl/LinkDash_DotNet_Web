using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Web.Favicons;
using Microsoft.AspNetCore.Mvc;

namespace Bergdahl.LinkDash.Web.Controllers;

/// <summary>
/// Serves the cached favicons of links that use the "favicon" icon. Public like the dashboard.
/// Only sites that are already saved as links are ever fetched.
/// </summary>
[Route("favicons")]
public class FaviconController(ILinkService linkService, FaviconService faviconService) : Controller
{
    /// <param name="linkId">The link whose site icon to show.</param>
    /// <param name="v">Ignored; <see cref="FaviconService.CacheKey"/> of the link URL, so browsers reload the icon when the URL changes.</param>
    [HttpGet("{linkId:guid}")]
    public async Task<IActionResult> Get(Guid linkId, string? v, CancellationToken cancellationToken)
    {
        var groups = await linkService.GetGroupsAsync(cancellationToken);
        var link = groups.SelectMany(g => g.Links).FirstOrDefault(l => l.Id == linkId);
        if (link is null || await faviconService.GetAsync(link.Url, cancellationToken) is not { } image)
        {
            return NotFound();
        }

        // Icons may be SVG from another site; if one is opened directly it must not be able to run script on this site.
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(image.Data, image.ContentType);
    }
}
