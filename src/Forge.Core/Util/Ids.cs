namespace Forge.Core.Util;

/// <summary>Ids curtos e legíveis no JSON ("g1a2b3c4d", "t9f8e7d6c").</summary>
public static class Ids
{
    public static string New(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
