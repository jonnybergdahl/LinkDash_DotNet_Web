using System.Security.Claims;
using Bergdahl.LinkDash.Web.Authentication;
using Bergdahl.LinkDash.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bergdahl.LinkDash.Web.Controllers;

/// <summary>
/// Sign-in and sign-out for the administrator, and creating that account on first sign-in.
/// </summary>
[AllowAnonymous]
public class AccountController(AdminCredentials credentials) : Controller
{
    /// <summary>
    /// Rate limiting policy that slows down password guessing.
    /// </summary>
    public const string LoginRateLimitPolicy = "login";

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        if (!credentials.HasAccount)
        {
            return RedirectToAction(nameof(CreateAccount), new { returnUrl });
        }

        return View(new LoginFormModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    public async Task<IActionResult> Login(LoginFormModel model)
    {
        if (!credentials.HasAccount)
        {
            return RedirectToAction(nameof(CreateAccount), new { model.ReturnUrl });
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var principal = credentials.Authenticate(model.Username, model.Password);
        if (principal is null)
        {
            ModelState.AddModelError(string.Empty, "Wrong user name or password.");
            return View(model);
        }

        return await SignInAsync(principal, model.RememberMe, model.ReturnUrl);
    }

    /// <summary>
    /// Shown instead of the login page until the administrator account exists.
    /// </summary>
    [HttpGet]
    public IActionResult CreateAccount(string? returnUrl)
    {
        if (credentials.HasAccount)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        return View(new CreateAccountFormModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    public async Task<IActionResult> CreateAccount(CreateAccountFormModel model)
    {
        if (credentials.HasAccount)
        {
            return RedirectToAction(nameof(Login), new { model.ReturnUrl });
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Null when someone else created the account a moment earlier.
        var principal = credentials.CreateAccount(model.Username, model.Password);
        if (principal is null)
        {
            return RedirectToAction(nameof(Login), new { model.ReturnUrl });
        }

        TempData[SettingsController.MessageKey] = $"Account \"{model.Username.Trim()}\" created.";
        return await SignInAsync(principal, isPersistent: false, model.ReturnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task<IActionResult> SignInAsync(ClaimsPrincipal principal, bool isPersistent, string? returnUrl)
    {
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = isPersistent });
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Settings")!;
}
