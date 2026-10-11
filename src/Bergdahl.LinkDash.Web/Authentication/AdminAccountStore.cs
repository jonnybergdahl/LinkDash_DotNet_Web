using System.Text.Json;
using Bergdahl.LinkDash.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Bergdahl.LinkDash.Web.Authentication;

/// <summary>
/// Keeps the administrator account created on first sign-in in "admin.json" next to the data file,
/// the one folder the app is allowed to write to.
/// </summary>
public class AdminAccountStore(IOptions<LinkDashOptions> options, ILogger<AdminAccountStore> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly Lock _lock = new();
    private AdminOptions? _cached;
    private DateTime _cachedWriteTime;

    public string FilePath { get; } = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.Value.DataFilePath))!, "admin.json");

    /// <summary>
    /// The stored account, or null when none has been created. Re-reads the file when it changes,
    /// so deleting it brings back the create account page.
    /// </summary>
    public AdminOptions? Load()
    {
        lock (_lock)
        {
            if (!File.Exists(FilePath))
            {
                _cached = null;
                return null;
            }

            var writeTime = File.GetLastWriteTimeUtc(FilePath);
            if (_cached is null || writeTime != _cachedWriteTime)
            {
                _cached = JsonSerializer.Deserialize<AdminOptions>(File.ReadAllText(FilePath), SerializerOptions);
                _cachedWriteTime = writeTime;
            }

            return _cached;
        }
    }

    /// <summary>
    /// Saves the account unless one already exists. Returns false when another request created it first.
    /// </summary>
    public bool TryCreate(AdminOptions account)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(account, SerializerOptions));
            try
            {
                // Moving without overwrite fails if the file exists, so an existing account is never replaced.
                File.Move(tempPath, FilePath, overwrite: false);
            }
            catch (IOException) when (File.Exists(FilePath))
            {
                File.Delete(tempPath);
                return false;
            }

            logger.LogInformation("Created administrator account {Username} in {FilePath}", account.Username, FilePath);
            return true;
        }
    }
}
