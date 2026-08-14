using System.Net;
using LinkGuard.Checking;
using LinkGuard.Reporting;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Checking;

public class LinkCheckerDiscoveredLinksTests
{
    private static readonly Uri Url = new("https://example.com/");

    [Fact]
    public async Task Check_SuccessfulInternalHtmlResponse_ExtractsLinks()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.Ok("""
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
    public async Task Check_FailedInternalResponse_DoesNotExtractLinks()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""<a href="/x">x</a>""", System.Text.Encoding.UTF8, "text/html"),
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Empty(result.DiscoveredLinks);
    }

    [Fact]
    public async Task Check_NonHtmlContentType_DoesNotExtractLinks()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""<a href="/x">x</a>""", System.Text.Encoding.UTF8, "application/pdf"),
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Empty(result.DiscoveredLinks);
    }
}
