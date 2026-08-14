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

    // A broken external link warns instead of failing - a third-party site being down at 2am must not block a release.
    public CheckOutcome Outcome => Error is not null || StatusCode is >= 400
        ? (Kind == LinkKind.Internal ? CheckOutcome.Fail : CheckOutcome.Warn)
        : CheckOutcome.Pass;
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
