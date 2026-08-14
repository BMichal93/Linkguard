using LinkGuard.Reporting;

namespace LinkGuard.Tests.Reporting;

public class ClassificationTests
{
    private static readonly Uri Url = new("https://example.com/page");

    private static CheckResult Result(LinkKind kind, int? statusCode = null, string? error = null, IReadOnlyList<RedirectHop>? chain = null) =>
        new()
        {
            Url = Url,
            Kind = kind,
            StatusCode = statusCode,
            Error = error,
            RedirectChain = chain ?? [],
        };

    [Theory]
    [InlineData(LinkKind.Internal)]
    [InlineData(LinkKind.External)]
    public void Outcome_is_pass_for_2xx(LinkKind kind)
    {
        Assert.Equal(CheckOutcome.Pass, Result(kind, statusCode: 200).Outcome);
    }

    [Theory]
    [InlineData(LinkKind.Internal)]
    [InlineData(LinkKind.External)]
    public void Outcome_is_pass_for_a_redirect_chain_resolving_to_2xx(LinkKind kind)
    {
        var chain = new[] { new RedirectHop(Url, 301) };
        Assert.Equal(CheckOutcome.Pass, Result(kind, statusCode: 200, chain: chain).Outcome);
    }

    [Fact]
    public void Outcome_is_fail_for_internal_4xx()
    {
        Assert.Equal(CheckOutcome.Fail, Result(LinkKind.Internal, statusCode: 404).Outcome);
    }

    [Fact]
    public void Outcome_is_warn_for_external_4xx()
    {
        Assert.Equal(CheckOutcome.Warn, Result(LinkKind.External, statusCode: 404).Outcome);
    }

    [Fact]
    public void Outcome_is_fail_for_internal_5xx()
    {
        Assert.Equal(CheckOutcome.Fail, Result(LinkKind.Internal, statusCode: 500).Outcome);
    }

    [Fact]
    public void Outcome_is_warn_for_external_5xx()
    {
        Assert.Equal(CheckOutcome.Warn, Result(LinkKind.External, statusCode: 500).Outcome);
    }

    [Fact]
    public void Outcome_is_fail_for_a_redirect_chain_resolving_to_4xx_internally()
    {
        var chain = new[] { new RedirectHop(Url, 301) };
        Assert.Equal(CheckOutcome.Fail, Result(LinkKind.Internal, statusCode: 404, chain: chain).Outcome);
    }

    [Fact]
    public void Outcome_is_warn_for_a_redirect_chain_resolving_to_5xx_externally()
    {
        var chain = new[] { new RedirectHop(Url, 302) };
        Assert.Equal(CheckOutcome.Warn, Result(LinkKind.External, statusCode: 500, chain: chain).Outcome);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("dns failure")]
    [InlineData("tls failure")]
    [InlineData("redirect loop")]
    [InlineData("too many redirects")]
    public void Outcome_is_fail_for_internal_errors(string error)
    {
        Assert.Equal(CheckOutcome.Fail, Result(LinkKind.Internal, error: error).Outcome);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("dns failure")]
    [InlineData("tls failure")]
    public void Outcome_is_warn_for_external_errors(string error)
    {
        Assert.Equal(CheckOutcome.Warn, Result(LinkKind.External, error: error).Outcome);
    }

    [Fact]
    public void ExitCode_is_clean_when_nothing_failed()
    {
        var results = new[]
        {
            Result(LinkKind.Internal, statusCode: 200),
            Result(LinkKind.External, statusCode: 404), // warns, does not fail the build
        };

        Assert.Equal(ExitCodes.Clean, ExitCodes.Compute(results));
    }

    [Fact]
    public void ExitCode_is_internal_failures_when_an_internal_link_fails()
    {
        var results = new[]
        {
            Result(LinkKind.Internal, statusCode: 200),
            Result(LinkKind.Internal, statusCode: 500),
            Result(LinkKind.External, statusCode: 500),
        };

        Assert.Equal(ExitCodes.InternalFailures, ExitCodes.Compute(results));
    }

    [Fact]
    public void ExitCode_ignores_external_failures_entirely()
    {
        var results = new[]
        {
            Result(LinkKind.External, error: "timeout"),
            Result(LinkKind.External, statusCode: 500),
        };

        Assert.Equal(ExitCodes.Clean, ExitCodes.Compute(results));
    }
}
