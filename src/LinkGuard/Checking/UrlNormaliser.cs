using System.Text;

namespace LinkGuard.Checking;

public static class UrlNormaliser
{
    public static string NormalisedKey(Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        var path = CollapseSlashes(uri.AbsolutePath);
        if (path.Length > 1 && path.EndsWith('/'))
            path = path[..^1];

        return $"{scheme}://{host}{port}{path}{uri.Query}";
    }

    private static string CollapseSlashes(string path)
    {
        var builder = new StringBuilder(path.Length);
        var lastWasSlash = false;

        foreach (var c in path)
        {
            if (c == '/')
            {
                if (lastWasSlash)
                    continue;
                lastWasSlash = true;
            }
            else
            {
                lastWasSlash = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
