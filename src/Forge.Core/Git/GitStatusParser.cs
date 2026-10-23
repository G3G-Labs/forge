using System.Globalization;
using Forge.Core.Models;

namespace Forge.Core.Git;

/// <summary>Lê a saída de <c>git status --porcelain=v2 --branch</c>.</summary>
public static class GitStatusParser
{
    public static GitStatus Parse(string output)
    {
        string? head = null, oid = null, upstream = null;
        int ahead = 0, behind = 0, changes = 0;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
                head = line["# branch.head ".Length..];
            else if (line.StartsWith("# branch.oid ", StringComparison.Ordinal))
                oid = line["# branch.oid ".Length..];
            else if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal))
                upstream = line["# branch.upstream ".Length..];
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
                (ahead, behind) = ParseAheadBehind(line["# branch.ab ".Length..]);
            // 1 = alterado, 2 = renomeado, u = conflito, ? = não rastreado. "!" (ignorado) não conta.
            else if (line.Length > 1 && line[1] == ' ' && line[0] is '1' or '2' or 'u' or '?')
                changes++;
        }

        var detached = head == "(detached)";
        var branch = detached
            ? (oid is { Length: >= 7 } ? oid[..7] : "HEAD")
            : head ?? "HEAD";
        return new GitStatus(branch, upstream, ahead, behind, changes, detached);
    }

    // "+2 -1"
    private static (int Ahead, int Behind) ParseAheadBehind(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ahead = parts.Length > 0 && int.TryParse(parts[0].TrimStart('+'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 0;
        var behind = parts.Length > 1 && int.TryParse(parts[1].TrimStart('-'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 0;
        return (ahead, behind);
    }
}
