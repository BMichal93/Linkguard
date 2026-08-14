namespace LinkGuard.Reporting;

public enum LinkKind
{
    Internal,
    External,
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
}
