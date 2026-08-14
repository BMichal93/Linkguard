namespace LinkGuard;

public enum OutputFormat
{
    Console,
    Json,
}

public sealed class OptionsParseException(string message) : Exception(message);

public sealed record Options
{
    public required Uri BaseUrl { get; init; }
    public int TimeoutSeconds { get; init; } = 20;
    public int MaxConcurrency { get; init; } = 8;
    public IReadOnlyList<string> IgnorePatterns { get; init; } = [];
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = [];
    public OutputFormat Format { get; init; } = OutputFormat.Console;
    public string? JUnitPath { get; init; }
    public bool NoExternal { get; init; }

    public const string UsageText = """
        Usage: linkguard --url <base-url> [options]

          --url <url>              Base URL to crawl (required), e.g. https://staging.example.com
          --timeout <seconds>       Per-request timeout, seconds (default: 20)
          --max-concurrency <n>     Cap on in-flight requests (default: 8)
          --ignore <pattern>        Substring or regex to skip; repeatable
          --header <Name: Value>    Header sent on every request; repeatable
          --format <console|json>   Output format (default: console)
          --junit <path>            Path to write a JUnit XML report
          --no-external              Skip external link checks entirely
        """;

    public static Options Parse(string[] args)
    {
        Uri? baseUrl = null;
        var timeout = 20;
        var maxConcurrency = 8;
        var ignore = new List<string>();
        var headers = new List<KeyValuePair<string, string>>();
        var format = OutputFormat.Console;
        string? junitPath = null;
        var noExternal = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--url":
                    baseUrl = ParseUri(RequireValue(args, ref i, arg));
                    break;
                case "--timeout":
                    timeout = ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;
                case "--max-concurrency":
                    maxConcurrency = ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;
                case "--ignore":
                    ignore.Add(RequireValue(args, ref i, arg));
                    break;
                case "--header":
                    headers.Add(ParseHeader(RequireValue(args, ref i, arg)));
                    break;
                case "--format":
                    format = ParseFormat(RequireValue(args, ref i, arg));
                    break;
                case "--junit":
                    junitPath = RequireValue(args, ref i, arg);
                    break;
                case "--no-external":
                    noExternal = true;
                    break;
                case "-h":
                case "--help":
                    throw new OptionsParseException(UsageText);
                default:
                    throw new OptionsParseException($"Unknown argument: {arg}\n\n{UsageText}");
            }
        }

        if (baseUrl is null)
            throw new OptionsParseException($"Missing required argument: --url\n\n{UsageText}");

        return new Options
        {
            BaseUrl = baseUrl,
            TimeoutSeconds = timeout,
            MaxConcurrency = maxConcurrency,
            IgnorePatterns = ignore,
            Headers = headers,
            Format = format,
            JUnitPath = junitPath,
            NoExternal = noExternal,
        };
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new OptionsParseException($"Missing value for {flag}\n\n{UsageText}");
        return args[++i];
    }

    private static Uri ParseUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new OptionsParseException($"Invalid --url: '{value}' is not an absolute http(s) URL\n\n{UsageText}");
        return uri;
    }

    private static int ParsePositiveInt(string value, string flag)
    {
        if (!int.TryParse(value, out var n) || n <= 0)
            throw new OptionsParseException($"Invalid value for {flag}: '{value}' must be a positive integer\n\n{UsageText}");
        return n;
    }

    private static OutputFormat ParseFormat(string value) => value switch
    {
        "console" => OutputFormat.Console,
        "json" => OutputFormat.Json,
        _ => throw new OptionsParseException($"Invalid --format: '{value}' must be 'console' or 'json'\n\n{UsageText}"),
    };

    private static KeyValuePair<string, string> ParseHeader(string value)
    {
        var idx = value.IndexOf(':');
        if (idx <= 0 || idx == value.Length - 1)
            throw new OptionsParseException($"Invalid --header: '{value}' must be 'Name: Value'\n\n{UsageText}");
        return new KeyValuePair<string, string>(value[..idx].Trim(), value[(idx + 1)..].Trim());
    }
}
