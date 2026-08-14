namespace LinkGuard.Tests.TestSupport;

public class FixturesTests
{
    [Theory]
    [InlineData("flat-sitemap.xml")]
    [InlineData("sitemap-index.xml")]
    [InlineData("truncated-sitemap.xml")]
    [InlineData("umbraco-page.html")]
    [InlineData("malformed-page.html")]
    public void Load_KnownFixture_ReturnsNonEmptyContent(string name)
    {
        var content = Fixtures.Load(name);

        Assert.False(string.IsNullOrWhiteSpace(content));
    }
}
