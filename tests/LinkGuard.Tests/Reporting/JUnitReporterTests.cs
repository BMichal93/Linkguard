using System.Xml.Linq;
using LinkGuard.Reporting;

namespace LinkGuard.Tests.Reporting;

public class JUnitReporterTests
{
    [Fact]
    public void Write_produces_one_testcase_per_checked_url_with_failure_details()
    {
        var brokenUrl = new Uri("https://example.com/broken");
        var okUrl = new Uri("https://example.com/");
        var homeUrl = new Uri("https://example.com/");

        var report = new LinkGuardReport
        {
            Results =
            [
                new CheckResult { Url = okUrl, Kind = LinkKind.Internal, StatusCode = 200 },
                new CheckResult { Url = brokenUrl, Kind = LinkKind.Internal, StatusCode = 404 },
            ],
            Referrers = new Dictionary<string, IReadOnlyList<Uri>> { ["https://example.com/broken"] = [homeUrl] },
            Duration = TimeSpan.FromSeconds(3),
        };

        var path = Path.Combine(Path.GetTempPath(), $"linkguard-{Guid.NewGuid():N}.xml");
        try
        {
            JUnitReporter.Write(report, path);
            var document = XDocument.Load(path);
            var suite = document.Root!;

            Assert.Equal("2", suite.Attribute("tests")!.Value);
            Assert.Equal("1", suite.Attribute("failures")!.Value);

            var testCases = suite.Elements("testcase").ToList();
            Assert.Equal(2, testCases.Count);

            var brokenCase = testCases.Single(tc => tc.Attribute("name")!.Value == brokenUrl.ToString());
            var failure = brokenCase.Element("failure");
            Assert.NotNull(failure);
            Assert.Contains("404", failure!.Attribute("message")!.Value);
            Assert.Contains(homeUrl.ToString(), failure.Value);

            var okCase = testCases.Single(tc => tc.Attribute("name")!.Value == okUrl.ToString());
            Assert.Null(okCase.Element("failure"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
