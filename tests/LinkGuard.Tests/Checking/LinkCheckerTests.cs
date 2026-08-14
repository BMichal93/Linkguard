using System.Net;
using LinkGuard.Checking;
using LinkGuard.Reporting;
using LinkGuard.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace LinkGuard.Tests.Checking;

public class LinkCheckerTests
{
    private static readonly Uri Url = new("https://example.com/page");

    [Fact]
    public async Task Check_200_Passes()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(200, result.StatusCode);
        Assert.Empty(result.RedirectChain);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Check_404_ReportsFailure()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.Status(HttpStatusCode.NotFound));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(404, result.StatusCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Check_4xx_IsNotRetried()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.Status(HttpStatusCode.NotFound));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(1, result.Attempts);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Check_500_ReportsFailureAfterExhaustingRetries()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.Status(HttpStatusCode.InternalServerError));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(500, result.StatusCode);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Check_503ThenSuccess_PassesAfterRetry()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.FailThenOk(failCount: 1));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task Check_RetriesUseBackoff_AdvancesTimeProviderNotWallClock()
    {
        var timeProvider = new FakeTimeProvider();
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.FailThenOk(failCount: 2));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4, timeProvider);

        var task = checker.CheckAsync(Url, LinkKind.Internal);

        // Nudge the fake clock forward past the pending retry delays. Task.Yield lets queued
        // continuations run without any real wall-clock wait. The delays themselves resolve
        // within a handful of rounds; the final WaitAsync is just a deadlock safety net for
        // the remaining (non-time-based) async work, not the retry mechanism under test.
        for (var round = 0; round < 20 && !task.IsCompleted; round++)
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Yield();
        }

        var result = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task Check_RedirectChainEndingIn200_PassesAndRecordsChain()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/page", StubHttpMessageHandler.Redirect("/page-2", permanent: true))
            .Map("https://example.com/page-2", StubHttpMessageHandler.Redirect("/page-3"))
            .Map("https://example.com/page-3", StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Error);
        Assert.Equal(
            [(301, "/page"), (302, "/page-2")],
            result.RedirectChain.Select(h => (h.StatusCode, h.Url.AbsolutePath)));
    }

    [Fact]
    public async Task Check_RedirectChainEndingIn404_ReportsFailureWithChain()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/page", StubHttpMessageHandler.Redirect("/gone", permanent: true))
            .Map("https://example.com/gone", StubHttpMessageHandler.Status(HttpStatusCode.NotFound));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(404, result.StatusCode);
        Assert.Single(result.RedirectChain);
        Assert.Equal(301, result.RedirectChain[0].StatusCode);
    }

    [Fact]
    public async Task Check_RedirectLoop_ReportsFailureNotHang()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/page", StubHttpMessageHandler.Redirect("/loop-b"))
            .Map("https://example.com/loop-b", StubHttpMessageHandler.Redirect("/page"));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("redirect loop", result.Error);
        Assert.NotEmpty(result.RedirectChain);
    }

    [Fact]
    public async Task Check_MoreThanFiveHops_ReportsFailure()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/hop-0", StubHttpMessageHandler.Redirect("/hop-1"))
            .Map("https://example.com/hop-1", StubHttpMessageHandler.Redirect("/hop-2"))
            .Map("https://example.com/hop-2", StubHttpMessageHandler.Redirect("/hop-3"))
            .Map("https://example.com/hop-3", StubHttpMessageHandler.Redirect("/hop-4"))
            .Map("https://example.com/hop-4", StubHttpMessageHandler.Redirect("/hop-5"))
            .Map("https://example.com/hop-5", StubHttpMessageHandler.Redirect("/hop-6"))
            .Map("https://example.com/hop-6", StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(new Uri("https://example.com/hop-0"), LinkKind.Internal);

        Assert.Equal("too many redirects", result.Error);
        Assert.Equal(5, result.RedirectChain.Count);
    }

    [Fact]
    public async Task Check_TransientTimeoutThenSuccess_PassesAfterRetry()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), attempt =>
            attempt == 1 ? throw new TaskCanceledException("simulated timeout") : new HttpResponseMessage(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Error);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task Check_TimeoutOnAllAttempts_ReportsFailureAfterExactlyThreeAttempts()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.Timeout());
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal("timeout", result.Error);
        Assert.Null(result.StatusCode);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Check_DnsFailure_ReportsFailure()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.DnsFailure());
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal("dns failure", result.Error);
        Assert.Null(result.StatusCode);
    }

    [Fact]
    public async Task Check_TlsFailure_ReportsFailure()
    {
        var handler = new StubHttpMessageHandler().Map(Url.ToString(), StubHttpMessageHandler.TlsFailure());
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal("tls failure", result.Error);
        Assert.Null(result.StatusCode);
    }

    [Fact]
    public async Task Check_ExternalLink_UsesHeadNotGet()
    {
        var externalUrl = new Uri("https://other.example.org/asset");
        var handler = new StubHttpMessageHandler().Map(externalUrl.ToString(), StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        await checker.CheckAsync(externalUrl, LinkKind.External);

        Assert.Equal([HttpMethod.Head], handler.Requests.Select(r => r.Method));
    }

    [Fact]
    public async Task Check_ExternalHeadReturns405_RetriesWithGet()
    {
        var externalUrl = new Uri("https://other.example.org/asset");
        var handler = new StubHttpMessageHandler().Map(externalUrl.ToString(), attempt => attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
            : new HttpResponseMessage(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(externalUrl, LinkKind.External);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal([HttpMethod.Head, HttpMethod.Get], handler.Requests.Select(r => r.Method));
    }

    [Fact]
    public async Task Check_ExternalLink_BodyNeverParsedForLinks()
    {
        var externalUrl = new Uri("https://other.example.org/asset");
        var handler = new StubHttpMessageHandler().Map(externalUrl.ToString(),
            StubHttpMessageHandler.Ok("""<a href="/should-not-be-followed">x</a>"""));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(externalUrl, LinkKind.External);

        Assert.Empty(result.DiscoveredLinks);
    }

    [Fact]
    public async Task Check_CustomHeaders_SentOnEveryRequestIncludingRedirectHops()
    {
        var handler = new StubHttpMessageHandler()
            .Map("https://example.com/page", StubHttpMessageHandler.Redirect("/page-2"))
            .Map("https://example.com/page-2", StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Add("CF-IPCountry", "PL");
        var checker = new LinkChecker(httpClient, maxConcurrency: 4);

        await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal("PL", r.Headers.GetValues("CF-IPCountry").Single()));
    }

    [Fact]
    public async Task CheckAllAsync_ConcurrencyCap_NeverExceedsConfiguredMax()
    {
        var handler = new StubHttpMessageHandler();
        var targets = Enumerable.Range(0, 20)
            .Select(i => new Uri($"https://example.com/page-{i}"))
            .ToList();
        foreach (var target in targets)
            handler.Map(target.ToString(), StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        await checker.CheckAllAsync(targets.Select(u => (u, LinkKind.Internal)));

        Assert.True(handler.MaxConcurrent <= 4, $"expected MaxConcurrent <= 4, was {handler.MaxConcurrent}");
    }

    [Fact]
    public async Task CheckAllAsync_SameUrlInDifferentForms_FetchedOnce()
    {
        var handler = new StubHttpMessageHandler().Map("https://example.com/about", StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var results = await checker.CheckAllAsync([
            (new Uri("https://example.com/about"), LinkKind.Internal),
            (new Uri("https://example.com/about/"), LinkKind.Internal),
        ]);

        Assert.Single(results);
        Assert.Single(handler.Requests);
    }
}
