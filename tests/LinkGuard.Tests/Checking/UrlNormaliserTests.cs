using LinkGuard.Checking;

namespace LinkGuard.Tests.Checking;

public class UrlNormaliserTests
{
    [Fact]
    public void NormalisedKey_lowercases_scheme_and_host()
    {
        var a = UrlNormaliser.NormalisedKey(new Uri("HTTPS://Example.COM/path"));
        var b = UrlNormaliser.NormalisedKey(new Uri("https://example.com/path"));

        Assert.Equal(b, a);
    }

    [Fact]
    public void NormalisedKey_strips_fragment_but_keeps_query()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.com/path?x=1#section"));

        Assert.Equal("https://example.com/path?x=1", key);
    }

    [Fact]
    public void NormalisedKey_collapses_duplicate_slashes()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.com/a//b///c"));

        Assert.Equal("https://example.com/a/b/c", key);
    }

    [Fact]
    public void NormalisedKey_treats_trailing_slash_as_equivalent()
    {
        var withSlash = UrlNormaliser.NormalisedKey(new Uri("https://example.com/about/"));
        var withoutSlash = UrlNormaliser.NormalisedKey(new Uri("https://example.com/about"));

        Assert.Equal(withoutSlash, withSlash);
    }

    [Fact]
    public void NormalisedKey_keeps_root_path_as_single_slash()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.com/"));

        Assert.Equal("https://example.com/", key);
    }

    [Fact]
    public void NormalisedKey_includes_non_default_port()
    {
        var key = UrlNormaliser.NormalisedKey(new Uri("https://example.com:8443/path"));

        Assert.Equal("https://example.com:8443/path", key);
    }
}
