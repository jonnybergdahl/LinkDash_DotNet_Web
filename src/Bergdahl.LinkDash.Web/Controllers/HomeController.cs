using System.Diagnostics;
using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bergdahl.LinkDash.Web.Controllers;

public class HomeController(ILinkService linkService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var groups = await linkService.GetGroupsAsync(cancellationToken);
        return View(groups);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
