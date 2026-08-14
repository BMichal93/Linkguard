using LinkGuard.Discovery;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Discovery;

public class CrawlSourceTests
{
    private static readonly Uri BaseUrl = new("https://example.com/");

    [Fact]
    public async Task DiscoverAsync_follows_internal_links_breadth_first_and_ignores_external_ones()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/" => StubHttpMessageHandler.Html("""
                <html><body>
                  <a href="/a">A</a>
                  <a href="/b">B</a>
                  <a href="https://other.example.org/x">External</a>
                </body></html>
                """),
            "/a" => StubHttpMessageHandler.Html("""<html><body><a href="/c">C</a></body></html>"""),
            "/b" => StubHttpMessageHandler.Html("""<html><body><a href="/">Home again</a></body></html>"""),
            "/c" => StubHttpMessageHandler.Html("<html><body>leaf</body></html>"),
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new CrawlSource(new HttpClient(handler));

        var urls = (await source.DiscoverAsync(BaseUrl)).Select(u => u.AbsolutePath).ToList();

        Assert.Equal(["/", "/a", "/b", "/c"], urls);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.Host == "other.example.org");
    }

    [Fact]
    public async Task DiscoverAsync_does_not_revisit_a_page_reached_by_two_paths()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/" => StubHttpMessageHandler.Html("""<html><body><a href="/a">A</a><a href="/b">B</a></body></html>"""),
            "/a" => StubHttpMessageHandler.Html("""<html><body><a href="/shared">S</a></body></html>"""),
            "/b" => StubHttpMessageHandler.Html("""<html><body><a href="/shared">S</a></body></html>"""),
            "/shared" => StubHttpMessageHandler.Html("<html><body>shared leaf</body></html>"),
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new CrawlSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal(1, urls.Count(u => u.AbsolutePath == "/shared"));
        Assert.Equal(1, handler.Requests.Count(r => r.RequestUri!.AbsolutePath == "/shared"));
    }

    [Fact]
    public async Task DiscoverAsync_skips_non_html_responses_without_throwing()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/" => StubHttpMessageHandler.Html("""<html><body><a href="/report.pdf">PDF</a></body></html>"""),
            "/report.pdf" => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) { Headers = { ContentType = new("application/pdf") } },
            },
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new CrawlSource(new HttpClient(handler));

        var urls = (await source.DiscoverAsync(BaseUrl)).Select(u => u.AbsolutePath).ToList();

        Assert.Equal(["/", "/report.pdf"], urls);
    }
}
