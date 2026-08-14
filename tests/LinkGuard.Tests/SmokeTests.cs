using LinkGuard.Checking;
using LinkGuard.Reporting;

namespace LinkGuard.Tests;

// Not part of the default run: dotnet test --filter Category!=Smoke excludes this trait.
// Run it by hand once before the first pipeline integration, pointing LINKGUARD_SMOKE_URL
// at a real staging site, to confirm the tool copes with real Umbraco output, Cloudflare,
// and the geo-redirect middleware. It is a sanity check, not a gate - and unlike every other
// test in this suite, it does make a live network call.
public class SmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Run_AgainstRealStagingUrl_ExitsCleanlyOrWithKnownFailures()
    {
        var url = Environment.GetEnvironmentVariable("LINKGUARD_SMOKE_URL");
        if (string.IsNullOrWhiteSpace(url))
            return; // no staging URL configured - nothing to smoke-test right now

        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Cli.RunAsync(
            ["--url", url, "--timeout", "30"],
            LinkChecker.CreateHttpClient,
            output,
            error);

        Assert.True(
            exitCode is ExitCodes.Clean or ExitCodes.InternalFailures,
            $"unexpected exit code {exitCode}\nstderr: {error}\nstdout: {output}");
    }
}
