using LinkGuard.Checking;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Checking;

public class LinkExtractorTests
{
    private static readonly Uri PageUrl = new("https://example.test/section/page");

    [Fact]
    public async Task Extract_RelativeHrefs_ResolvedAgainstPageUrl()
    {
        var links = await LinkExtractor.ExtractHrefsAsync("""<a href="/absolute">A</a><a href="relative">B</a>""", PageUrl);

        Assert.Equal(
            [new Uri("https://example.test/absolute"), new Uri("https://example.test/section/relative")],
            links);
    }

    [Fact]
    public async Task Extract_BaseTag_OverridesPageUrlForResolution()
    {
        var links = await LinkExtractor.ExtractHrefsAsync(
            """<html><head><base href="https://other-base.test/root/"></head><body><a href="child">C</a></body></html>""",
            PageUrl);

        Assert.Equal([new Uri("https://other-base.test/root/child")], links);
    }

    [Theory]
    [InlineData("""<a href="mailto:hello@example.test">Mail</a>""")]
    [InlineData("""<a href="tel:+11234567890">Tel</a>""")]
    [InlineData("""<a href="javascript:void(0)">JS</a>""")]
    public async Task Extract_NonHttpSchemes_Ignored(string html)
    {
        var links = await LinkExtractor.ExtractHrefsAsync(html, PageUrl);

        Assert.Empty(links);
    }

    [Fact]
    public async Task Extract_FragmentOnly_Ignored()
    {
        var links = await LinkExtractor.ExtractHrefsAsync("""<a href="#section">Jump</a>""", PageUrl);

        Assert.Empty(links);
    }

    [Fact]
    public async Task Extract_EmptyHref_Ignored()
    {
        var links = await LinkExtractor.ExtractHrefsAsync("""<a href="">Self</a>""", PageUrl);

        Assert.Empty(links);
    }

    [Fact]
    public async Task Extract_DuplicateLinks_ReturnedOnce()
    {
        var links = await LinkExtractor.ExtractHrefsAsync(
            """<a href="/a">One</a><a href="/a">Two</a><a href="/a">Three</a>""", PageUrl);

        Assert.Equal([new Uri("https://example.test/a")], links);
    }

    [Fact]
    public async Task Extract_MalformedHtml_StillReturnsValidLinks()
    {
        // The fixture has unclosed tags, an unterminated quote, and a stray quote in text -
        // AngleSharp recovers instead of throwing, and links outside the broken attribute
        // are still extracted (the one with the unterminated quote is genuinely unrecoverable).
        var html = Fixtures.Load("malformed-page.html");

        var links = await LinkExtractor.ExtractHrefsAsync(html, new Uri("https://example.test/"));

        Assert.Contains(new Uri("https://example.test/about"), links);
        Assert.Contains(new Uri("https://example.test/contact"), links);
        Assert.Contains(new Uri("https://example.test/team"), links);
    }

    [Fact]
    public async Task Extract_RelNofollow_StillChecked()
    {
        var links = await LinkExtractor.ExtractHrefsAsync("""<a href="/a" rel="nofollow">A</a>""", PageUrl);

        Assert.Equal([new Uri("https://example.test/a")], links);
    }

    [Fact]
    public async Task Extract_UmbracoPageFixture_FindsInternalAndExternalLinks()
    {
        var html = Fixtures.Load("umbraco-page.html");

        var links = await LinkExtractor.ExtractHrefsAsync(html, new Uri("https://example.test/about"));

        Assert.Contains(new Uri("https://example.test/products/widget-a"), links);
        Assert.Contains(new Uri("https://www.linkedin.com/company/example-brand"), links);
        Assert.DoesNotContain(links, l => l.Scheme is "mailto" or "tel");
    }
}
