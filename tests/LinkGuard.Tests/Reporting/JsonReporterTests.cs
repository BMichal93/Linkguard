using System.Text.Json;
using LinkGuard.Reporting;

namespace LinkGuard.Tests.Reporting;

public class JsonReporterTests
{
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
