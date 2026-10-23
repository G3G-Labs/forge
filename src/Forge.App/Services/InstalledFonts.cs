using System.Windows;
using System.Windows.Media;

namespace Forge.App.Services;

/// <summary>Uma fonte instalada, para a busca em Configurações › Aparência.</summary>
public sealed record FontChoice(string Name, bool Monospaced);

/// <summary>
/// As fontes instaladas no Windows: as do sistema e as instaladas só para o usuário (o padrão do Windows 10 1809 em
/// diante, onde costumam ficar as Nerd Fonts), que o WPF não lista sozinho. Lento (abre cada arquivo de fonte):
/// chame fora da thread da interface.
/// </summary>
public static class InstalledFonts
{
    public static IReadOnlyList<FontChoice> Read()
    {
        var families = Fonts.SystemFontFamilies.ToList();
        var userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        if (Directory.Exists(userFonts))
        {
            try
            {
                families.AddRange(Fonts.GetFontFamilies(userFonts + Path.DirectorySeparatorChar));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Pasta ilegível: ficam as do sistema.
            }
        }

        return families
            .Select(f => new FontChoice(NameOf(f), IsMonospaced(f)))
            .Where(f => f.Name.Length > 0)
            .DistinctBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // "Cascadia Mono" do sistema; "file:///C:/…/Fonts/#Fira Code" das fontes do usuário.
    private static string NameOf(FontFamily family)
    {
        var source = family.Source;
        var hash = source.LastIndexOf('#');
        return (hash >= 0 ? source[(hash + 1)..] : source).Trim();
    }

    /// <summary>Monoespaçada: i, M, W, ponto e zero com a mesma largura.</summary>
    private static bool IsMonospaced(FontFamily family)
    {
        try
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyphs))
                return false;
            double? width = null;
            foreach (var c in "iMW.0")
            {
                if (!glyphs.CharacterToGlyphMap.TryGetValue(c, out var index))
                    return false;
                var advance = glyphs.AdvanceWidths[index];
                if (width is null)
                    width = advance;
                else if (Math.Abs(advance - width.Value) > 0.001)
                    return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException or NotSupportedException)
        {
            // Arquivo de fonte corrompido ou inacessível: entra na lista, sem a marca de monoespaçada.
            return false;
        }
    }
}
