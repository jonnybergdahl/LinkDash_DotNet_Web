using Bergdahl.LinkDash.Core.Services;

namespace Bergdahl.LinkDash.Tests.Core;

public class LinkValidationTests
{
    [TestCase("https://example.com")]
    [TestCase("http://localhost:8080/path?q=1")]
    [TestCase("  https://example.com  ")]
    public void IsValidUrl_AcceptsHttpAndHttps(string url)
    {
        Assert.That(LinkValidation.IsValidUrl(url), Is.True);
    }

    [Test]
    public void IsValidUrl_IgnoresSurroundingWhitespaceForMaxLength()
    {
        var url = "https://example.com/" + new string('a', LinkValidation.MaxUrlLength - "https://example.com/".Length);

        Assert.That(LinkValidation.IsValidUrl($"  {url}\t\n"), Is.True);
        Assert.That(LinkValidation.IsValidUrl(url + "a"), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("example.com")]
    [TestCase("/relative/path")]
    [TestCase("javascript:alert(1)")]
    [TestCase("ftp://example.com")]
    [TestCase("file:///c:/windows")]
    public void IsValidUrl_RejectsEverythingElse(string? url)
    {
        Assert.That(LinkValidation.IsValidUrl(url), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("home")]
    [TestCase("mdi:home")]
    [TestCase("mdi:home-assistant")]
    [TestCase("numeric-1-box")]
    public void IsValidIcon_AcceptsMdiNamesAndEmpty(string? icon)
    {
        Assert.That(LinkValidation.IsValidIcon(icon), Is.True);
    }

    [TestCase("Home")]
    [TestCase("open_in_new")]
    [TestCase("mdi:")]
    [TestCase("mdi:-home")]
    [TestCase("mdi:home-")]
    [TestCase("fa:home")]
    [TestCase("<script>")]
    [TestCase("two words")]
    public void IsValidIcon_RejectsOtherText(string icon)
    {
        Assert.That(LinkValidation.IsValidIcon(icon), Is.False);
    }

    [TestCase(null, null)]
    [TestCase("  ", null)]
    [TestCase(" home ", "mdi:home")]
    [TestCase("mdi:home-assistant", "mdi:home-assistant")]
    public void NormalizeIcon_AddsPrefix(string? icon, string? expected)
    {
        Assert.That(LinkValidation.NormalizeIcon(icon), Is.EqualTo(expected));
    }

    [TestCase("mdi:email", "mdi-email")]
    [TestCase("github", "mdi-github")]
    [TestCase(null, "mdi-link")]
    [TestCase("not valid", "mdi-link")]
    public void IconCssClass_MapsToWebfontClass(string? icon, string expected)
    {
        Assert.That(LinkValidation.IconCssClass(icon), Is.EqualTo(expected));
    }
}
