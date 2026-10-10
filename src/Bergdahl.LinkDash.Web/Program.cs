using Bergdahl.LinkDash.Core.Configuration;
using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Core.Storage;
using Microsoft.AspNetCore.Mvc;

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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
