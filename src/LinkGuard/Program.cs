using LinkGuard;
using LinkGuard.Checking;

return await Cli.RunAsync(args, LinkChecker.CreateHttpClient, Console.Out, Console.Error);
