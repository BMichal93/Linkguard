using System.Net;
using LinkGuard.Checking;
using LinkGuard.Reporting;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Checking;

public class LinkCheckerDiscoveredLinksTests
{
    private static readonly Uri Url = new("https://example.com/");

    [Fact]
    public async Task CheckAsync_extracts_links_from_a_successful_internal_html_response()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Html("""
            <html><body>
              <a href="/about">About</a>
              <a href="https://other.example.org/x">External</a>
            </body></html>
            """));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(
            [new Uri("https://example.com/about"), new Uri("https://other.example.org/x")],
            result.DiscoveredLinks);
    }

    [Fact]
    public async Task CheckAsync_does_not_extract_links_for_external_checks()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Html("""<a href="/x">x</a>"""));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(new Uri("https://other.example.org/"), LinkKind.External);

        Assert.Empty(result.DiscoveredLinks);
    }

    [Fact]
    public async Task CheckAsync_does_not_extract_links_from_a_failed_internal_response()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Html("""<a href="/x">x</a>""", HttpStatusCode.NotFound));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Empty(result.DiscoveredLinks);
    }
}
