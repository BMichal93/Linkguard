using LinkGuard.Reporting;

namespace LinkGuard.Tests.Reporting;

public class ConsoleReporterTests
{
    private static readonly Uri HomeUrl = new("https://example.com/");
    private static readonly Uri BrokenUrl = new("https://example.com/broken");
    private static readonly Uri ExternalUrl = new("https://other.example.org/asset");

    [Fact]
    public void Report_CleanRun_PrintsExactlyOneLine()
    {
        var report = new LinkGuardReport
        {
            Results = [new CheckResult { Url = HomeUrl, Kind = LinkKind.Internal, StatusCode = 200 }],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>>(),
            Duration = TimeSpan.FromSeconds(2.5),
        };

        var writer = new StringWriter();
        ConsoleReporter.Report(report, writer);

        var output = writer.ToString().TrimEnd('\n', '\r');
        Assert.Single(output.Split('\n'));
        Assert.Contains("1 URLs", output);
        Assert.Contains("0 warning(s)", output);
    }

    [Fact]
    public void Report_Failures_GroupedByReferringPage()
    {
        var failure = new CheckResult { Url = BrokenUrl, Kind = LinkKind.Internal, StatusCode = 404 };
        var report = new LinkGuardReport
        {
            Results = [failure],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>> { ["https://example.com/broken"] = [HomeUrl] },
            Duration = TimeSpan.FromMilliseconds(500),
        };

        var writer = new StringWriter();
        ConsoleReporter.Report(report, writer);
        var output = writer.ToString();

        Assert.Contains(HomeUrl.ToString(), output);
        Assert.Contains(BrokenUrl.ToString(), output);
        Assert.Contains("404", output);
    }

    [Fact]
    public void Report_FailuresBeforeWarnings()
    {
        var failure = new CheckResult { Url = BrokenUrl, Kind = LinkKind.Internal, StatusCode = 404 };
        var warning = new CheckResult { Url = ExternalUrl, Kind = LinkKind.External, StatusCode = 500 };

        var report = new LinkGuardReport
        {
            Results = [failure, warning],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>>
            {
                ["https://example.com/broken"] = [HomeUrl],
                ["https://other.example.org/asset"] = [HomeUrl],
            },
            Duration = TimeSpan.FromMilliseconds(500),
        };

        var writer = new StringWriter();
        ConsoleReporter.Report(report, writer);
        var output = writer.ToString();

        var failuresIndex = output.IndexOf("Failures:", StringComparison.Ordinal);
        var warningsIndex = output.IndexOf("Warnings:", StringComparison.Ordinal);

        Assert.True(failuresIndex >= 0 && warningsIndex > failuresIndex);
    }

    [Fact]
    public void Report_BrokenLinkWithNoKnownReferrer_IsLabelled()
    {
        var failure = new CheckResult { Url = BrokenUrl, Kind = LinkKind.Internal, StatusCode = 404 };
        var report = new LinkGuardReport
        {
            Results = [failure],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>>(),
            Duration = TimeSpan.Zero,
        };

        var writer = new StringWriter();
        ConsoleReporter.Report(report, writer);

        Assert.Contains("(no referring page found)", writer.ToString());
    }

    [Fact]
    public void Report_RedirectChain_IsIncludedWhenPresent()
    {
        var failure = new CheckResult
        {
            Url = BrokenUrl,
            Kind = LinkKind.Internal,
            StatusCode = 404,
            RedirectChain = [new RedirectHop(BrokenUrl, 301)],
        };
        var report = new LinkGuardReport
        {
            Results = [failure],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>>(),
            Duration = TimeSpan.Zero,
        };

        var writer = new StringWriter();
        ConsoleReporter.Report(report, writer);

        Assert.Contains("redirects:", writer.ToString());
    }
}
