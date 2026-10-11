using Bergdahl.LinkDash.Web.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bergdahl.LinkDash.Tests.Web;

/// <summary>
/// Hosts the web app in memory with its data file in a private temporary directory.
/// </summary>
/// <param name="configureAdmin">Whether to configure the administrator account; false leaves sign-in unset.</param>
public class LinkDashWebApplicationFactory(bool configureAdmin = true) : WebApplicationFactory<Program>
{
    public const string AdminUsername = "admin";
    public const string AdminPassword = "correct horse battery staple";

    // Hashing is deliberately slow, so share one hash between all tests.
    public static readonly string AdminPasswordHash = AdminCredentials.HashPassword(AdminPassword);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LinkDashTests", Guid.NewGuid().ToString("N"));

    public string DataFilePath => Path.Combine(_directory, "links.json");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("LinkDash:DataFilePath", DataFilePath);
        builder.UseSetting("LinkDash:Admin:Username", configureAdmin ? AdminUsername : "");
        builder.UseSetting("LinkDash:Admin:PasswordHash", configureAdmin ? AdminPasswordHash : "");
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
