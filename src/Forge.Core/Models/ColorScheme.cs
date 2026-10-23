namespace Forge.Core.Models;

/// <summary>Esquema de cores do terminal, no mesmo formato do Windows Terminal (cores em #RRGGBB).</summary>
public sealed class ColorScheme
{
    public string Name { get; set; } = "";

    public string Background { get; set; } = "#111214";

    public string Foreground { get; set; } = "#D7DAE0";

    public string SelectionBackground { get; set; } = "#2A3B57";

    public string CursorColor { get; set; } = "#E7E9EE";

    /// <summary>As 16 cores ANSI: preto, vermelho, verde, amarelo, azul, magenta, ciano, branco e as brilhantes.</summary>
    public List<string> Palette { get; set; } = [];
}

public static class BuiltInSchemes
{
    public static ColorScheme ForgeDark { get; } = new()
    {
        Name = "Forge Escuro",
        Background = "#111214",
        Foreground = "#D7DAE0",
        SelectionBackground = "#2A3B57",
        CursorColor = "#E7E9EE",
        Palette =
        [
            "#1B1D22", "#F2616B", "#4CC38A", "#E8B931", "#8AB4FF", "#C792EA", "#56C2D6", "#C9CED8",
            "#4A505B", "#FF8A92", "#74DBA8", "#F5D06A", "#B5CFFF", "#DDB3F5", "#88DCEA", "#F3F4F7",
        ],
    };

    public static ColorScheme ForgeLight { get; } = new()
    {
        Name = "Forge Claro",
        Background = "#FBFBFC",
        Foreground = "#2A2F37",
        SelectionBackground = "#CFE0FF",
        CursorColor = "#1B1E24",
        Palette =
        [
            "#1B1E24", "#D23A45", "#1A7548", "#9A6A00", "#2F6FEB", "#8E44AD", "#0F6E7C", "#626A78",
            "#4A5160", "#E5535D", "#1F8A55", "#B07D00", "#4F86F0", "#A55CC4", "#138A9C", "#1B1E24",
        ],
    };

    public static IReadOnlyList<ColorScheme> All { get; } = [ForgeDark, ForgeLight];
}
