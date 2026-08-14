using LinkGuard.Checking;

namespace LinkGuard.Tests.Checking;

public class UrlNormaliserTests
{
    [Fact]
    public void NormalisedKey_LowercasesSchemeAndHost()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("HTTPS://Example.test/A"));

        Assert.Equal("https://example.test/A", key);
    }

    [Fact]
    public void NormalisedKey_PreservesPathCase()
    {
        // Umbraco Cloud sits behind a case-sensitive origin for some assets, so folding
        // path case would hide real 404s.
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test/About"));

        Assert.Equal("https://example.test/About", key);
    }

    [Fact]
    public void NormalisedKey_StripsFragment()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test/a#top"));

        Assert.Equal("https://example.test/a", key);
    }

    [Fact]
    public void NormalisedKey_KeepsQuery()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test/a?b=1"));

        Assert.Equal("https://example.test/a?b=1", key);
    }

    [Fact]
    public void NormalisedKey_QueryOrderIsSignificant()
    {
        var first = UrlNormaliser.NormalisedKey(new Uri("https://example.test/a?b=1&c=2"));
        var second = UrlNormaliser.NormalisedKey(new Uri("https://example.test/a?c=2&b=1"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NormalisedKey_TrailingSlashIsEquivalentToNone()
    {
        var withSlash = UrlNormaliser.NormalisedKey(new Uri("https://example.test/a/"));
        var withoutSlash = UrlNormaliser.NormalisedKey(new Uri("https://example.test/a"));

        Assert.Equal(withoutSlash, withSlash);
    }

    [Fact]
    public void NormalisedKey_RootSlashIsPreserved()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test"));

        Assert.Equal("https://example.test/", key);
    }

    [Fact]
    public void NormalisedKey_CollapsesDuplicateSlashes()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test/a//b"));

        Assert.Equal("https://example.test/a/b", key);
    }

    [Fact]
    public void NormalisedKey_ResolvesRelativeAgainstBase()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test/x/y/"), "../b");

        Assert.Equal("https://example.test/x/b", key);
    }

    [Fact]
    public void NormalisedKey_DropsDefaultPort()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test:443/a"));

        Assert.Equal("https://example.test/a", key);
    }

    [Fact]
    public void NormalisedKey_KeepsNonDefaultPort()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test:8080/a"));

        Assert.Equal("https://example.test:8080/a", key);
    }

    [Fact]
    public void NormalisedKey_NormalisesPercentEncoding()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.test/%7Euser"));

        Assert.Equal("https://example.test/~user", key);
    }

    [Theory]
    [InlineData("mailto:hello@example.test")]
    [InlineData("tel:+11234567890")]
    [InlineData("javascript:void(0)")]
    public void IsCheckable_RejectsNonHttpSchemes(string url)
    {
        Assert.False(UrlNormaliser.IsCheckable(new Uri(url)));
    }

    [Theory]
    [InlineData("http://example.test/a")]
    [InlineData("https://example.test/a")]
    public void IsCheckable_AcceptsHttpAndHttps(string url)
    {
        Assert.True(UrlNormaliser.IsCheckable(new Uri(url)));
    }
}
