using System.Net;
using System.Text;
using LinkGuard.Checking;

namespace LinkGuard.Tests.TestSupport;

/// <summary>
/// A route map from URL to a response factory. The factory takes the attempt number
/// (1-based, per distinct URL), so a single route can fail then succeed - that is how
/// retry gets tested without any real network flakiness. Routes are matched on the
/// normalised absolute URI. An unmapped URL returns 404 rather than throwing, so tests
/// only declare what matters.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<int, HttpResponseMessage>> _routes = [];
    private readonly Dictionary<string, int> _attempts = [];
    private readonly List<HttpRequestMessage> _requests = [];
    private readonly Lock _gate = new();
    private int _inFlight;
    private int _maxConcurrent;

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;
    public int MaxConcurrent => _maxConcurrent;

    public StubHttpMessageHandler Map(string url, Func<int, HttpResponseMessage> factory)
    {
        _routes[UrlNormaliser.NormalisedKey(new Uri(url))] = factory;
        return this;
    }

    public StubHttpMessageHandler Map(string url, HttpResponseMessage response) => Map(url, _ => response);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        TrackRequest(request);

        // A yield forces a real scheduling gap, so concurrent callers actually overlap here
        // instead of running the dispatch loop to completion one at a time. No wall-clock wait.
        TrackInFlightStart();
        try
        {
            await Task.Yield();

            var key = UrlNormaliser.NormalisedKey(request.RequestUri!);
            int attempt;
            lock (_gate)
            {
                attempt = _attempts[key] = _attempts.GetValueOrDefault(key) + 1;
            }

            if (!_routes.TryGetValue(key, out var factory))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            return factory(attempt);
        }
        finally
        {
            TrackInFlightEnd();
        }
    }

    private void TrackRequest(HttpRequestMessage request)
    {
        lock (_gate)
            _requests.Add(request);
    }

    private void TrackInFlightStart()
    {
        lock (_gate)
        {
            _inFlight++;
            _maxConcurrent = Math.Max(_maxConcurrent, _inFlight);
        }
    }

    private void TrackInFlightEnd()
    {
        lock (_gate)
            _inFlight--;
    }

    public static Func<int, HttpResponseMessage> Ok(string html) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        };

    public static Func<int, HttpResponseMessage> Text(string body) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };

    public static Func<int, HttpResponseMessage> Xml(string body) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/xml"),
        };

    public static Func<int, HttpResponseMessage> Status(HttpStatusCode status) => _ => new HttpResponseMessage(status);

    public static Func<int, HttpResponseMessage> Redirect(string to, bool permanent = false) =>
        _ =>
        {
            var response = new HttpResponseMessage(permanent ? HttpStatusCode.MovedPermanently : HttpStatusCode.Found);
            response.Headers.Location = new Uri(to, UriKind.RelativeOrAbsolute);
            return response;
        };

    public static Func<int, HttpResponseMessage> Timeout() => _ => throw new TaskCanceledException("simulated timeout");

    public static Func<int, HttpResponseMessage> DnsFailure() =>
        _ => throw new HttpRequestException(HttpRequestError.NameResolutionError, "simulated dns failure");

    public static Func<int, HttpResponseMessage> TlsFailure() =>
        _ => throw new HttpRequestException(HttpRequestError.SecureConnectionError, "simulated tls failure");

    public static Func<int, HttpResponseMessage> FailThenOk(int failCount) =>
        attempt => attempt <= failCount
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK);
}
