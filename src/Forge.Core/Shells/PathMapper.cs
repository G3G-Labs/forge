namespace Forge.Core.Shells;

/// <summary>Converte pastas entre o Windows, o WSL e o Git Bash (MSYS).</summary>
public static class PathMapper
{
    /// <summary>Caminho do Linux (/home/dev) ou relativo à home (~/api), e não do Windows.</summary>
    public static bool IsLinuxPath(string path) => path.StartsWith('/') || path == "~" || path.StartsWith("~/", StringComparison.Ordinal);

    public static bool IsWslUnc(string path) =>
        path.StartsWith(@"\\wsl.localhost\", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(@"\\wsl$\", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pasta como o WSL a enxerga: C:\dev\web → /mnt/c/dev/web, \\wsl.localhost\Ubuntu\home\dev → /home/dev.
    /// Caminhos do Linux voltam como estão.
    /// </summary>
    public static string ToWsl(string path)
    {
        if (IsLinuxPath(path))
            return path;
        if (IsWslUnc(path))
        {
            // \\wsl.localhost\<distro>\resto → /resto
            var parts = path.TrimStart('\\').Split('\\', 3);
            return parts.Length < 3 ? "/" : "/" + parts[2].Replace('\\', '/');
        }
        if (IsDrivePath(path))
            return "/mnt/" + char.ToLowerInvariant(path[0]) + Rest(path);
        return path.Replace('\\', '/');
    }

    /// <summary>Pasta como o Git Bash a enxerga: C:\Users\dev → /c/Users/dev.</summary>
    public static string ToMsys(string path)
    {
        if (IsLinuxPath(path))
            return path;
        if (IsDrivePath(path))
            return "/" + char.ToLowerInvariant(path[0]) + Rest(path);
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Pasta de uma aba WSL como o Windows a enxerga (Explorer, VS Code sem extensão): /mnt/c/dev → C:\dev,
    /// /home/dev/api → \\wsl.localhost\Ubuntu\home\dev\api.
    /// </summary>
    public static string ToWindows(string linuxPath, string distro)
    {
        if (linuxPath.StartsWith("/mnt/", StringComparison.Ordinal) && linuxPath.Length >= 6 && char.IsAsciiLetter(linuxPath[5])
            && (linuxPath.Length == 6 || linuxPath[6] == '/'))
        {
            var rest = linuxPath.Length > 6 ? linuxPath[7..].Replace('/', '\\') : "";
            return char.ToUpperInvariant(linuxPath[5]) + @":\" + rest;
        }
        return $@"\\wsl.localhost\{distro}" + linuxPath.Replace('/', '\\');
    }

    /// <summary>
    /// Pasta de um OSC 7 (file://host/caminho, com %XX): "file://pc/home/dev" → "/home/dev",
    /// "file:///C:/dev/web" → "C:\dev\web". Null se não for um file://.
    /// </summary>
    public static string? FromFileUri(string uri)
    {
        const string Scheme = "file://";
        if (!uri.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            return null;
        var afterScheme = uri[Scheme.Length..];
        var slash = afterScheme.IndexOf('/');
        if (slash < 0)
            return null;
        var path = Uri.UnescapeDataString(afterScheme[slash..]);
        // /C:/dev/web → C:\dev\web
        if (path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':')
            return path[1..].Replace('/', '\\');
        return path;
    }

    private static bool IsDrivePath(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    // "C:" → "", "C:\dev\web\" → "/dev/web"
    private static string Rest(string drivePath)
    {
        var rest = drivePath[2..].Replace('\\', '/').TrimEnd('/');
        return rest.Length == 0 || rest.StartsWith('/') ? rest : "/" + rest;
    }
}
