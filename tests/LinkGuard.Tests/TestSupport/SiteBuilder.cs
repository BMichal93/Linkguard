using System.Net;

namespace LinkGuard.Tests.TestSupport;

/// <summary>
/// A test site as an HttpClient wired to a StubHttpMessageHandler. Fluent so tests read
/// as site shapes ("here is a page linking to a 404") rather than HTTP plumbing.
/// </summary>
public sealed record TestSite(Uri BaseUrl, HttpClient HttpClient, StubHttpMessageHandler Handler);

public sealed class SiteBuilder(string baseUrl)
{
    private readonly Uri _baseUrl = new(baseUrl);
    private readonly StubHttpMessageHandler _handler = new();

    public SiteBuilder WithRobots(string body)
    {
        _handler.Map(Resolve("/robots.txt"), StubHttpMessageHandler.Text(body));
        return this;
    }

    public SiteBuilder WithSitemap(params string[] paths)
    {
        var entries = string.Join("\n", paths.Select(p => $"  <url><loc>{Resolve(p)}</loc></url>"));
        var xml = $"""
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            {entries}
            </urlset>
            """;
        _handler.Map(Resolve("/sitemap.xml"), StubHttpMessageHandler.Xml(xml));
        return this;
    }

    public SiteBuilder WithPage(string path, string? html = null, IReadOnlyList<string>? links = null)
    {
        _handler.Map(Resolve(path), StubHttpMessageHandler.Ok(html ?? RenderLinks(links ?? [])));
        return this;
    }

    public SiteBuilder WithStatus(string path, int statusCode)
    {
        _handler.Map(Resolve(path), StubHttpMessageHandler.Status((HttpStatusCode)statusCode));
        return this;
    }

    public SiteBuilder WithRoute(string path, Func<int, HttpResponseMessage> factory)
    {
        _handler.Map(Resolve(path), factory);
        return this;
    }

    public TestSite Build() => new(_baseUrl, new HttpClient(_handler), _handler);

    private string Resolve(string pathOrAbsoluteUrl)
    {
        // A leading "/" also parses as an absolute file:// URI on Unix, so require an http(s)
        // scheme before treating the input as already-absolute rather than relative to the site.
        var isAbsoluteHttpUrl = Uri.TryCreate(pathOrAbsoluteUrl, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps);

        return (isAbsoluteHttpUrl ? absolute! : new Uri(_baseUrl, pathOrAbsoluteUrl)).ToString();
    }

    private static string RenderLinks(IReadOnlyList<string> links) =>
        $"<html><body>{string.Join("", links.Select(l => $"""<a href="{l}">{l}</a>"""))}</body></html>";
}
