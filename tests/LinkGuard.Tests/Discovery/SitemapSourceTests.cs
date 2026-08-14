using LinkGuard.Discovery;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Discovery;

public class SitemapSourceTests
{
    private static readonly Uri BaseUrl = new("https://example.com");

    [Fact]
    public async Task DiscoverAsync_reads_flat_sitemap_referenced_by_robots_txt()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => StubHttpMessageHandler.Text("User-agent: *\nSitemap: https://example.com/sitemap.xml\n"),
            "/sitemap.xml" => StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/</loc></url>
                  <url><loc>https://example.com/about</loc></url>
                  <url><loc>https://other-brand.example.org/sibling</loc></url>
                </urlset>
                """),
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal(
            [new Uri("https://example.com/"), new Uri("https://example.com/about")],
            urls);
    }

    [Fact]
    public async Task DiscoverAsync_recurses_into_sitemap_index()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap_index.xml"),
            "/sitemap_index.xml" => StubHttpMessageHandler.Xml("""
                <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <sitemap><loc>https://example.com/sitemap-pages.xml</loc></sitemap>
                  <sitemap><loc>https://example.com/sitemap-blog.xml</loc></sitemap>
                </sitemapindex>
                """),
            "/sitemap-pages.xml" => StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/</loc></url>
                </urlset>
                """),
            "/sitemap-blog.xml" => StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/blog/post-1</loc></url>
                </urlset>
                """),
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal(
            [new Uri("https://example.com/"), new Uri("https://example.com/blog/post-1")],
            urls);
    }

    [Fact]
    public async Task DiscoverAsync_guards_against_a_cyclic_sitemap_index()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => StubHttpMessageHandler.Text("Sitemap: https://example.com/a.xml"),
            "/a.xml" => StubHttpMessageHandler.Xml("""
                <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <sitemap><loc>https://example.com/b.xml</loc></sitemap>
                </sitemapindex>
                """),
            "/b.xml" => StubHttpMessageHandler.Xml("""
                <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <sitemap><loc>https://example.com/a.xml</loc></sitemap>
                </sitemapindex>
                """),
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(urls);
    }

    [Fact]
    public async Task DiscoverAsync_returns_empty_for_malformed_sitemap_without_throwing()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => StubHttpMessageHandler.Text("Sitemap: https://example.com/sitemap.xml"),
            "/sitemap.xml" => StubHttpMessageHandler.Xml("<urlset><this is not valid xml"),
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Empty(urls);
    }

    [Fact]
    public async Task DiscoverAsync_falls_back_to_sitemap_xml_when_robots_txt_has_no_directive()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
            "/sitemap.xml" => StubHttpMessageHandler.Xml("""
                <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                  <url><loc>https://example.com/direct</loc></url>
                </urlset>
                """),
            _ => StubHttpMessageHandler.Status(System.Net.HttpStatusCode.NotFound),
        });
        var source = new SitemapSource(new HttpClient(handler));

        var urls = await source.DiscoverAsync(BaseUrl);

        Assert.Equal([new Uri("https://example.com/direct")], urls);
    }
}
