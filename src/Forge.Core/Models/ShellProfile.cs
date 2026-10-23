namespace Forge.Core.Models;

public enum ShellKind
{
    PowerShell,
    WindowsPowerShell,
    Cmd,
    GitBash,
    Wsl,
    Custom,
}

/// <summary>A linguagem do shell. Decide o script de inicialização e se há integração (OSC 133).</summary>
public enum ShellFlavor
{
    PowerShell,
    Bash,
    Zsh,
    Cmd,
    Unknown,
}

/// <summary>Um shell que o usuário pode abrir numa aba: detectado na máquina ou cadastrado à mão.</summary>
public sealed record ShellProfile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public ShellKind Kind { get; init; } = ShellKind.Custom;

    public ShellFlavor Flavor { get; init; } = ShellFlavor.Unknown;

    /// <summary>Caminho completo do executável (pwsh.exe, wsl.exe, bash.exe…).</summary>
    public required string Executable { get; init; }

    /// <summary>Argumentos extras, só para perfis personalizados.</summary>
    public string Arguments { get; init; } = "";

    /// <summary>Distro, quando <see cref="Kind"/> é <see cref="ShellKind.Wsl"/>.</summary>
    public string? Distro { get; init; }

    public bool IsWsl => Kind == ShellKind.Wsl;

    public bool SupportsIntegration => Flavor is ShellFlavor.PowerShell or ShellFlavor.Bash or ShellFlavor.Zsh;
}

public static class ShellIds
{
    public const string PowerShell = "pwsh";
    public const string WindowsPowerShell = "powershell";
    public const string Cmd = "cmd";
    public const string GitBash = "gitbash";

    public static string Wsl(string distro) => "wsl:" + distro;

    public static string Custom(string name) => "custom:" + name;
}
