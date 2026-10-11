using System.Net;
using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Services;
using Bergdahl.LinkDash.Web.Favicons;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bergdahl.LinkDash.Tests.Web;

public class FaviconTests
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Ico = [0x00, 0x00, 0x01, 0x00, 1, 2, 3];

    private LinkDashWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _app = null!;
    private FakeSites _sites = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new LinkDashWebApplicationFactory();
        _sites = new FakeSites();
        _app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient(FaviconService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _sites)));
        _client = _app.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _app.Dispose();
        _sites.Dispose();
        _factory.Dispose();
    }

    private async Task<Link> AddFaviconLinkAsync(string url)
    {
        var service = _app.Services.GetRequiredService<ILinkService>();
        var group = await service.AddGroupAsync("Home lab");
        return (await service.AddLinkAsync(group.Id, new LinkInput("Site", url, Icon: LinkValidation.FaviconIcon)))!;
    }

    [Test]
    public async Task ServesIconDeclaredInPage()
    {
        _sites.Add("http://router.lan/", "text/html", "<html><head><link rel=\"icon\" href=\"/static/logo.png\"></head></html>"u8.ToArray());
        _sites.Add("http://router.lan/static/logo.png", "application/octet-stream", Png);
        var link = await AddFaviconLinkAsync("http://router.lan/");

        var response = await _client.GetAsync($"/favicons/{link.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"), "The type comes from the bytes, not the header");
        Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.EqualTo(Png));
        Assert.That(response.Headers.GetValues("Content-Security-Policy").Single(), Does.Contain("sandbox"));
    }

    [Test]
    public async Task FallsBackToFaviconIcoAndCachesIt()
    {
        _sites.Add("https://nas.lan/", "text/html", "<html><head></head></html>"u8.ToArray());
        _sites.Add("https://nas.lan/favicon.ico", "image/x-icon", Ico);
        var link = await AddFaviconLinkAsync("https://nas.lan/");

        var first = await _client.GetByteArrayAsync($"/favicons/{link.Id}");
        var requests = _sites.RequestCount;
        var second = await _client.GetByteArrayAsync($"/favicons/{link.Id}");

        Assert.That(first, Is.EqualTo(Ico));
        Assert.That(second, Is.EqualTo(Ico));
        Assert.That(_sites.RequestCount, Is.EqualTo(requests), "The second request is served from the cache");
    }

    [Test]
    public async Task ReturnsNotFoundWhenSiteHasNoIcon()
    {
        _sites.Add("https://example.lan/", "text/html", "<html></html>"u8.ToArray());
        _sites.Add("https://example.lan/favicon.ico", "text/html", "<html>Not found</html>"u8.ToArray());
        var link = await AddFaviconLinkAsync("https://example.lan/");

        var response = await _client.GetAsync($"/favicons/{link.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "HTML is never served as an icon");
    }

    [Test]
    public async Task ReturnsNotFoundForUnknownLink()
    {
        var response = await _client.GetAsync($"/favicons/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(_sites.RequestCount, Is.Zero);
    }

    [Test]
    public async Task DashboardShowsFaviconImage()
    {
        var link = await AddFaviconLinkAsync("https://nas.lan/");

        var html = await _client.GetStringAsync("/");

        Assert.That(html, Does.Contain($"src=\"/favicons/{link.Id}?v={FaviconService.CacheKey(link.Url)}\""));
    }

    [Test]
    public void FindIconLinks_PrefersLargestIconsThenTouchIcons()
    {
        const string html = """
            <link rel="apple-touch-icon" href="/apple.png">
            <link rel="shortcut icon" href="/small.ico">
            <link href='/big.png' sizes="16x16 192x192" rel=icon>
            <link rel="mask-icon" href="/mask.svg">
            <link rel="icon" href="javascript:alert(1)">
            <link rel="stylesheet" href="/site.css">
            <link rel="icon" href="https://cdn.example.com/a.svg?x=1&amp;y=2" type="image/svg+xml">
            """;

        var icons = FaviconService.FindIconLinks(html, new Uri("https://example.com/app/"));

        Assert.That(icons.Select(uri => uri.ToString()), Is.EqualTo(new[]
        {
            "https://cdn.example.com/a.svg?x=1&y=2",
            "https://example.com/big.png",
            "https://example.com/small.ico",
            "https://example.com/apple.png"
        }));
    }

    [Test]
    public void DetectContentType_RecognizesImagesOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FaviconService.DetectContentType(Png), Is.EqualTo("image/png"));
            Assert.That(FaviconService.DetectContentType(Ico), Is.EqualTo("image/x-icon"));
            Assert.That(FaviconService.DetectContentType("GIF89a..."u8), Is.EqualTo("image/gif"));
            Assert.That(FaviconService.DetectContentType("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8), Is.EqualTo("image/svg+xml"));
            Assert.That(FaviconService.DetectContentType("<!DOCTYPE html><html></html>"u8), Is.Null);
            Assert.That(FaviconService.DetectContentType([]), Is.Null);
        });
    }

    /// <summary>
    /// Answers requests for the registered addresses, and 404 for everything else.
    /// </summary>
    private class FakeSites : HttpMessageHandler
    {
        private readonly Dictionary<string, (string ContentType, byte[] Body)> _responses = [];
        private int _requestCount;

        public int RequestCount => _requestCount;

        public void Add(string url, string contentType, byte[] body) => _responses[url] = (contentType, body);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            if (!_responses.TryGetValue(request.RequestUri!.ToString(), out var page))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
            }

            var content = new ByteArrayContent(page.Body);
            content.Headers.ContentType = new(page.ContentType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
        }
    }
}
