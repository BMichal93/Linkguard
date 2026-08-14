using LinkGuard.Discovery;

namespace LinkGuard.Tests.TestSupport;

public class SiteBuilderTests
{
    [Fact]
    public async Task Build_SiteWithRobotsSitemapAndPages_IsDiscoverableAndCheckable()
    {
        var site = new SiteBuilder("https://example.test")
            .WithRobots("Sitemap: https://example.test/sitemap.xml")
            .WithSitemap("/", "/about", "/contact")
            .WithPage("/about", links: ["/team", "https://external.test/x"])
            .WithStatus("/team", 404)
            .Build();

        var urls = await new SitemapSource(site.HttpClient).DiscoverAsync(site.BaseUrl);

        Assert.Equal(
            [new Uri("https://example.test/"), new Uri("https://example.test/about"), new Uri("https://example.test/contact")],
            urls);
    }
}
