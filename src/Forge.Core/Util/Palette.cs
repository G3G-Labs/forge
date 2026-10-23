namespace Forge.Core.Util;

/// <summary>Uma cor da paleta, com o nome que aparece nos menus.</summary>
public sealed record NamedColor(string Name, string Hex);

/// <summary>
/// Cores de grupo e aba da identidade do Forge. Nunca vermelho (erro) nem amarelo (administrador) na paleta; a cor
/// personalizada (qualquer #RRGGBB) fica por conta do usuário.
/// </summary>
public static class Palette
{
    /// <summary>A paleta, na ordem dos menus. As seis primeiras são as cores originais dos grupos.</summary>
    public static IReadOnlyList<NamedColor> Colors { get; } =
    [
        new("Azul", "#4F8EF7"),
        new("Violeta", "#A47CF3"),
        new("Turquesa", "#2BB5B0"),
        new("Rosa", "#E58AC1"),
        new("Ardósia", "#8A9BB0"),
        new("Areia", "#C9A27A"),
        new("Céu", "#38BDF8"),
        new("Índigo", "#6C7CF0"),
        new("Lilás", "#C99BF5"),
        new("Magenta", "#D35BB0"),
        new("Laranja", "#F0994A"),
        new("Pêssego", "#F2B48C"),
        new("Oliva", "#A3B25A"),
        new("Verde", "#5DBB78"),
        new("Menta", "#4FD1A5"),
        new("Grafite", "#6B7280"),
    ];

    public static IReadOnlyList<string> GroupColors { get; } = Colors.Select(c => c.Hex).ToList();

    /// <summary>A cor do próximo grupo: a primeira ainda não usada, ou a sequência recomeça.</summary>
    public static string NextGroupColor(IEnumerable<string> used)
    {
        var taken = used.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GroupColors.FirstOrDefault(c => !taken.Contains(c)) ?? GroupColors[taken.Count % GroupColors.Count];
    }

    /// <summary>
    /// Cor digitada pelo usuário no formato do Forge (#RRGGBB, maiúsculas): aceita "#3a7", "3A7BD5" e "#3a7bd5".
    /// Null se não for uma cor.
    /// </summary>
    public static string? NormalizeHex(string? text)
    {
        var hex = text?.Trim().TrimStart('#') ?? "";
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6 || !hex.All(char.IsAsciiHexDigit))
            return null;
        return "#" + hex.ToUpperInvariant();
    }
}
