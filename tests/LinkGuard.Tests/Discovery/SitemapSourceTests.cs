using System.Net;
using LinkGuard.Discovery;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Discovery;

public class SitemapSourceTests
{
    private static readonly Uri BaseUrl = new("https://example.com");

    [Fact]
    public async Task Discover_RobotsWithSitemapDirective_UsesThatUrl()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap.xml"))
            .Map("https://example.com/sitemap.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/</loc></url>
                  <url><loc>https://example.com/about</loc></url>
                  <url><loc>https://other-brand.example.org/sibling</loc></url>
                </urlset>
                """));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal(
            [new Uri("https://example.com/"), new Uri("https://example.com/about")],
            urls);
    }

    [Fact]
    public async Task Discover_SitemapIndex_RecursesIntoChildren()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap_index.xml"))
            .Map("https://example.com/sitemap_index.xml", StubHttpMessageHandler.Xml("""
                <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <sitemap><loc>https://example.com/sitemap-pages.xml</loc></sitemap>
                  <sitemap><loc>https://example.com/sitemap-blog.xml</loc></sitemap>
                </sitemapindex>
                """))
            .Map("https://example.com/sitemap-pages.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/</loc></url>
                </urlset>
                """))
            .Map("https://example.com/sitemap-blog.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/blog/post-1</loc></url>
                </urlset>
                """));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal(
            [new Uri("https://example.com/"), new Uri("https://example.com/blog/post-1")],
            urls);
    }

    [Fact]
    public async Task Discover_SitemapIndexDeeperThanThree_StopsAtCap()
    {
        // Four nested indexes before any urlset: a.xml -> b.xml -> c.xml -> d.xml -> e.xml (urlset).
        // The depth cap (3) means the urlset at the 5th hop is never reached.
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/a.xml"))
            .Map("https://example.com/a.xml", IndexPointingTo("https://example.com/b.xml"))
            .Map("https://example.com/b.xml", IndexPointingTo("https://example.com/c.xml"))
            .Map("https://example.com/c.xml", IndexPointingTo("https://example.com/d.xml"))
            .Map("https://example.com/d.xml", IndexPointingTo("https://example.com/e.xml"))
            .Map("https://example.com/e.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/too-deep</loc></url>
                </urlset>
                """));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(urls);
    }

    [Fact]
    public async Task Discover_SelfReferencingIndex_TerminatesWithoutLoop()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/a.xml"))
            .Map("https://example.com/a.xml", IndexPointingTo("https://example.com/b.xml"))
            .Map("https://example.com/b.xml", IndexPointingTo("https://example.com/a.xml"));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(urls);
    }

    [Fact]
    public async Task Discover_MalformedXml_ReturnsEmptyAndDoesNotThrow()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap.xml"))
            .Map("https://example.com/sitemap.xml", StubHttpMessageHandler.Xml("<urlset><this is not valid xml"));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Empty(urls);
    }

    [Fact]
    public async Task Discover_MultipleSitemapDirectives_UnionsAll()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("""
                Sitemap: https://example.com/sitemap-a.xml
                Sitemap: https://example.com/sitemap-b.xml
                """))
            .Map("https://example.com/sitemap-a.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/a</loc></url>
                </urlset>
                """))
            .Map("https://example.com/sitemap-b.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/b</loc></url>
                </urlset>
                """));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal(
            [new Uri("https://example.com/a"), new Uri("https://example.com/b")],
            urls);
    }

    [Fact]
    public async Task Discover_EmptySitemap_ReturnsEmptySoCallerFallsBackToCrawl()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap.xml"))
            .Map("https://example.com/sitemap.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"></urlset>
                """));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Empty(urls);
    }

    [Fact]
    public async Task Discover_NoRobots_FallsBackToSitemapXml()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Status(HttpStatusCode.NotFound))
            .Map("https://example.com/sitemap.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/direct</loc></url>
                </urlset>
                """));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal([new Uri("https://example.com/direct")], urls);
    }

    [Fact]
    public async Task Discover_SitemapReturns500_FallsBackToCrawlYieldingNoSitemapUrls()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap.xml"))
            .Map("https://example.com/sitemap.xml", StubHttpMessageHandler.Status(HttpStatusCode.InternalServerError));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Empty(urls); // caller (Program.cs) falls back to CrawlSource when this is empty
    }

    [Fact]
    public async Task Discover_SitemapWithSiblingBrandUrls_FiltersToBaseHost()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/robots.txt", StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap.xml"))
            .Map("https://example.com/sitemap.xml", StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/own-page</loc></url>
                  <url><loc>https://sibling-brand.example.org/their-page</loc></url>
                </urlset>
                """));
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal([new Uri("https://example.com/own-page")], urls);
    }

    private static Func<int, HttpResponseMessage> IndexPointingTo(string childUrl) => StubHttpMessageHandler.Xml($"""
        <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
          <sitemap><loc>{childUrl}</loc></sitemap>
        </sitemapindex>
        """);
}
