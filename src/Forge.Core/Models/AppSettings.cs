namespace Forge.Core.Models;

/// <summary>Para onde cada aba volta ao reabrir o app (decisão em aberto no escopo; padrão: pasta inicial).</summary>
public enum FolderOnRestore
{
    StartFolder,
    LastVisited,
}

public enum CursorShape
{
    Block,
    Bar,
    Underline,
}

/// <summary>Preferências globais em %APPDATA%\Forge\settings.json, separadas dos workspaces.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    /// <summary>Shell da aba nova (Ctrl+T) quando o usuário não escolhe outro.</summary>
    public string DefaultShell { get; set; } = ShellIds.PowerShell;

    public bool StartWithWindows { get; set; }

    /// <summary>Reabrir com o mesmo tamanho, posição e aba ativa.</summary>
    public bool RestoreWindow { get; set; } = true;

    public FolderOnRestore FolderOnRestore { get; set; } = FolderOnRestore.StartFolder;

    /// <summary>Linhas guardadas no history.jsonl.</summary>
    public int HistoryLimit { get; set; } = 50_000;

    /// <summary>Um arquivo de log por aba, com rotação a cada 5 MB. Desligado por padrão.</summary>
    public bool FileLog { get; set; }

    /// <summary>A única chamada de rede do app.</summary>
    public bool CheckUpdates { get; set; } = true;

    public AppearanceSettings Appearance { get; set; } = new();

    public IdentitySettings Identity { get; set; } = new();

    public HelperSettings Helpers { get; set; } = new();

    public List<ShellProfile> CustomProfiles { get; set; } = [];

    /// <summary>Perfis em que a integração de shell foi desligada (ex.: conflita com o perfil do usuário).</summary>
    public List<string> IntegrationDisabled { get; set; } = [];

    /// <summary>Atalhos remapeados (ação → tecla, ex.: "NewTab": "Ctrl+Shift+T"). Só o que difere do padrão.</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = new();
}

public sealed class AppearanceSettings
{
    /// <summary>Nome do esquema de cores: um dos prontos ou um importado do Windows Terminal.</summary>
    public string Scheme { get; set; } = BuiltInSchemes.ForgeDark.Name;

    public List<ColorScheme> ImportedSchemes { get; set; } = [];

    public string FontFamily { get; set; } = "Cascadia Mono";

    public double FontSize { get; set; } = 13;

    public CursorShape Cursor { get; set; } = CursorShape.Block;

    public bool CursorBlink { get; set; } = true;
}

/// <summary>Nome de exibição do usuário e da máquina, definido pelo próprio usuário.</summary>
public sealed class IdentitySettings
{
    public string? UserName { get; set; }

    public string? MachineName { get; set; }

    public bool ShowInHeader { get; set; } = true;

    public bool InjectInPowerShellPrompt { get; set; }

    public bool InjectInBashPrompt { get; set; }

    public string Display(string fallbackUser, string fallbackMachine) =>
        $"{(string.IsNullOrWhiteSpace(UserName) ? fallbackUser : UserName)}@{(string.IsNullOrWhiteSpace(MachineName) ? fallbackMachine : MachineName)}";
}

/// <summary>Auxílios garantidos em todos os shells. Cada um pode ser desligado se conflitar com o perfil do usuário.</summary>
public sealed class HelperSettings
{
    /// <summary>bash e zsh: Tab completa ignorando maiúsculas (PowerShell e CMD já fazem isso).</summary>
    public bool CaseInsensitiveCompletion { get; set; } = true;

    /// <summary>O shell grava o histórico a cada comando (history -a, PSReadLine incremental).</summary>
    public bool FlushHistoryEachCommand { get; set; } = true;

    /// <summary>Scripts OSC 133 que alimentam o estado da aba e o histórico unificado.</summary>
    public bool ShellIntegration { get; set; } = true;

    public bool ClickableLinks { get; set; } = true;
}
