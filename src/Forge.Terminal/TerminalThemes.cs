using System.Globalization;
using Forge.Core.Models;
using Microsoft.Terminal.Wpf;

namespace Forge.Terminal;

/// <summary>Converte o esquema de cores e o cursor das configurações para o formato do controle nativo.</summary>
public static class TerminalThemes
{
    /// <summary>O esquema escolhido (pronto ou importado); se sumiu, o Forge Escuro.</summary>
    public static ColorScheme Resolve(AppearanceSettings appearance) =>
        BuiltInSchemes.All.Concat(appearance.ImportedSchemes).FirstOrDefault(s => s.Name == appearance.Scheme)
        ?? BuiltInSchemes.ForgeDark;

    public static TerminalTheme From(AppearanceSettings appearance)
    {
        var scheme = Resolve(appearance);
        var palette = scheme.Palette.Count == 16 ? scheme.Palette : BuiltInSchemes.ForgeDark.Palette;
        return new TerminalTheme
        {
            DefaultBackground = ColorRef(scheme.Background),
            DefaultForeground = ColorRef(scheme.Foreground),
            DefaultSelectionBackground = ColorRef(scheme.SelectionBackground),
            CursorStyle = (appearance.Cursor, appearance.CursorBlink) switch
            {
                (CursorShape.Block, true) => CursorStyle.BlinkingBlock,
                (CursorShape.Block, false) => CursorStyle.SteadyBlock,
                (CursorShape.Bar, true) => CursorStyle.BlinkingBar,
                (CursorShape.Bar, false) => CursorStyle.SteadyBar,
                (CursorShape.Underline, true) => CursorStyle.BlinkingUnderline,
                _ => CursorStyle.SteadyUnderline,
            },
            ColorTable = palette.Select(ColorRef).ToArray(),
        };
    }

    /// <summary>"#RRGGBB" para COLORREF (0x00BBGGRR), o formato do controle.</summary>
    internal static uint ColorRef(string hex)
    {
        var rgb = uint.Parse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);
    }
}
