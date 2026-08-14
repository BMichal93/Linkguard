using System.Net;
using LinkGuard.Checking;
using LinkGuard.Reporting;
using LinkGuard.Tests.TestSupport;

namespace LinkGuard.Tests.Checking;

public class LinkCheckerTests
{
    private static readonly Uri Url = new("https://example.com/page");

    [Fact]
    public async Task CheckAsync_reports_200_as_a_plain_success()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(200, result.StatusCode);
        Assert.Empty(result.RedirectChain);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_404()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.NotFound));
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(404, result.StatusCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_persistent_500_after_exhausting_retries()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return StubHttpMessageHandler.Status(HttpStatusCode.InternalServerError);
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(500, result.StatusCode);
        Assert.Equal(3, attempts);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task CheckAsync_follows_a_redirect_chain_ending_in_200()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/page" => StubHttpMessageHandler.Redirect("/page-2", HttpStatusCode.MovedPermanently),
            "/page-2" => StubHttpMessageHandler.Redirect("/page-3", HttpStatusCode.Found),
            "/page-3" => StubHttpMessageHandler.Status(HttpStatusCode.OK),
            _ => StubHttpMessageHandler.Status(HttpStatusCode.NotFound),
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Error);
        Assert.Equal(
            [(301, "/page"), (302, "/page-2")],
            result.RedirectChain.Select(h => (h.StatusCode, h.Url.AbsolutePath)));
    }

    [Fact]
    public async Task CheckAsync_follows_a_redirect_chain_ending_in_404()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/page" => StubHttpMessageHandler.Redirect("/gone", HttpStatusCode.MovedPermanently),
            "/gone" => StubHttpMessageHandler.Status(HttpStatusCode.NotFound),
            _ => StubHttpMessageHandler.Status(HttpStatusCode.NotFound),
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(404, result.StatusCode);
        Assert.Single(result.RedirectChain);
        Assert.Equal(301, result.RedirectChain[0].StatusCode);
    }

    [Fact]
    public async Task CheckAsync_detects_a_redirect_loop()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/page" => StubHttpMessageHandler.Redirect("/loop-b", HttpStatusCode.Found),
            "/loop-b" => StubHttpMessageHandler.Redirect("/page", HttpStatusCode.Found),
            _ => StubHttpMessageHandler.Status(HttpStatusCode.NotFound),
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("redirect loop", result.Error);
        Assert.Equal(302, result.StatusCode);
        Assert.NotEmpty(result.RedirectChain);
    }

    [Fact]
    public async Task CheckAsync_retries_a_timeout_and_succeeds()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            if (attempts == 1)
                throw new TaskCanceledException("simulated timeout");
            return StubHttpMessageHandler.Status(HttpStatusCode.OK);
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(Url, LinkKind.Internal);

        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Error);
        Assert.Equal(2, attempts);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task CheckAsync_uses_head_for_external_links_and_falls_back_to_get_on_405()
    {
        var methodsSeen = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            methodsSeen.Add(request.Method.Method);
            return request.Method == HttpMethod.Head
                ? StubHttpMessageHandler.Status(HttpStatusCode.MethodNotAllowed)
                : StubHttpMessageHandler.Status(HttpStatusCode.OK);
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var result = await checker.CheckAsync(new Uri("https://other.example.org/asset"), LinkKind.External);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(["HEAD", "GET"], methodsSeen);
    }

    [Fact]
    public async Task CheckAllAsync_dedupes_targets_that_normalise_to_the_same_url()
    {
        var requestedPaths = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedPaths.Add(request.RequestUri!.AbsolutePath);
            return StubHttpMessageHandler.Status(HttpStatusCode.OK);
        });
        var checker = new LinkChecker(new HttpClient(handler), maxConcurrency: 4);

        var results = await checker.CheckAllAsync([
            (new Uri("https://example.com/about"), LinkKind.Internal),
            (new Uri("https://example.com/about/"), LinkKind.Internal),
        ]);

        Assert.Single(results);
        Assert.Single(requestedPaths);
    }
}
