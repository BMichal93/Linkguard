using System.Diagnostics;
using LinkGuard.Checking;
using LinkGuard.Discovery;
using LinkGuard.Reporting;

namespace LinkGuard;

// Separated from Program.cs so integration tests can drive the whole check pipeline
// against a stub HttpClient and capture output, without needing a real process/Main.
public static class Runner
{
    public static async Task<int> RunAsync(Options options, HttpClient httpClient, TextWriter output, TimeProvider? timeProvider = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var checker = new LinkChecker(httpClient, options.MaxConcurrency, timeProvider);

        var internalUrls = IgnoreFilter.Apply(await DiscoverInternalUrlsAsync(httpClient, options), options.IgnorePatterns);
        var internalResults = await checker.CheckAllAsync(internalUrls.Select(u => (u, LinkKind.Internal)));
        var referrers = BuildReferrerMap(internalResults);

        var externalResults = options.NoExternal
            ? []
            : await CheckExternalLinksAsync(checker, internalResults, options);

        var allResults = internalResults.Concat(externalResults).ToList();
        stopwatch.Stop();

        var report = new LinkGuardReport
        {
            Results = allResults,
            Referrers = referrers,
            Duration = stopwatch.Elapsed,
        };

        if (options.Format == OutputFormat.Json)
            output.WriteLine(JsonReporter.Serialize(report));
        else
            ConsoleReporter.Report(report, output);

        if (options.JUnitPath is not null)
            JUnitReporter.Write(report, options.JUnitPath);

        return ExitCodes.Compute(allResults);
    }

    private static async Task<IReadOnlyList<Uri>> DiscoverInternalUrlsAsync(HttpClient httpClient, Options options)
    {
        var sitemapUrls = await new SitemapSource(httpClient).DiscoverAsync(options.BaseUrl);
        return sitemapUrls.Count > 0
            ? sitemapUrls
            : await new CrawlSource(httpClient, options.IgnorePatterns).DiscoverAsync(options.BaseUrl);
    }

    private static async Task<IReadOnlyList<CheckResult>> CheckExternalLinksAsync(
        LinkChecker checker, IReadOnlyList<CheckResult> internalResults, Options options)
    {
        var externalTargets = internalResults
            .SelectMany(r => r.DiscoveredLinks)
            .Where(u => !string.Equals(u.Host, options.BaseUrl.Host, StringComparison.OrdinalIgnoreCase))
            .Distinct();

        var filtered = IgnoreFilter.Apply(externalTargets, options.IgnorePatterns);
        return filtered.Count == 0
            ? []
            : await checker.CheckAllAsync(filtered.Select(u => (u, LinkKind.External)));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Uri>> BuildReferrerMap(IReadOnlyList<CheckResult> internalResults)
    {
        var map = new Dictionary<string, List<Uri>>();

        foreach (var page in internalResults)
        {
            foreach (var target in page.DiscoveredLinks)
            {
                var key = UrlNormaliser.NormalisedKey(target);
                if (!map.TryGetValue(key, out var referrers))
                    map[key] = referrers = [];
                if (!referrers.Contains(page.Url))
                    referrers.Add(page.Url);
            }
        }

        return map.ToDictionary(kv => kv.Key, IReadOnlyList<Uri> (kv) => kv.Value);
    }
}
