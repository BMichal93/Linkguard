using LinkGuard;

try
{
    var options = Options.Parse(args);

    Console.WriteLine($"url: {options.BaseUrl}");
    Console.WriteLine($"timeout: {options.TimeoutSeconds}s");
    Console.WriteLine($"max-concurrency: {options.MaxConcurrency}");
    Console.WriteLine($"ignore: {(options.IgnorePatterns.Count == 0 ? "(none)" : string.Join(", ", options.IgnorePatterns))}");
    Console.WriteLine($"headers: {(options.Headers.Count == 0 ? "(none)" : string.Join(", ", options.Headers.Select(h => $"{h.Key}: {h.Value}")))}");
    Console.WriteLine($"format: {options.Format}");
    Console.WriteLine($"junit: {options.JUnitPath ?? "(none)"}");
    Console.WriteLine($"no-external: {options.NoExternal}");

    return 0;
}
catch (OptionsParseException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
