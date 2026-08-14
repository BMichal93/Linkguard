using System.Net;
using LinkGuard.Reporting;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests;

public class CliTests
{
    [Fact]
    public async Task Run_HealthySite_ExitsZero()
    {
        var site = new SiteBuilder("https://example.test")
            .WithPage("/", links: ["/about"])
            .WithPage("/about")
            .Build();

        var (exitCode, output) = await RunAsync(["--url", "https://example.test/"], site);

        Assert.Equal(ExitCodes.Clean, exitCode);
        Assert.DoesNotContain("Failures:", output);
    }

    [Fact]
    public async Task Run_OneInternal404_ExitsOne()
    {
        var site = new SiteBuilder("https://example.test")
            .WithPage("/", links: ["/missing"])
            .WithStatus("/missing", 404)
            .Build();

        var (exitCode, output) = await RunAsync(["--url", "https://example.test/"], site);

        Assert.Equal(ExitCodes.InternalFailures, exitCode);
        Assert.Contains("Failures:", output);
        Assert.Contains("https://example.test/missing", output);
    }

    [Fact]
    public async Task Run_OnlyExternalFailures_ExitsZeroAndPrintsWarnings()
    {
        var site = new SiteBuilder("https://example.test")
            .WithPage("/", links: ["https://other.test/broken"])
            .WithRoute("https://other.test/broken", StubHttpMessageHandler.Status(HttpStatusCode.NotFound))
            .Build();

        var (exitCode, output) = await RunAsync(["--url", "https://example.test/"], site);

        Assert.Equal(ExitCodes.Clean, exitCode);
        Assert.Contains("1 warning(s)", output);
    }

    [Fact]
    public async Task Run_SitemapUnavailable_FallsBackToCrawlAndStillChecks()
    {
        var site = new SiteBuilder("https://example.test")
            .WithStatus("/robots.txt", 404)
            .WithStatus("/sitemap.xml", 404)
            .WithPage("/", links: ["/about"])
            .WithPage("/about")
            .Build();

        var (exitCode, output) = await RunAsync(["--url", "https://example.test/"], site);

        Assert.Equal(ExitCodes.Clean, exitCode);
        Assert.Contains("Checked 2 URLs", output);
    }

    [Fact]
    public async Task Run_BadArguments_ExitsTwo()
    {
        var site = new SiteBuilder("https://example.test").Build();

        var (exitCode, _) = await RunAsync([], site);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task Run_NoExternalFlag_SkipsExternalRequests()
    {
        var site = new SiteBuilder("https://example.test")
            .WithPage("/", links: ["https://other.test/x"])
            .Build();

        await RunAsync(["--url", "https://example.test/", "--no-external"], site);

        Assert.DoesNotContain(site.Handler.Requests, r => r.RequestUri!.Host == "other.test");
    }

    [Fact]
    public async Task Run_JunitFlag_WritesFileEvenWhenClean()
    {
        var site = new SiteBuilder("https://example.test").WithPage("/").Build();
        var path = Path.Combine(Path.GetTempPath(), $"linkguard-cli-{Guid.NewGuid():N}.xml");

        try
        {
            var (exitCode, _) = await RunAsync(["--url", "https://example.test/", "--junit", path], site);

            Assert.Equal(ExitCodes.Clean, exitCode);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Run_LargeSite_500Urls_CompletesWithinBudget()
    {
        var builder = new SiteBuilder("https://example.test");
        var links = Enumerable.Range(0, 499).Select(i => $"/page-{i}").ToArray();
        builder.WithPage("/", links: links);
        foreach (var link in links)
            builder.WithPage(link);
        var site = builder.Build();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var (exitCode, _) = await RunAsync(["--url", "https://example.test/", "--max-concurrency", "16"], site);
        stopwatch.Stop();

        Assert.Equal(ExitCodes.Clean, exitCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string[] args, TestSite site)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Cli.RunAsync(args, _ => site.HttpClient, output, error);

        return (exitCode, output.ToString() + error);
    }
}
