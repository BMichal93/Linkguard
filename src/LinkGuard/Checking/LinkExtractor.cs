using AngleSharp;
using AngleSharp.Html.Dom;

namespace LinkGuard.Checking;

public static class LinkExtractor
{
    public static async Task<IReadOnlyList<Uri>> ExtractHrefsAsync(string html, Uri pageUrl, CancellationToken cancellationToken = default)
    {
        var context = BrowsingContext.New(Configuration.Default);
        using var document = await context.OpenAsync(req => req.Content(html).Address(pageUrl.AbsoluteUri), cancellationToken);

        var hrefs = new List<Uri>();
        foreach (var anchor in document.QuerySelectorAll("a[href]").OfType<IHtmlAnchorElement>())
        {
            // anchor.Href is AngleSharp's already-resolved absolute URL, not the raw attribute.
            if (!Uri.TryCreate(anchor.Href, UriKind.Absolute, out var resolved))
                continue;
            if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
                continue;

            hrefs.Add(resolved);
        }

        return hrefs;
    }
}
