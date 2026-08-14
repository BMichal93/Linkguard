using LinkGuard.Reporting;

namespace LinkGuard;

// The testable entry point: arg parsing plus wiring, with the HttpClient construction and
// both output streams injected so integration tests can exercise the whole thing without
// touching the real network or a real process's Console.
public static class Cli
{
    public static async Task<int> RunAsync(
        string[] args,
        Func<Options, HttpClient> httpClientFactory,
        TextWriter output,
        TextWriter error,
        TimeProvider? timeProvider = null)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (OptionsParseException ex)
        {
            error.WriteLine(ex.Message);
            return ExitCodes.UsageError;
        }

        using var httpClient = httpClientFactory(options);
        return await Runner.RunAsync(options, httpClient, output, timeProvider);
    }
}
