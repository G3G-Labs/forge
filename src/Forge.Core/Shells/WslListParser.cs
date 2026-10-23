namespace Forge.Core.Shells;

/// <summary>Lê a saída de <c>wsl.exe -l -q</c>: uma distro por linha, em UTF-16.</summary>
public static class WslListParser
{
    // Distros internas do Docker Desktop: existem, mas não são para abrir um shell.
    private static readonly HashSet<string> Internal = new(StringComparer.OrdinalIgnoreCase)
    {
        "docker-desktop",
        "docker-desktop-data",
    };

    public static IReadOnlyList<string> Parse(string output)
    {
        // Lida como texto comum, a saída UTF-16 vira "U\0b\0u\0…": os nulos e o BOM saem antes de separar as linhas.
        var clean = output.Replace("\0", "", StringComparison.Ordinal).Replace("﻿", "", StringComparison.Ordinal);
        return clean
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => !Internal.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
