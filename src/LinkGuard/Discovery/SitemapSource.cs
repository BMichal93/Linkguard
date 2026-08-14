using System.Xml;
using System.Xml.Linq;

namespace LinkGuard.Discovery;

public sealed class SitemapSource(HttpClient httpClient)
{
    private const int MaxIndexDepth = 3;

    public async Task<IReadOnlyList<Uri>> DiscoverAsync(Uri baseUrl, CancellationToken cancellationToken = default)
    {
        var sitemapUrls = await FindSitemapUrlsFromRobotsAsync(baseUrl, cancellationToken);
        if (sitemapUrls.Count == 0)
            sitemapUrls = [new Uri(baseUrl, "/sitemap.xml")];

        var visited = new HashSet<Uri>();
        var pageUrls = new List<Uri>();
        foreach (var sitemapUrl in sitemapUrls)
            await CollectFromSitemapAsync(sitemapUrl, baseUrl, visited, pageUrls, depth: 0, cancellationToken);

        return pageUrls.Distinct().ToList();
    }

    private async Task<List<Uri>> FindSitemapUrlsFromRobotsAsync(Uri baseUrl, CancellationToken cancellationToken)
    {
        var robotsText = await TryGetStringAsync(new Uri(baseUrl, "/robots.txt"), cancellationToken);
        var sitemapUrls = new List<Uri>();
        if (robotsText is null)
            return sitemapUrls;

        foreach (var line in robotsText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = trimmed["Sitemap:".Length..].Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out var sitemapUri))
                sitemapUrls.Add(sitemapUri);
        }

        return sitemapUrls;
    }

    private async Task CollectFromSitemapAsync(
        Uri sitemapUrl, Uri baseUrl, HashSet<Uri> visited, List<Uri> pageUrls, int depth, CancellationToken cancellationToken)
    {
        if (depth > MaxIndexDepth || !visited.Add(sitemapUrl))
            return;

        var xml = await TryGetStringAsync(sitemapUrl, cancellationToken);
        if (xml is null)
            return;

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return;
        }

        var root = document.Root;
        if (root is null)
            return;

        var ns = root.Name.Namespace;

        if (root.Name.LocalName == "sitemapindex")
        {
            var children = ExtractLocs(root.Elements(ns + "sitemap"), ns);
            foreach (var child in children)
                await CollectFromSitemapAsync(child, baseUrl, visited, pageUrls, depth + 1, cancellationToken);
        }
        else if (root.Name.LocalName == "urlset")
        {
            pageUrls.AddRange(ExtractLocs(root.Elements(ns + "url"), ns).Where(u => IsSameHost(u, baseUrl)));
        }
    }

    private static IEnumerable<Uri> ExtractLocs(IEnumerable<XElement> entries, XNamespace ns) =>
        entries
            .Elements(ns + "loc")
            .Select(e => e.Value.Trim())
            .Where(v => Uri.TryCreate(v, UriKind.Absolute, out _))
            .Select(v => new Uri(v));

    private async Task<string?> TryGetStringAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static bool IsSameHost(Uri uri, Uri baseUrl) =>
        string.Equals(uri.Host, baseUrl.Host, StringComparison.OrdinalIgnoreCase);
}
