using LinkGuard;

namespace LinkGuard.Tests;

public class OptionsTests
{
    [Fact]
    public void Parse_applies_defaults_when_only_url_given()
    {
        var options = Options.Parse(["--url", "https://example.com"]);

        Assert.Equal(new Uri("https://example.com"), options.BaseUrl);
        Assert.Equal(20, options.TimeoutSeconds);
        Assert.Equal(8, options.MaxConcurrency);
        Assert.Empty(options.IgnorePatterns);
        Assert.Empty(options.Headers);
        Assert.Equal(OutputFormat.Console, options.Format);
        Assert.Null(options.JUnitPath);
        Assert.False(options.NoExternal);
    }

    [Fact]
    public void Parse_reads_all_flags()
    {
        var options = Options.Parse([
            "--url", "https://staging.example.com",
            "--timeout", "5",
            "--max-concurrency", "3",
            "--ignore", "/blog/",
            "--ignore", "foo.*bar",
            "--header", "CF-IPCountry: PL",
            "--format", "json",
            "--junit", "out/results.xml",
            "--no-external",
        ]);

        Assert.Equal(new Uri("https://staging.example.com"), options.BaseUrl);
        Assert.Equal(5, options.TimeoutSeconds);
        Assert.Equal(3, options.MaxConcurrency);
        Assert.Equal(["/blog/", "foo.*bar"], options.IgnorePatterns);
        Assert.Equal(new KeyValuePair<string, string>("CF-IPCountry", "PL"), Assert.Single(options.Headers));
        Assert.Equal(OutputFormat.Json, options.Format);
        Assert.Equal("out/results.xml", options.JUnitPath);
        Assert.True(options.NoExternal);
    }

    [Fact]
    public void Parse_throws_when_url_missing()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse([]));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    public void Parse_throws_when_url_invalid(string value)
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url", value]));
    }

    [Fact]
    public void Parse_throws_on_unknown_argument()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url", "https://example.com", "--bogus"]));
    }

    [Fact]
    public void Parse_throws_when_flag_value_missing()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void Parse_throws_on_invalid_timeout(string value)
    {
        Assert.Throws<OptionsParseException>(() =>
            Options.Parse(["--url", "https://example.com", "--timeout", value]));
    }

    [Fact]
    public void Parse_throws_on_invalid_format()
    {
        Assert.Throws<OptionsParseException>(() =>
            Options.Parse(["--url", "https://example.com", "--format", "xml"]));
    }

    [Fact]
    public void Parse_throws_on_malformed_header()
    {
        Assert.Throws<OptionsParseException>(() =>
            Options.Parse(["--url", "https://example.com", "--header", "NoColonHere"]));
    }
}
