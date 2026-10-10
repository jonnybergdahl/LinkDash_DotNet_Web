using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bergdahl.LinkDash.Web.Controllers;

/// <summary>
/// The settings pages: a table of groups (<see cref="Index"/>) and a table of links (<see cref="Links"/>),
/// each with add, edit, reorder and delete actions.
/// </summary>
public class SettingsController(ILinkService linkService) : Controller
{
    /// <summary>
    /// TempData key for the confirmation message shown as a snackbar after a redirect.
    /// </summary>
    public const string MessageKey = "Message";

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await linkService.GetGroupsAsync(cancellationToken));
    }

    /// <param name="groupId">The group tab to show; defaults to the first group.</param>
    public async Task<IActionResult> Links(Guid? groupId, CancellationToken cancellationToken)
    {
        var groups = await linkService.GetGroupsAsync(cancellationToken);
        return View(new LinksViewModel
        {
            Groups = groups,
            SelectedGroup = groups.FirstOrDefault(g => g.Id == groupId) ?? groups.FirstOrDefault()
        });
    }

    [HttpGet]
    public IActionResult AddGroup()
    {
        return View("EditGroup", new GroupFormModel());
    }

    [HttpPost]
    public async Task<IActionResult> AddGroup(GroupFormModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        if (!ModelState.IsValid)
        {
            return View("EditGroup", model);
        }

        LinkGroup? group = null;
        if (!await TryAsync(async () => group = await linkService.AddGroupAsync(model.Name, model.Icon, cancellationToken), nameof(model.Name)))
        {
            return View("EditGroup", model);
        }

        return RedirectWithMessage($"Added group \"{group!.Name}\".", nameof(Index), $"group-{group.Id}");
    }

    [HttpGet]
    public async Task<IActionResult> EditGroup(Guid id, CancellationToken cancellationToken)
    {
        var group = await linkService.GetGroupAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        return View(new GroupFormModel { Id = group.Id, Name = group.Name, Icon = group.Icon });
    }

    [HttpPost]
    public async Task<IActionResult> EditGroup(GroupFormModel model, CancellationToken cancellationToken)
    {
        if (model.Id is not { } id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = false;
        if (!await TryAsync(async () => updated = await linkService.UpdateGroupAsync(id, model.Name, model.Icon, cancellationToken), nameof(model.Name)))
        {
            return View(model);
        }

        return updated ? RedirectWithMessage("Group saved.", nameof(Index), $"group-{id}") : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken cancellationToken)
    {
        return await linkService.DeleteGroupAsync(id, cancellationToken)
            ? RedirectWithMessage("Group deleted.", nameof(Index))
            : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> MoveGroup(Guid id, MoveDirection direction, CancellationToken cancellationToken)
    {
        return await linkService.MoveGroupAsync(id, direction, cancellationToken)
            ? RedirectTo(nameof(Index), $"group-{id}")
            : NotFound();
    }

    /// <param name="groupId">Group to preselect; defaults to the first group.</param>
    [HttpGet]
    public async Task<IActionResult> AddLink(Guid? groupId, CancellationToken cancellationToken)
    {
        var groups = await linkService.GetGroupsAsync(cancellationToken);
        if (groups.Count == 0)
        {
            return RedirectWithMessage("Add a group before adding links.", nameof(Index));
        }

        var group = groups.FirstOrDefault(g => g.Id == groupId) ?? groups[0];
        return View("EditLink", new LinkFormModel { GroupId = group.Id, Groups = groups });
    }

    [HttpPost]
    public async Task<IActionResult> AddLink(LinkFormModel model, CancellationToken cancellationToken)
    {
        model.LinkId = null;
        model.Groups = await linkService.GetGroupsAsync(cancellationToken);
        if (model.Groups.All(g => g.Id != model.GroupId))
        {
            ModelState.AddModelError(nameof(model.GroupId), "Choose a group.");
        }

        if (!ModelState.IsValid)
        {
            return View("EditLink", model);
        }

        Link? link = null;
        if (!await TryAsync(async () => link = await linkService.AddLinkAsync(model.GroupId, model.ToInput(), cancellationToken), nameof(model.Url)))
        {
            return View("EditLink", model);
        }

        var groupName = model.Groups.First(g => g.Id == model.GroupId).Name;
        return link is null
            ? NotFound()
            : RedirectWithMessage($"Added \"{link.Title}\" to {groupName}.", nameof(Links), $"link-{link.Id}", new { groupId = model.GroupId });
    }

    [HttpGet]
    public async Task<IActionResult> EditLink(Guid groupId, Guid linkId, CancellationToken cancellationToken)
    {
        var group = await linkService.GetGroupAsync(groupId, cancellationToken);
        var link = group?.Links.FirstOrDefault(l => l.Id == linkId);
        if (group is null || link is null)
        {
            return NotFound();
        }

        return View(LinkFormModel.FromLink(group, link));
    }

    [HttpPost]
    public async Task<IActionResult> EditLink(LinkFormModel model, CancellationToken cancellationToken)
    {
        if (model.LinkId is not { } linkId)
        {
            return BadRequest();
        }

        var group = await linkService.GetGroupAsync(model.GroupId, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        model.GroupName = group.Name;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = false;
        if (!await TryAsync(async () => updated = await linkService.UpdateLinkAsync(model.GroupId, linkId, model.ToInput(), cancellationToken), nameof(model.Url)))
        {
            return View(model);
        }

        return updated
            ? RedirectWithMessage("Link saved.", nameof(Links), $"link-{linkId}", new { groupId = group.Id })
            : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> DeleteLink(Guid groupId, Guid linkId, CancellationToken cancellationToken)
    {
        return await linkService.DeleteLinkAsync(groupId, linkId, cancellationToken)
            ? RedirectWithMessage("Link deleted.", nameof(Links), routeValues: new { groupId })
            : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> MoveLink(Guid groupId, Guid linkId, MoveDirection direction, CancellationToken cancellationToken)
    {
        return await linkService.MoveLinkAsync(groupId, linkId, direction, cancellationToken)
            ? RedirectTo(nameof(Links), $"link-{linkId}", new { groupId })
            : NotFound();
    }

    /// <summary>
    /// Runs a service call and turns validation failures from the service into a model error.
    /// </summary>
    private async Task<bool> TryAsync(Func<Task> action, string errorKey)
    {
        try
        {
            await action();
            return true;
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(errorKey, ex.Message);
            return false;
        }
    }

    private RedirectResult RedirectWithMessage(string message, string action, string? fragment = null, object? routeValues = null)
    {
        TempData[MessageKey] = message;
        return RedirectTo(action, fragment, routeValues);
    }

    private RedirectResult RedirectTo(string action, string? fragment, object? routeValues = null)
    {
        var url = Url.Action(action, routeValues)!;
        return Redirect(fragment is null ? url : $"{url}#{fragment}");
    }
}
