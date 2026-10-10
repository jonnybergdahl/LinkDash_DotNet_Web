using System.Text.Json;
using Bergdahl.LinkDash.Core.Configuration;
using Bergdahl.LinkDash.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bergdahl.LinkDash.Core.Storage;

/// <summary>
/// Stores the link document in a single JSON file.
/// </summary>
public class JsonFileLinkRepository : ILinkRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<JsonFileLinkRepository> _logger;

    public JsonFileLinkRepository(IOptions<LinkDashOptions> options, ILogger<JsonFileLinkRepository> logger)
    {
        if (string.IsNullOrWhiteSpace(options.Value.DataFilePath))
        {
            throw new ArgumentException("LinkDash:DataFilePath must be configured.", nameof(options));
        }

        FilePath = Path.GetFullPath(options.Value.DataFilePath);
        _logger = logger;
    }

    public string FilePath { get; }

    public async Task<LinkData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            _logger.LogInformation("Data file {FilePath} does not exist, starting with no groups", FilePath);
            return new LinkData();
        }

        await using var stream = File.OpenRead(FilePath);
        if (stream.Length == 0)
        {
            return new LinkData();
        }

        try
        {
            var data = await JsonSerializer.DeserializeAsync<LinkData>(stream, SerializerOptions, cancellationToken);
            return Normalize(data);
        }
        catch (JsonException ex)
        {
            // Never fall back to an empty document here: the next save would overwrite the unreadable file.
            throw new InvalidDataException($"The data file '{FilePath}' does not contain valid LinkDash JSON.", ex);
        }
    }

    public async Task SaveAsync(LinkData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temporary file first so readers never see a half-written document.
        var tempPath = FilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, FilePath, overwrite: true);
    }

    /// <summary>
    /// Guards against hand-edited files with nulls where lists or names are expected.
    /// </summary>
    private static LinkData Normalize(LinkData? data)
    {
        data ??= new LinkData();
        data.Groups ??= [];
        data.Groups.RemoveAll(g => g is null);
        foreach (var group in data.Groups)
        {
            group.Name ??= string.Empty;
            group.Links ??= [];
            group.Links.RemoveAll(l => l is null);
        }

        return data;
    }
}
