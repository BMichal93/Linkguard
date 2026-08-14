namespace LinkGuard.Tests.TestSupport;

public static class Fixtures
{
    public static string Load(string name)
    {
        var assembly = typeof(Fixtures).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(name, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
