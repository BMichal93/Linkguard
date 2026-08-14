using System.Text.RegularExpressions;

namespace LinkGuard;

public static class IgnoreFilter
{
    public static IReadOnlyList<Uri> Apply(IEnumerable<Uri> urls, IReadOnlyList<string> patterns) =>
        patterns.Count == 0 ? urls.ToList() : urls.Where(u => !Matches(u, patterns)).ToList();

    public static bool Matches(Uri url, IReadOnlyList<string> patterns)
    {
        var text = url.ToString();
        foreach (var pattern in patterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase) || TryMatchRegex(text, pattern))
                return true;
        }

        return false;
    }

    private static bool TryMatchRegex(string text, string pattern)
    {
        try
        {
            return Regex.IsMatch(text, pattern);
        }
        catch (ArgumentException)
        {
            return false; // not a valid regex - the substring check already covered the plain case
        }
    }
}
