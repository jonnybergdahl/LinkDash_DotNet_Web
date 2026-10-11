using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bergdahl.LinkDash.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Bergdahl.LinkDash.Web.Favicons;

/// <summary>
/// Downloads the favicon of a linked site and caches it in a "favicons" folder next to the data file.
/// The server does the fetching so the browser never loads images from the linked sites directly, which would be
/// blocked for http sites when the dashboard itself runs on https. Register as a singleton.
/// </summary>
public partial class FaviconService(IHttpClientFactory httpClientFactory, IOptions<LinkDashOptions> options, ILogger<FaviconService> logger)
{
    public const string HttpClientName = "Favicons";

    private const int MaxPageBytes = 512 * 1024;
    private const int MaxImageBytes = 256 * 1024;
    private static readonly TimeSpan RefreshAfter = TimeSpan.FromDays(7);
    private static readonly TimeSpan RetryFailedAfter = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Lazy<Task<FaviconImage?>>> _downloads = new();

    private string CacheDirectory => Path.Combine(Path.GetDirectoryName(options.Value.DataFilePath)!, "favicons");

    /// <summary>
    /// Short hash of a link URL. Names the cache files, and versions the image URL so browsers reload the icon when the link URL changes.
    /// </summary>
    public static string CacheKey(string url) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];

    /// <summary>
    /// Returns the favicon of the site at <paramref name="url"/>, or null when the site has none that can be loaded.
    /// Cached icons are refreshed after a week; failed downloads are retried after an hour.
    /// </summary>
    public async Task<FaviconImage?> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(url);
        var imagePath = Path.Combine(CacheDirectory, key + ".img");
        var failedPath = Path.Combine(CacheDirectory, key + ".failed");

        var cached = await ReadCachedAsync(imagePath, cancellationToken);
        if ((cached is not null && IsNewerThan(imagePath, RefreshAfter)) || IsNewerThan(failedPath, RetryFailedAfter))
        {
            return cached;
        }

        // Several browsers may ask for the same icon at once, so download it only once. The download is not tied
        // to one request's cancellation, since others may be waiting for it; the HTTP client timeout bounds it.
        var download = _downloads.GetOrAdd(key, _ => new Lazy<Task<FaviconImage?>>(() => DownloadAndStoreAsync(url, imagePath, failedPath)));
        try
        {
            // Keep serving the old icon when a refresh fails.
            return await download.Value.WaitAsync(cancellationToken) ?? cached;
        }
        finally
        {
            _downloads.TryRemove(KeyValuePair.Create(key, download));
        }
    }

    private async Task<FaviconImage?> DownloadAndStoreAsync(string url, string imagePath, string failedPath)
    {
        var image = await DownloadAsync(url);
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            if (image is null)
            {
                await File.WriteAllBytesAsync(failedPath, []);
                return null;
            }

            // Write to a temporary file first so a concurrent read never sees a half-written icon.
            var temporaryPath = imagePath + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, image.Data);
            File.Move(temporaryPath, imagePath, overwrite: true);
            File.Delete(failedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not cache the favicon for {Url}", url);
        }

        return image;
    }

    private async Task<FaviconImage?> DownloadAsync(string url)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var pageUri = new Uri(url);
        var candidates = new List<Uri>();

        try
        {
            using var response = await client.GetAsync(pageUri, HttpCompletionOption.ResponseHeadersRead);
            // Resolve relative icon paths and /favicon.ico against the address the site redirected to.
            pageUri = response.RequestMessage?.RequestUri ?? pageUri;
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType is "text/html" or "application/xhtml+xml")
            {
                // The icon links are in the head, so a truncated page is fine.
                var html = Encoding.UTF8.GetString(await ReadAtMostAsync(response.Content, MaxPageBytes));
                candidates.AddRange(FindIconLinks(html, pageUri));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogInformation("Could not load {Url} to look for its favicon: {Message}", url, ex.Message);
        }

        candidates.Add(new Uri(pageUri, "/favicon.ico"));

        foreach (var candidate in candidates.Distinct())
        {
            if (await TryDownloadImageAsync(client, candidate) is { } image)
            {
                return image;
            }
        }

        logger.LogInformation("No favicon found for {Url}", url);
        return null;
    }

    private async Task<FaviconImage?> TryDownloadImageAsync(HttpClient client, Uri uri)
    {
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxImageBytes)
            {
                return null;
            }

            var data = await ReadAtMostAsync(response.Content, MaxImageBytes + 1);
            if (data.Length is 0 or > MaxImageBytes)
            {
                return null;
            }

            // Servers often send icons with a generic content type, so trust the bytes rather than the header.
            return DetectContentType(data) is { } contentType ? new FaviconImage(data, contentType) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug("Could not download favicon {Uri}: {Message}", uri, ex.Message);
            return null;
        }
    }

    private static async Task<FaviconImage?> ReadCachedAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var data = await File.ReadAllBytesAsync(path, cancellationToken);
            return DetectContentType(data) is { } contentType ? new FaviconImage(data, contentType) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsNewerThan(string path, TimeSpan age) =>
        File.Exists(path) && File.GetLastWriteTimeUtc(path) > DateTime.UtcNow - age;

    private static async Task<byte[]> ReadAtMostAsync(HttpContent content, int maxBytes)
    {
        await using var stream = await content.ReadAsStreamAsync();
        var buffer = new byte[maxBytes];
        var length = 0;
        int read;
        while (length < maxBytes && (read = await stream.ReadAsync(buffer.AsMemory(length))) > 0)
        {
            length += read;
        }

        return buffer[..length];
    }

    /// <summary>
    /// The icons declared with &lt;link rel="icon"&gt; in a page, largest first, followed by any apple-touch-icon.
    /// </summary>
    public static IReadOnlyList<Uri> FindIconLinks(string html, Uri baseUri)
    {
        var icons = new List<(Uri Uri, int Size)>();
        var touchIcons = new List<Uri>();

        foreach (Match tag in LinkTagRegex().Matches(html))
        {
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match attribute in AttributeRegex().Matches(tag.Value))
            {
                // Like browsers, the first of repeated attributes wins.
                attributes.TryAdd(attribute.Groups["name"].Value.ToLowerInvariant(), WebUtility.HtmlDecode(attribute.Groups["value"].Value).Trim());
            }

            if (!attributes.TryGetValue("rel", out var rel)
                || !attributes.TryGetValue("href", out var href)
                || !Uri.TryCreate(baseUri, href, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            var rels = rel.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (rels.Contains("icon"))
            {
                icons.Add((uri, IconSize(attributes)));
            }
            else if (rels.Contains("apple-touch-icon") || rels.Contains("apple-touch-icon-precomposed"))
            {
                touchIcons.Add(uri);
            }
        }

        return [.. icons.OrderByDescending(icon => icon.Size).Select(icon => icon.Uri), .. touchIcons];
    }

    /// <summary>
    /// The largest size in the "sizes" attribute; vector icons ("any" or SVG) rank above all others.
    /// </summary>
    private static int IconSize(Dictionary<string, string> attributes)
    {
        var sizes = attributes.GetValueOrDefault("sizes", "").ToLowerInvariant();
        if (sizes.Contains("any") || attributes.GetValueOrDefault("type", "").Contains("svg", StringComparison.OrdinalIgnoreCase))
        {
            return int.MaxValue;
        }

        return sizes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(size => int.TryParse(size.Split('x')[0], out var width) ? width : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// The content type of an image from its first bytes, or null when it is not an image format browsers show as an icon.
    /// Checking the bytes also keeps the endpoint from ever serving HTML from another site.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((byte[])[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (data.StartsWith((byte[])[0x00, 0x00, 0x01, 0x00]))
        {
            return "image/x-icon";
        }

        if (data.StartsWith("GIF8"u8))
        {
            return "image/gif";
        }

        if (data.StartsWith((byte[])[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        var text = Encoding.UTF8.GetString(data[..Math.Min(data.Length, 1024)]).TrimStart('﻿', ' ', '\t', '\r', '\n');
        if ((text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            && text.Contains("<svg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/svg+xml";
        }

        return null;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTagRegex();

    [GeneratedRegex(@"(?<name>[a-zA-Z][\w-]*)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s""'>]+))")]
    private static partial Regex AttributeRegex();
}

public record FaviconImage(byte[] Data, string ContentType);
