using AngleSharp;
using AngleSharp.Html.Dom;

namespace LinkGuard.Checking;

public static class LinkExtractor
{
    public static async Task<IReadOnlyList<Uri>> ExtractHrefsAsync(string html, Uri pageUrl, CancellationToken cancellationToken = default)
    {
        var context = BrowsingContext.New(Configuration.Default);
        using var document = await context.OpenAsync(req => req.Content(html).Address(pageUrl.AbsoluteUri), cancellationToken);

        var seen = new HashSet<Uri>();
        var hrefs = new List<Uri>();

        foreach (var anchor in document.QuerySelectorAll("a[href]").OfType<IHtmlAnchorElement>())
        {
            // Check the raw attribute for empty/fragment-only hrefs - the resolved anchor.Href
            // would otherwise turn href="" or href="#top" into a same-page link worth "checking".
            var rawHref = anchor.GetAttribute("href");
            if (string.IsNullOrEmpty(rawHref) || rawHref.StartsWith('#'))
                continue;

            // anchor.Href is AngleSharp's already-resolved absolute URL (respects a <base> tag).
            if (!Uri.TryCreate(anchor.Href, UriKind.Absolute, out var resolved) || !UrlNormaliser.IsCheckable(resolved))
                continue;

            if (seen.Add(resolved))
                hrefs.Add(resolved);
        }

        return hrefs;
    }
}
