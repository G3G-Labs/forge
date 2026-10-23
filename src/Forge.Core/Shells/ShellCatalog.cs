using Forge.Core.Models;

namespace Forge.Core.Shells;

/// <summary>O que o catálogo precisa saber da máquina. A implementação real (registro, PATH, wsl.exe) fica no Forge.Terminal.</summary>
public interface IShellProbe
{
    string SystemRoot { get; }

    string ProgramFiles { get; }

    bool FileExists(string path);

    /// <summary>Caminho completo de um executável encontrado no PATH, ou null.</summary>
    string? FindOnPath(string exe);

    /// <summary>Pasta de instalação do Git for Windows, lida do registro, ou null.</summary>
    string? GitInstallPath();

    Task<IReadOnlyList<string>> ListWslDistrosAsync(CancellationToken ct);
}

/// <summary>
/// Os shells disponíveis: PowerShell 7 e 5.1, CMD, Git Bash e cada distro WSL, detectados automaticamente,
/// mais os perfis personalizados das configurações.
/// </summary>
public sealed class ShellCatalog(IShellProbe probe)
{
    public IReadOnlyList<ShellProfile> Profiles { get; private set; } = [];

    public async Task<IReadOnlyList<ShellProfile>> DetectAsync(IEnumerable<ShellProfile> custom, CancellationToken ct = default)
    {
        var list = new List<ShellProfile>();
        var system32 = Win(probe.SystemRoot, "System32");

        var pwsh = probe.FindOnPath("pwsh.exe") ?? Existing(Win(probe.ProgramFiles, "PowerShell", "7", "pwsh.exe"));
        if (pwsh is not null)
            list.Add(new ShellProfile { Id = ShellIds.PowerShell, Name = "PowerShell 7", Kind = ShellKind.PowerShell, Flavor = ShellFlavor.PowerShell, Executable = pwsh });

        if (Existing(Win(system32, "WindowsPowerShell", "v1.0", "powershell.exe")) is { } winPs)
            list.Add(new ShellProfile { Id = ShellIds.WindowsPowerShell, Name = "Windows PowerShell 5.1", Kind = ShellKind.WindowsPowerShell, Flavor = ShellFlavor.PowerShell, Executable = winPs });

        if (Existing(Win(system32, "cmd.exe")) is { } cmd)
            list.Add(new ShellProfile { Id = ShellIds.Cmd, Name = "Prompt de Comando", Kind = ShellKind.Cmd, Flavor = ShellFlavor.Cmd, Executable = cmd });

        var gitRoot = probe.GitInstallPath() ?? Win(probe.ProgramFiles, "Git");
        if (Existing(Win(gitRoot, "bin", "bash.exe")) is { } bash)
            list.Add(new ShellProfile { Id = ShellIds.GitBash, Name = "Git Bash", Kind = ShellKind.GitBash, Flavor = ShellFlavor.Bash, Executable = bash });

        if (Existing(Win(system32, "wsl.exe")) is { } wsl)
        {
            foreach (var distro in await probe.ListWslDistrosAsync(ct).ConfigureAwait(false))
            {
                list.Add(new ShellProfile
                {
                    Id = ShellIds.Wsl(distro),
                    Name = distro,
                    Kind = ShellKind.Wsl,
                    // Bash é o padrão das distros; quem usa zsh troca o perfil nas configurações.
                    Flavor = ShellFlavor.Bash,
                    Executable = wsl,
                    Distro = distro,
                });
            }
        }

        list.AddRange(custom);
        Profiles = list;
        return list;
    }

    public ShellProfile? Find(string id) => Profiles.FirstOrDefault(p => p.Id == id);

    /// <summary>O shell da aba nova: o escolhido nas configurações, ou o primeiro que existir.</summary>
    public ShellProfile? Default(string preferredId) => Find(preferredId) ?? Profiles.FirstOrDefault();

    private string? Existing(string path) => probe.FileExists(path) ? path : null;

    // Caminhos do Windows montados sempre com "\", mesmo quando o Core roda nos testes em Linux.
    private static string Win(params string[] parts) => string.Join('\\', parts.Select(p => p.TrimEnd('\\')));
}
