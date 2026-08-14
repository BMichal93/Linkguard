using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using LinkGuard.Reporting;

namespace LinkGuard.Checking;

public sealed class LinkChecker(HttpClient httpClient, int maxConcurrency)
{
    private const int MaxRedirectHops = 5;
    private const int MaxAttempts = 3; // one initial attempt plus two retries

    public static HttpClient CreateHttpClient(Options options)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = options.MaxConcurrency,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(BuildUserAgent());
        foreach (var header in options.Headers)
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

        return client;
    }

    private static string BuildUserAgent()
    {
        var pipeline = Environment.GetEnvironmentVariable("BUILD_DEFINITIONNAME");
        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");
        return pipeline is null
            ? "LinkGuard/1.0 (+https://github.com/BMichal93/Linkguard)"
            : $"LinkGuard/1.0 (+{pipeline}; build {buildId})";
    }

    public async Task<IReadOnlyList<CheckResult>> CheckAllAsync(
        IEnumerable<(Uri Url, LinkKind Kind)> targets, CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<(Uri Url, LinkKind Kind)>();
        var seen = new ConcurrentDictionary<string, byte>();

        foreach (var target in targets)
        {
            if (seen.TryAdd(UrlNormaliser.NormalisedKey(target.Url), 0))
                await channel.Writer.WriteAsync(target, cancellationToken);
        }
        channel.Writer.Complete();

        var results = new ConcurrentBag<CheckResult>();
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var workers = new List<Task>();

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            await semaphore.WaitAsync(cancellationToken);
            workers.Add(Task.Run(async () =>
            {
                try
                {
                    results.Add(await CheckAsync(item.Url, item.Kind, cancellationToken));
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(workers);
        return results.ToList();
    }

    public async Task<CheckResult> CheckAsync(Uri url, LinkKind kind, CancellationToken cancellationToken = default)
    {
        var chain = new List<RedirectHop>();
        var visited = new HashSet<Uri> { url };
        var current = url;
        var attemptsUsed = 0;

        while (true)
        {
            var (response, error, attempts) = await SendWithRetryAsync(current, kind, cancellationToken);
            attemptsUsed += attempts;

            if (response is null)
            {
                return new CheckResult
                {
                    Url = url,
                    Kind = kind,
                    StatusCode = null,
                    RedirectChain = chain,
                    Error = error,
                    Attempts = attemptsUsed,
                };
            }

            var status = (int)response.StatusCode;
            var location = response.Headers.Location;

            if (status is >= 300 and < 400 && location is not null)
            {
                response.Dispose();

                if (chain.Count >= MaxRedirectHops)
                {
                    return new CheckResult
                    {
                        Url = url,
                        Kind = kind,
                        StatusCode = status,
                        RedirectChain = chain,
                        Error = "too many redirects",
                        Attempts = attemptsUsed,
                    };
                }

                chain.Add(new RedirectHop(current, status));
                var next = location.IsAbsoluteUri ? location : new Uri(current, location);

                if (!visited.Add(next))
                {
                    return new CheckResult
                    {
                        Url = url,
                        Kind = kind,
                        StatusCode = status,
                        RedirectChain = chain,
                        Error = "redirect loop",
                        Attempts = attemptsUsed,
                    };
                }

                current = next;
                continue;
            }

            var discoveredLinks = await ExtractLinksIfInternalHtmlAsync(response, kind, status, current, cancellationToken);
            response.Dispose();
            return new CheckResult
            {
                Url = url,
                Kind = kind,
                StatusCode = status,
                RedirectChain = chain,
                Attempts = attemptsUsed,
                DiscoveredLinks = discoveredLinks,
            };
        }
    }

    // Internal checks GET the body anyway, so link extraction is a free byproduct - it feeds the
    // referrer map for reporting and surfaces external targets, without ever recursing into them.
    private static async Task<IReadOnlyList<Uri>> ExtractLinksIfInternalHtmlAsync(
        HttpResponseMessage response, LinkKind kind, int status, Uri pageUrl, CancellationToken cancellationToken)
    {
        if (kind != LinkKind.Internal || status is < 200 or >= 300)
            return [];

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not null && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            return [];

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return await LinkExtractor.ExtractHrefsAsync(html, pageUrl, cancellationToken);
    }

    private async Task<(HttpResponseMessage? Response, string? Error, int Attempts)> SendWithRetryAsync(
        Uri uri, LinkKind kind, CancellationToken cancellationToken)
    {
        string? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var method = kind == LinkKind.Internal ? HttpMethod.Get : HttpMethod.Head;
                var response = await SendAsync(uri, method, cancellationToken);

                // Plenty of CDNs reject HEAD; fall back to GET rather than reporting a false failure.
                if (kind == LinkKind.External && response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    response.Dispose();
                    response = await SendAsync(uri, HttpMethod.Get, cancellationToken);
                }

                if ((int)response.StatusCode >= 500 && attempt < MaxAttempts)
                {
                    response.Dispose();
                    await DelayBeforeRetry(attempt, cancellationToken);
                    continue;
                }

                return (response, null, attempt);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = "timeout";
            }
            catch (HttpRequestException ex)
            {
                lastError = ClassifyHttpError(ex);
            }

            if (attempt < MaxAttempts)
                await DelayBeforeRetry(attempt, cancellationToken);
        }

        return (null, lastError ?? "request failed", MaxAttempts);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, HttpMethod method, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static Task DelayBeforeRetry(int attempt, CancellationToken cancellationToken)
    {
        var jitterMs = Random.Shared.Next(0, 250);
        return Task.Delay(TimeSpan.FromMilliseconds(200 * attempt + jitterMs), cancellationToken);
    }

    private static string ClassifyHttpError(HttpRequestException ex) => ex.HttpRequestError switch
    {
        HttpRequestError.NameResolutionError => "dns failure",
        HttpRequestError.SecureConnectionError => "tls failure",
        _ => "connection failure",
    };
}
