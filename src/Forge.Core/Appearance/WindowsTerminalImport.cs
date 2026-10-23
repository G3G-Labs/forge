using System.Text.Json;
using Forge.Core.Models;

namespace Forge.Core.Appearance;

/// <summary>
/// Importa esquemas de cores no formato do Windows Terminal: o settings.json inteiro (lista "schemes"), uma lista
/// de esquemas ou um esquema só. O settings.json do Windows Terminal aceita comentários e vírgulas sobrando.
/// </summary>
public static class WindowsTerminalImport
{
    private static readonly string[] AnsiKeys =
    [
        "black", "red", "green", "yellow", "blue", "purple", "cyan", "white",
        "brightBlack", "brightRed", "brightGreen", "brightYellow", "brightBlue", "brightPurple", "brightCyan", "brightWhite",
    ];

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<ColorScheme> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, Options);
        var root = doc.RootElement;
        IEnumerable<JsonElement> items = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("schemes", out var schemes) && schemes.ValueKind == JsonValueKind.Array => schemes.EnumerateArray(),
            JsonValueKind.Object => [root],
            _ => [],
        };
        return items.Select(ToScheme).OfType<ColorScheme>().ToList();
    }

    private static ColorScheme? ToScheme(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || Text(e, "name") is not { } name || Text(e, "background") is not { } background)
            return null;
        var palette = AnsiKeys.Select(k => Text(e, k)).ToList();
        if (palette.Any(c => c is null))
            return null;
        return new ColorScheme
        {
            Name = name,
            Background = background,
            Foreground = Text(e, "foreground") ?? "#CCCCCC",
            SelectionBackground = Text(e, "selectionBackground") ?? "#264F78",
            CursorColor = Text(e, "cursorColor") ?? "#FFFFFF",
            Palette = palette.Select(c => c!).ToList(),
        };
    }

    private static string? Text(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
