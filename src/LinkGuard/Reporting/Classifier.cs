namespace LinkGuard.Reporting;

public static class Classifier
{
    // A broken external link warns instead of failing - a third-party site being down at
    // 2am must not block a release. Any other outcome (2xx, or a 3xx chain that resolved
    // to one) is a pass; redirect chains are recorded separately and do not change this.
    public static CheckOutcome Classify(int? statusCode, string? error, LinkKind kind) =>
        error is not null || statusCode is >= 400
            ? (kind == LinkKind.Internal ? CheckOutcome.Fail : CheckOutcome.Warn)
            : CheckOutcome.Pass;
}
