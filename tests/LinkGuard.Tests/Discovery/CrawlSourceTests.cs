using System.Net;
using LinkGuard.Discovery;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Discovery;

public class CrawlSourceTests
{
    private static readonly Uri BaseUrl = new("https://example.com/");

    [Fact]
    public async Task Crawl_FollowsInternalLinksBreadthFirst()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/", StubHttpMessageHandler.Ok("""
                <html><body>
                  <a href="/a">A</a>
                  <a href="/b">B</a>
                  <a href="https://other.example.org/x">External</a>
                </body></html>
                """))
            .Map("https://example.com/a", StubHttpMessageHandler.Ok("""<html><body><a href="/c">C</a></body></html>"""))
            .Map("https://example.com/b", StubHttpMessageHandler.Ok("""<html><body><a href="/">Home again</a></body></html>"""))
            .Map("https://example.com/c", StubHttpMessageHandler.Ok("<html><body>leaf</body></html>"));
        var source = new CrawlSource(new HttpClient(handler));

        var urls = (await source.DiscoverAsync(BaseUrl)).Select(u => u.AbsolutePath).ToList();

        Assert.Equal(["/", "/a", "/b", "/c"], urls);
    }

    [Fact]
    public async Task Crawl_DoesNotRecurseIntoExternalHosts()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/", StubHttpMessageHandler.Ok("""<a href="https://other.example.org/x">External</a>"""));
        var source = new CrawlSource(new HttpClient(handler));

        await source.DiscoverAsync(BaseUrl);

        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.Host == "other.example.org");
    }

    [Fact]
    public async Task Crawl_CyclicLinks_TerminatesAndVisitsEachOnce()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/", StubHttpMessageHandler.Ok("""<html><body><a href="/a">A</a></body></html>"""))
            .Map("https://example.com/a", StubHttpMessageHandler.Ok("""<html><body><a href="/">Back to home</a></body></html>"""));
        var source = new CrawlSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, urls.Count);
        Assert.Equal(1, handler.Requests.Count(r => r.RequestUri!.AbsolutePath == "/"));
    }

    [Fact]
    public async Task Crawl_SkipsUrlsAlreadySeenInDifferentForm()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/", StubHttpMessageHandler.Ok("""<html><body><a href="/a">A</a><a href="/b">B</a></body></html>"""))
            .Map("https://example.com/a", StubHttpMessageHandler.Ok("""<html><body><a href="/shared">S</a></body></html>"""))
            .Map("https://example.com/b", StubHttpMessageHandler.Ok("""<html><body><a href="/shared">S</a></body></html>"""))
            .Map("https://example.com/shared", StubHttpMessageHandler.Ok("<html><body>shared leaf</body></html>"));
        var source = new CrawlSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal(1, urls.Count(u => u.AbsolutePath == "/shared"));
        Assert.Equal(1, handler.Requests.Count(r => r.RequestUri!.AbsolutePath == "/shared"));
    }

    [Fact]
    public async Task Crawl_NonHtmlResponse_SkipsWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/", StubHttpMessageHandler.Ok("""<html><body><a href="/report.pdf">PDF</a></body></html>"""))
            .Map("https://example.com/report.pdf", _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) { Headers = { ContentType = new("application/pdf") } },
            });
        var source = new CrawlSource(new HttpClient(handler));

        var urls = (await source.DiscoverAsync(BaseUrl)).Select(u => u.AbsolutePath).ToList();

        Assert.Equal(["/", "/report.pdf"], urls);
    }
}
