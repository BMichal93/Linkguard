using LinkGuard.Checking;

namespace LinkGuard.Discovery;

public sealed class CrawlSource(HttpClient httpClient)
{
    public async Task<IReadOnlyList<Uri>> DiscoverAsync(Uri baseUrl, CancellationToken cancellationToken = default)
    {
        var root = StripFragment(baseUrl);
        var queue = new Queue<Uri>([root]);
        var seen = new HashSet<Uri> { root };
        var discovered = new List<Uri>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            discovered.Add(current);

            var html = await TryGetHtmlAsync(current, cancellationToken);
            if (html is null)
                continue;

            foreach (var link in await LinkExtractor.ExtractHrefsAsync(html, current, cancellationToken))
            {
                if (!IsSameHost(link, root))
                    continue;

                var normalized = StripFragment(link);
                if (seen.Add(normalized))
                    queue.Enqueue(normalized);
            }
        }

        return discovered;
    }

    private async Task<string?> TryGetHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not null && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static bool IsSameHost(Uri uri, Uri baseUrl) =>
        string.Equals(uri.Host, baseUrl.Host, StringComparison.OrdinalIgnoreCase);

    private static Uri StripFragment(Uri uri) =>
        string.IsNullOrEmpty(uri.Fragment) ? uri : new UriBuilder(uri) { Fragment = string.Empty }.Uri;
}
