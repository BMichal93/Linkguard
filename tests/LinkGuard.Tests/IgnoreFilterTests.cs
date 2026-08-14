namespace LinkGuard.Tests;

public class IgnoreFilterTests
{
    [Fact]
    public void Apply_returns_all_urls_when_no_patterns_given()
    {
        Uri[] urls = [new("https://example.com/a"), new("https://example.com/b")];

        var result = IgnoreFilter.Apply(urls, []);

        Assert.Equal(urls, result);
    }

    [Fact]
    public void Apply_filters_by_plain_substring()
    {
        Uri[] urls = [new("https://example.com/blog/post"), new("https://example.com/about")];

        var result = IgnoreFilter.Apply(urls, ["/blog/"]);

        Assert.Equal([new Uri("https://example.com/about")], result);
    }

    [Fact]
    public void Apply_filters_by_regex()
    {
        Uri[] urls = [new("https://example.com/page-1"), new("https://example.com/page-2"), new("https://example.com/about")];

        var result = IgnoreFilter.Apply(urls, [@"page-\d+"]);

        Assert.Equal([new Uri("https://example.com/about")], result);
    }

    [Fact]
    public void Apply_treats_an_invalid_regex_as_a_plain_substring_that_simply_does_not_match()
    {
        Uri[] urls = [new("https://example.com/about")];

        var result = IgnoreFilter.Apply(urls, ["[unterminated"]);

        Assert.Equal(urls, result);
    }
}
