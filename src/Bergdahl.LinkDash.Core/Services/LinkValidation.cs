using System.Text.RegularExpressions;

namespace Bergdahl.LinkDash.Core.Services;

/// <summary>
/// Validation rules shared by the service and the web forms.
/// </summary>
public static partial class LinkValidation
{
    public const int MaxGroupNameLength = 100;
    public const int MaxTitleLength = 100;
    public const int MaxUrlLength = 2048;
    public const int MaxDescriptionLength = 300;
    public const int MaxIconLength = 64;

    /// <summary>
    /// Only absolute http(s) URLs are allowed, which also keeps "javascript:" links out.
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        var trimmed = url?.Trim();
        return !string.IsNullOrEmpty(trimmed)
            && trimmed.Length <= MaxUrlLength
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public const string IconPrefix = "mdi:";
    public const string DefaultIcon = "mdi:link";

    /// <summary>
    /// An empty glyph from the MDI font, used to preview "no icon".
    /// </summary>
    public const string BlankIcon = "mdi:blank";

    /// <summary>
    /// Icon value that shows the linked site's own favicon instead of an MDI icon. Only links can use it,
    /// since groups have no site to take it from.
    /// </summary>
    public const string FaviconIcon = "favicon";

    public static bool IsFavicon(string? icon) => string.Equals(icon?.Trim(), FaviconIcon, StringComparison.Ordinal);

    /// <summary>
    /// Material Design Icons names (https://pictogrammers.com/library/mdi/) written like in Home Assistant,
    /// e.g. "mdi:home" or "mdi:home-assistant". The "mdi:" prefix is optional. Empty means no icon.
    /// <see cref="FaviconIcon"/> is only accepted with <paramref name="allowFavicon"/>.
    /// </summary>
    public static bool IsValidIcon(string? icon, bool allowFavicon = false)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return true;
        }

        if (IsFavicon(icon))
        {
            return allowFavicon;
        }

        var trimmed = icon.Trim();
        return trimmed.Length <= MaxIconLength && IconRegex().IsMatch(trimmed);
    }

    /// <summary>
    /// Returns the icon in "mdi:name" form (or <see cref="FaviconIcon"/>), or null when empty.
    /// </summary>
    public static string? NormalizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        var trimmed = icon.Trim();
        if (trimmed == FaviconIcon)
        {
            return FaviconIcon;
        }

        return trimmed.StartsWith(IconPrefix, StringComparison.Ordinal) ? trimmed : IconPrefix + trimmed;
    }

    /// <summary>
    /// The CSS class of the MDI webfont for an icon, e.g. "mdi:home" becomes "mdi-home".
    /// Falls back to <paramref name="fallback"/> (the default link icon) when the icon is empty, invalid or
    /// <see cref="FaviconIcon"/>; the fallback is also what replaces a favicon that fails to load.
    /// </summary>
    public static string IconCssClass(string? icon, string fallback = DefaultIcon)
    {
        var normalized = IsValidIcon(icon) ? NormalizeIcon(icon) ?? fallback : fallback;
        return "mdi-" + normalized[IconPrefix.Length..];
    }

    [GeneratedRegex("^(mdi:)?[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IconRegex();
}
