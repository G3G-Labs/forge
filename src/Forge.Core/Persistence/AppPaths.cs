namespace Forge.Core.Persistence;

/// <summary>
/// Onde o Forge guarda tudo: %APPDATA%\Forge. JSON legível, para editar à mão e versionar.
/// A pasta é injetável para os testes.
/// </summary>
public sealed class AppPaths(string? root = null)
{
    public static string DefaultRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Forge");

    public string Root { get; } = root ?? DefaultRoot;

    public string Settings => Path.Combine(Root, "settings.json");

    public string History => Path.Combine(Root, "history.jsonl");

    /// <summary>Scripts de inicialização gerados por aba (integração de shell, pasta, variáveis).</summary>
    public string Scripts => Path.Combine(Root, "shell");

    public string Logs => Path.Combine(Root, "logs");

    /// <summary>
    /// A janela de administrador é outro processo (elevado) e tem o próprio arquivo: os dois nunca gravam no
    /// mesmo workspaces.json, então não há disputa entre processos.
    /// </summary>
    public string Workspaces(bool elevated) =>
        Path.Combine(Root, elevated ? "workspaces-admin.json" : "workspaces.json");
}
