using System.Diagnostics;
using LinkGuard;
using LinkGuard.Checking;
using LinkGuard.Discovery;
using LinkGuard.Reporting;

try
{
    var options = Options.Parse(args);
    return await RunAsync(options);
}
catch (OptionsParseException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCodes.UsageError;
}

static async Task<int> RunAsync(Options options)
{
    var stopwatch = Stopwatch.StartNew();
    using var httpClient = LinkChecker.CreateHttpClient(options);
    var checker = new LinkChecker(httpClient, options.MaxConcurrency);

    var internalUrls = IgnoreFilter.Apply(await DiscoverInternalUrlsAsync(httpClient, options.BaseUrl), options.IgnorePatterns);
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
        Console.WriteLine(JsonReporter.Serialize(report));
    else
        ConsoleReporter.Report(report, Console.Out);

    if (options.JUnitPath is not null)
        JUnitReporter.Write(report, options.JUnitPath);

    return ExitCodes.Compute(allResults);
}

static async Task<IReadOnlyList<Uri>> DiscoverInternalUrlsAsync(HttpClient httpClient, Uri baseUrl)
{
    var sitemapUrls = await new SitemapSource(httpClient).DiscoverAsync(baseUrl);
    return sitemapUrls.Count > 0 ? sitemapUrls : await new CrawlSource(httpClient).DiscoverAsync(baseUrl);
}

static async Task<IReadOnlyList<CheckResult>> CheckExternalLinksAsync(
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

static IReadOnlyDictionary<string, IReadOnlyList<Uri>> BuildReferrerMap(IReadOnlyList<CheckResult> internalResults)
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
