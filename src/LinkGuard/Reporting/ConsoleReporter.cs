namespace LinkGuard.Reporting;

public static class ConsoleReporter
{
    public static void Report(LinkGuardReport report, TextWriter writer)
    {
        var failures = report.Results.Where(r => r.Outcome == CheckOutcome.Fail).ToList();
        var warnings = report.Results.Where(r => r.Outcome == CheckOutcome.Warn).ToList();

        if (failures.Count == 0)
        {
            writer.WriteLine(
                $"Checked {report.Results.Count} URLs in {FormatDuration(report.Duration)} - {warnings.Count} warning(s)");
            return;
        }

        writer.WriteLine(
            $"Checked {report.Results.Count} URLs in {FormatDuration(report.Duration)} - " +
            $"{failures.Count} failed, {warnings.Count} warning(s)");

        writer.WriteLine();
        writer.WriteLine("Failures:");
        WriteGroupedByReferrer(writer, failures, report);

        if (warnings.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("Warnings:");
            WriteGroupedByReferrer(writer, warnings, report);
        }
    }

    private static void WriteGroupedByReferrer(TextWriter writer, IReadOnlyList<CheckResult> results, LinkGuardReport report)
    {
        var byReferrer = new SortedDictionary<string, List<CheckResult>>(StringComparer.Ordinal);

        foreach (var result in results)
        {
            var referrers = report.ReferrersFor(result.Url);
            var referrerLabels = referrers.Count > 0 ? referrers.Select(r => r.ToString()) : ["(no referring page found)"];

            foreach (var referrer in referrerLabels)
            {
                if (!byReferrer.TryGetValue(referrer, out var bucket))
                    byReferrer[referrer] = bucket = [];
                bucket.Add(result);
            }
        }

        foreach (var (referrer, items) in byReferrer)
        {
            writer.WriteLine($"  {referrer}");
            foreach (var item in items)
                WriteResultLine(writer, item);
        }
    }

    private static void WriteResultLine(TextWriter writer, CheckResult result)
    {
        var status = result.Error ?? result.StatusCode?.ToString() ?? "unknown";
        writer.WriteLine($"    -> {result.Url} [{status}]");

        if (result.RedirectChain.Count == 0)
            return;

        var chain = string.Join(" -> ", result.RedirectChain.Select(h => $"{h.Url} ({h.StatusCode})"));
        writer.WriteLine($"       redirects: {chain} -> {status}");
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalSeconds < 1 ? $"{duration.TotalMilliseconds:F0}ms" : $"{duration.TotalSeconds:F1}s";
}
