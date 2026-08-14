using System.Text.Json;
using LinkGuard.Reporting;

namespace LinkGuard.Tests.Reporting;

public class JsonReporterTests
{
    private sealed record ResultShape(string Url, string Kind, string Outcome, int? StatusCode, string[] Referrers);
    private sealed record ReportShape(int CheckedUrls, int Failed, int Warnings, ResultShape[] Results);

    [Fact]
    public void Json_Shape_IsStableAndParseable()
    {
        var brokenUrl = new Uri("https://example.com/broken");
        var homeUrl = new Uri("https://example.com/");
        var report = new LinkGuardReport
        {
            Results = [new CheckResult { Url = brokenUrl, Kind = LinkKind.Internal, StatusCode = 404 }],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>> { ["https://example.com/broken"] = [homeUrl] },
            Duration = TimeSpan.FromSeconds(1),
        };

        var json = JsonReporter.Serialize(report);
        var parsed = JsonSerializer.Deserialize<ReportShape>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(parsed);
        Assert.Equal(1, parsed.CheckedUrls);
        Assert.Equal(1, parsed.Failed);
        Assert.Equal(0, parsed.Warnings);
        var result = Assert.Single(parsed.Results);
        Assert.Equal(brokenUrl.ToString(), result.Url);
        Assert.Equal("Fail", result.Outcome);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal([homeUrl.ToString()], result.Referrers);

        // Re-serializing the same report produces the same shape - no ordering/nondeterminism.
        Assert.Equal(json, JsonReporter.Serialize(report));
    }

    [Fact]
    public void Serialize_produces_the_full_result_set_with_referrers()
    {
        var brokenUrl = new Uri("https://example.com/broken");
        var homeUrl = new Uri("https://example.com/");

        var report = new LinkGuardReport
        {
            Results =
            [
                new CheckResult
                {
                    Url = brokenUrl,
                    Kind = LinkKind.Internal,
                    StatusCode = 404,
                    RedirectChain = [new RedirectHop(brokenUrl, 301)],
                },
            ],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>> { ["https://example.com/broken"] = [homeUrl] },
            Duration = TimeSpan.FromSeconds(1),
        };

        var json = JsonReporter.Serialize(report);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("checkedUrls").GetInt32());
        Assert.Equal(1, root.GetProperty("failed").GetInt32());

        var result = root.GetProperty("results")[0];
        Assert.Equal(brokenUrl.ToString(), result.GetProperty("url").GetString());
        Assert.Equal("Fail", result.GetProperty("outcome").GetString());
        Assert.Equal(404, result.GetProperty("statusCode").GetInt32());
        Assert.Equal(homeUrl.ToString(), result.GetProperty("referrers")[0].GetString());
        Assert.Equal(1, result.GetProperty("redirectChain").GetArrayLength());
    }
}
