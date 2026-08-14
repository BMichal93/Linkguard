using System.Globalization;
using System.Xml.Linq;

namespace LinkGuard.Reporting;

public static class JUnitReporter
{
    public static void Write(LinkGuardReport report, string path)
    {
        var suite = new XElement("testsuite",
            new XAttribute("name", "LinkGuard"),
            new XAttribute("tests", report.Results.Count),
            new XAttribute("failures", report.Results.Count(r => r.Outcome == CheckOutcome.Fail)),
            new XAttribute("time", report.Duration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)),
            report.Results.Select(r => BuildTestCase(r, report)));

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        new XDocument(suite).Save(path);
    }

    private static XElement BuildTestCase(CheckResult result, LinkGuardReport report)
    {
        var testCase = new XElement("testcase",
            new XAttribute("classname", $"LinkGuard.{result.Kind}"),
            new XAttribute("name", result.Url.ToString()));

        var details = DescribeDetails(result, report);

        testCase.Add(result.Outcome switch
        {
            CheckOutcome.Fail => new XElement("failure", new XAttribute("message", DescribeStatus(result)), details),
            CheckOutcome.Warn => new XElement("system-out", $"WARNING: {DescribeStatus(result)}\n{details}"),
            _ => new XElement("system-out", details),
        });

        return testCase;
    }

    private static string DescribeStatus(CheckResult result) => result.Error ?? $"HTTP {result.StatusCode}";

    private static string DescribeDetails(CheckResult result, LinkGuardReport report)
    {
        var referrers = report.ReferrersFor(result.Url);
        var referrerText = referrers.Count > 0 ? string.Join(", ", referrers) : "(no referring page found)";

        var chainText = result.RedirectChain.Count > 0
            ? " via " + string.Join(" -> ", result.RedirectChain.Select(h => $"{h.Url} ({h.StatusCode})"))
            : "";

        return $"Referenced from: {referrerText}{chainText}";
    }
}
