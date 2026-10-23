using Forge.Core.Models;

namespace Forge.Core.Shells;

/// <summary>
/// Lê a saída de <see cref="Script"/>, rodado dentro da distro: o /etc/shells, uma linha "---" e a linha do usuário
/// padrão no passwd (de onde saem a home e o shell de login).
/// </summary>
public static class WslShellsParser
{
    public const string Script = "cat /etc/shells 2>/dev/null; echo ---; getent passwd \"$(id -un)\"";

    // Os shells interativos que valem uma opção na lista, nesta ordem. sh, dash e rbash ficam de fora; o pwsh do Linux
    // também (o PowerShell da lista é o do Windows, aberto na pasta da distro).
    private static readonly string[] Known = ["bash", "zsh", "fish", "nu", "ksh", "tcsh"];

    public static WslDistroInfo? Parse(string output)
    {
        var lines = output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.TrimEntries);
        var separator = Array.IndexOf(lines, "---");
        if (separator < 0)
            return null;

        var installed = lines[..separator]
            .Where(l => l.StartsWith('/'))
            .Select(l => l[(l.LastIndexOf('/') + 1)..])
            .ToHashSet(StringComparer.Ordinal);

        string? home = null;
        string? login = null;
        if (lines.Skip(separator + 1).FirstOrDefault(l => l.Length > 0) is { } passwd)
        {
            // usuario:x:1000:1000:Nome:/home/usuario:/bin/zsh
            var fields = passwd.Split(':');
            if (fields.Length >= 7)
            {
                home = fields[5].Length > 0 ? fields[5] : null;
                login = fields[6].Length > 0 ? fields[6][(fields[6].LastIndexOf('/') + 1)..] : null;
            }
        }

        var defaultShell = login ?? (installed.Contains("bash") ? "bash" : null);
        if (defaultShell is null)
            return null;
        var shells = Known.Where(installed.Contains).Prepend(defaultShell).Distinct(StringComparer.Ordinal).ToList();
        return new WslDistroInfo(home, defaultShell, shells);
    }
}
