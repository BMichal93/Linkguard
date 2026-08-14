using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkGuard.Reporting;

public static class JsonReporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(LinkGuardReport report)
    {
        var payload = new
        {
            checkedUrls = report.Results.Count,
            durationMs = report.Duration.TotalMilliseconds,
            failed = report.Results.Count(r => r.Outcome == CheckOutcome.Fail),
            warnings = report.Results.Count(r => r.Outcome == CheckOutcome.Warn),
            results = report.Results.Select(r => new
            {
                url = r.Url.ToString(),
                kind = r.Kind,
                outcome = r.Outcome,
                statusCode = r.StatusCode,
                error = r.Error,
                attempts = r.Attempts,
                redirectChain = r.RedirectChain.Select(h => new { url = h.Url.ToString(), statusCode = h.StatusCode }),
                referrers = report.ReferrersFor(r.Url).Select(u => u.ToString()),
            }),
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}
