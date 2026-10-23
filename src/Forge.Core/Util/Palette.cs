namespace Forge.Core.Util;

/// <summary>Cores de grupo e aba da identidade do Forge. Nunca vermelho (erro) nem amarelo (administrador).</summary>
public static class Palette
{
    public static IReadOnlyList<string> GroupColors { get; } =
    [
        "#4F8EF7", // azul
        "#A47CF3", // violeta
        "#2BB5B0", // turquesa
        "#E58AC1", // rosa
        "#8A9BB0", // ardósia
        "#C9A27A", // areia
    ];

    /// <summary>A cor do próximo grupo: a primeira ainda não usada, ou a sequência recomeça.</summary>
    public static string NextGroupColor(IEnumerable<string> used)
    {
        var taken = used.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GroupColors.FirstOrDefault(c => !taken.Contains(c)) ?? GroupColors[taken.Count % GroupColors.Count];
    }
}
