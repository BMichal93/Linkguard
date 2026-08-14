namespace LinkGuard.Reporting;

public enum LinkKind
{
    Internal,
    External,
}

public enum CheckOutcome
{
    Pass,
    Fail,
    Warn,
}

public sealed record RedirectHop(Uri Url, int StatusCode);

public sealed record CheckResult
{
    public required Uri Url { get; init; }
    public required LinkKind Kind { get; init; }

    // Null when the final attempt never produced a response (timeout, DNS, TLS, too many redirects).
    public int? StatusCode { get; init; }
    public IReadOnlyList<RedirectHop> RedirectChain { get; init; } = [];
    public string? Error { get; init; }
    public int Attempts { get; init; } = 1;

    // Outbound links found in an internal page's body - used to build the referrer map for reporting
    // and, for external links, as check targets. Never populated for external checks (one level deep only).
    public IReadOnlyList<Uri> DiscoveredLinks { get; init; } = [];

    public CheckOutcome Outcome => Classifier.Classify(StatusCode, Error, Kind);
}

public sealed record LinkGuardReport
{
    public required IReadOnlyList<CheckResult> Results { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<Uri>> Referrers { get; init; }
    public required TimeSpan Duration { get; init; }

    public IReadOnlyList<Uri> ReferrersFor(Uri url) =>
        Referrers.TryGetValue(Checking.UrlNormaliser.NormalisedKey(url), out var list) ? list : [];
}

public static class ExitCodes
{
    public const int Clean = 0;
    public const int InternalFailures = 1;
    public const int UsageError = 2;

    public static int Compute(IEnumerable<CheckResult> results) =>
        results.Any(r => r.Kind == LinkKind.Internal && r.Outcome == CheckOutcome.Fail)
            ? InternalFailures
            : Clean;
}
