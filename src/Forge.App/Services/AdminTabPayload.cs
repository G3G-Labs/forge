using System.Text;
using System.Text.Json;
using Forge.Core.Models;
using Forge.Core.Persistence;

namespace Forge.App.Services;

/// <summary>
/// A aba que a janela comum manda para a de administrador ("Abrir como administrador"). Vai como argumento do
/// novo processo elevado, em base64, junto com o nome e a cor do grupo para a janela elevada espelhar a árvore.
/// </summary>
public sealed record AdminTabPayload(string GroupName, string GroupColor, WorkspaceTab Tab)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, ForgeJson.Line)));

    public static AdminTabPayload? Decode(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<AdminTabPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(text)), ForgeJson.Line);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
