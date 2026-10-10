using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bergdahl.LinkDash.Tests.Web;

/// <summary>
/// Hosts the web app in memory with its data file in a private temporary directory.
/// </summary>
public class LinkDashWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LinkDashTests", Guid.NewGuid().ToString("N"));

    public string DataFilePath => Path.Combine(_directory, "links.json");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("LinkDash:DataFilePath", DataFilePath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
