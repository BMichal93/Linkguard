namespace LinkGuard.Tests;

public class OptionsTests
{
    [Fact]
    public void Parse_Defaults_AreApplied()
    {
        var options = Options.Parse(["--url", "https://example.com"]);

        Assert.Equal(20, options.TimeoutSeconds);
        Assert.Equal(8, options.MaxConcurrency);
        Assert.Empty(options.IgnorePatterns);
        Assert.Empty(options.Headers);
        Assert.Equal(OutputFormat.Console, options.Format);
        Assert.Null(options.JUnitPath);
        Assert.False(options.NoExternal);
    }

    [Fact]
    public void Parse_Url_IsParsedAsAbsoluteUri()
    {
        var options = Options.Parse(["--url", "https://staging.example.com"]);

        Assert.Equal(new Uri("https://staging.example.com"), options.BaseUrl);
    }

    [Fact]
    public void Parse_Timeout_ParsesGivenValue()
    {
        var options = Options.Parse(["--url", "https://example.com", "--timeout", "5"]);

        Assert.Equal(5, options.TimeoutSeconds);
    }

    [Fact]
    public void Parse_MaxConcurrency_ParsesGivenValue()
    {
        var options = Options.Parse(["--url", "https://example.com", "--max-concurrency", "3"]);

        Assert.Equal(3, options.MaxConcurrency);
    }

    [Fact]
    public void Parse_RepeatedIgnore_CollectsAll()
    {
        var options = Options.Parse(["--url", "https://example.com", "--ignore", "/blog/", "--ignore", "foo.*bar"]);

        Assert.Equal(["/blog/", "foo.*bar"], options.IgnorePatterns);
    }

    [Fact]
    public void Parse_RepeatedHeader_CollectsAll()
    {
        var options = Options.Parse([
            "--url", "https://example.com",
            "--header", "CF-IPCountry: PL",
            "--header", "X-Test: 1",
        ]);

        Assert.Equal(
            [new KeyValuePair<string, string>("CF-IPCountry", "PL"), new KeyValuePair<string, string>("X-Test", "1")],
            options.Headers);
    }

    [Fact]
    public void Parse_Format_ParsesJson()
    {
        var options = Options.Parse(["--url", "https://example.com", "--format", "json"]);

        Assert.Equal(OutputFormat.Json, options.Format);
    }

    [Fact]
    public void Parse_Junit_ParsesPath()
    {
        var options = Options.Parse(["--url", "https://example.com", "--junit", "out/results.xml"]);

        Assert.Equal("out/results.xml", options.JUnitPath);
    }

    [Fact]
    public void Parse_NoExternal_SetsFlag()
    {
        var options = Options.Parse(["--url", "https://example.com", "--no-external"]);

        Assert.True(options.NoExternal);
    }

    [Fact]
    public void Parse_MissingUrl_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse([]));
    }

    [Fact]
    public void Parse_UrlWithoutScheme_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url", "example.com"]));
    }

    [Fact]
    public void Parse_UrlWithNonHttpScheme_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url", "ftp://example.com"]));
    }

    [Fact]
    public void Parse_UnknownFlag_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url", "https://example.com", "--bogus"]));
    }

    [Fact]
    public void Parse_FlagMissingValue_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url"]));
    }

    [Fact]
    public void Parse_NonNumericTimeout_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() => Options.Parse(["--url", "https://example.com", "--timeout", "abc"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Parse_ZeroOrNegativeConcurrency_ReturnsUsageError(string value)
    {
        Assert.Throws<OptionsParseException>(() =>
            Options.Parse(["--url", "https://example.com", "--max-concurrency", value]));
    }

    [Fact]
    public void Parse_HeaderWithoutColon_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() =>
            Options.Parse(["--url", "https://example.com", "--header", "NoColonHere"]));
    }

    [Fact]
    public void Parse_UnknownFormat_ReturnsUsageError()
    {
        Assert.Throws<OptionsParseException>(() =>
            Options.Parse(["--url", "https://example.com", "--format", "xml"]));
    }
}
