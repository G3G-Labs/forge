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

/// <summary>
/// Um terminal num ambiente, como a aba escolhe: o ambiente é o Windows ou uma distro WSL, e cada um abre vários
/// terminais. Na distro, os shells do Linux (bash, zsh…) e também os do Windows (PowerShell, CMD, Git Bash), que
/// abrem na pasta da distro por \\wsl.localhost. Detectado na máquina ou cadastrado à mão.
/// </summary>
public sealed record ShellProfile
{
    private readonly string? _terminal;
    private readonly string? _settingsId;

    public required string Id { get; init; }

    /// <summary>Nome completo: "PowerShell 7", "Debian · zsh", "Debian · PowerShell 7".</summary>
    public required string Name { get; init; }

    /// <summary>Só o terminal, para a lista que já está filtrada pelo ambiente: "zsh", "bash (padrão)".</summary>
    public string Terminal
    {
        get => _terminal ?? Name;
        init => _terminal = value;
    }

    /// <summary>Id das preferências por shell (integração): o do terminal do Windows, mesmo aberto numa distro.</summary>
    public string SettingsId
    {
        get => _settingsId ?? Id;
        init => _settingsId = value;
    }

    public ShellKind Kind { get; init; } = ShellKind.Custom;

    public ShellFlavor Flavor { get; init; } = ShellFlavor.Unknown;

    /// <summary>Caminho completo do executável (pwsh.exe, wsl.exe, bash.exe…).</summary>
    public required string Executable { get; init; }

    /// <summary>Argumentos extras, só para perfis personalizados.</summary>
    public string Arguments { get; init; } = "";

    /// <summary>
    /// Distro do ambiente WSL. Com <see cref="ShellKind.Wsl"/>, o shell roda no Linux; com um terminal do Windows,
    /// ele abre na pasta da distro (\\wsl.localhost\…) e o git da pasta roda dentro dela.
    /// </summary>
    public string? Distro { get; init; }

    /// <summary>O wsl.exe, quando o ambiente é uma distro.</summary>
    public string? WslExecutable { get; init; }

    /// <summary>O programa do Linux (bash, zsh, fish…), quando <see cref="Kind"/> é <see cref="ShellKind.Wsl"/>.</summary>
    public string LinuxShell { get; init; } = "bash";

    /// <summary>Home do usuário na distro (/home/dev), quando já foi detectada.</summary>
    public string? WslHome { get; init; }

    /// <summary>O shell roda no Linux.</summary>
    public bool IsWsl => Kind == ShellKind.Wsl;

    /// <summary>O ambiente é uma distro WSL (shell do Linux ou terminal do Windows aberto nela).</summary>
    public bool InWsl => Distro is not null;

    public string Environment => Distro is null ? "Windows" : "WSL · " + Distro;

    public bool SupportsIntegration => Flavor is ShellFlavor.PowerShell or ShellFlavor.Bash or ShellFlavor.Zsh;
}

public static class ShellIds
{
    public const string PowerShell = "pwsh";
    public const string WindowsPowerShell = "powershell";
    public const string Cmd = "cmd";
    public const string GitBash = "gitbash";

    /// <summary>O shell padrão da distro (o de login do usuário).</summary>
    public static string Wsl(string distro) => "wsl:" + distro;

    /// <summary>Outro terminal na distro: um shell do Linux ("wsl:Debian:zsh") ou um do Windows ("wsl:Debian:pwsh").</summary>
    public static string Wsl(string distro, string terminal) => "wsl:" + distro + ":" + terminal;

    public static string Custom(string name) => "custom:" + name;
}
