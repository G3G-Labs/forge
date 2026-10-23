namespace Forge.Core.Shells;

/// <summary>
/// Um arquivo que o app grava antes de abrir a aba (script de inicialização), com as quebras de linha certas.
/// <paramref name="Utf8Bom"/>: o Windows PowerShell 5.1 lê arquivo sem BOM como ANSI e estragaria pastas com acento.
/// </summary>
public sealed record ScriptFile(string WindowsPath, string Content, bool Utf8Bom = false);

/// <summary>
/// Como abrir uma aba: a linha de comando entregue ao ConPTY e os scripts que ela usa.
/// <paramref name="Integration"/> diz se o shell vai emitir OSC 133 (estado da aba e histórico unificado).
/// </summary>
public sealed record LaunchPlan(string CommandLine, IReadOnlyList<ScriptFile> Scripts, bool Integration);
