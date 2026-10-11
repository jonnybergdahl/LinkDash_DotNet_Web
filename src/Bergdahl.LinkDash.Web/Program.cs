using System.Threading.RateLimiting;
using Bergdahl.LinkDash.Core.Configuration;
using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Core.Storage;
using Bergdahl.LinkDash.Web.Authentication;
using Bergdahl.LinkDash.Web.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

if (args is [HashPasswordCommand.Name])
{
    return HashPasswordCommand.Run();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services
    .AddOptions<LinkDashOptions>()
    .Bind(builder.Configuration.GetSection(LinkDashOptions.SectionName))
    .PostConfigure(options =>
    {
        // Relative paths in appsettings are relative to the app, not the process working directory.
        if (!string.IsNullOrWhiteSpace(options.DataFilePath) && !Path.IsPathRooted(options.DataFilePath))
        {
            options.DataFilePath = Path.Combine(builder.Environment.ContentRootPath, options.DataFilePath);
        }
    });

builder.Services.AddSingleton<ILinkRepository, JsonFileLinkRepository>();
builder.Services.AddSingleton<ILinkService, LinkService>();

builder.Services.AddOptions<AdminOptions>().Bind(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddSingleton<AdminAccountStore>();
builder.Services.AddSingleton<AdminCredentials>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "LinkDash.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            // Sign out sessions issued before the user name or password was changed.
            var credentials = context.HttpContext.RequestServices.GetRequiredService<AdminCredentials>();
            if (!credentials.IsCurrent(context.Principal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

// Keep the keys that protect the sign-in and antiforgery cookies next to the data file. Without this, IIS app pools
// that don't load a user profile get new keys on every restart, which signs everyone out.
builder.Services.AddDataProtection().SetApplicationName("LinkDash");
builder.Services
    .AddOptions<KeyManagementOptions>()
    .Configure<IOptions<LinkDashOptions>, ILoggerFactory>((options, linkDash, loggerFactory) =>
    {
        var keysDirectory = Path.Combine(Path.GetDirectoryName(linkDash.Value.DataFilePath)!, "keys");
        options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(keysDirectory), loggerFactory);
        if (OperatingSystem.IsWindows())
        {
            options.XmlEncryptor = new DpapiXmlEncryptor(protectToLocalMachine: true, loggerFactory);
        }
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AccountController.LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.OnRejected = (context, cancellationToken) =>
        new ValueTask(context.HttpContext.Response.WriteAsync("Too many sign-in attempts. Wait a minute and try again.", cancellationToken));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
return 0;
