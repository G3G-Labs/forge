using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Core.Persistence;

/// <summary>Formato dos arquivos do Forge: camelCase, enums por nome e campos nulos omitidos.</summary>
public static class ForgeJson
{
    public static JsonSerializerOptions Indented { get; } = Create(indented: true);

    /// <summary>Uma linha por registro (history.jsonl).</summary>
    public static JsonSerializerOptions Line { get; } = Create(indented: false);

    private static JsonSerializerOptions Create(bool indented) => new()
    {
        WriteIndented = indented,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
