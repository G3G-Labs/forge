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

    /// <summary>Roda <see cref="WslShellsParser.Script"/> dentro da distro (liga a distro, se estiver parada).</summary>
    Task<WslDistroInfo?> DescribeWslAsync(string wslExecutable, string distro, CancellationToken ct);
}

/// <summary>
/// Os ambientes e seus terminais. Windows: PowerShell 7 e 5.1, CMD e Git Bash. Cada distro WSL: os shells do Linux
/// instalados nela (o de login primeiro) e os terminais do Windows abertos na pasta da distro. Por último, os perfis
/// personalizados das configurações.
/// </summary>
public sealed class ShellCatalog(IShellProbe probe)
{
    private List<ShellProfile> _windows = [];
    private List<ShellProfile> _custom = [];
    private IReadOnlyDictionary<string, WslDistroInfo> _wslInfo = new Dictionary<string, WslDistroInfo>();
    private string? _wsl;

    public IReadOnlyList<ShellProfile> Profiles { get; private set; } = [];

    public IReadOnlyList<string> Distros { get; private set; } = [];

    /// <summary>A lista mudou (shells de uma distro detectados).</summary>
    public event EventHandler? Changed;

    /// <summary>Os ambientes na ordem da lista: "Windows", "WSL · Debian"…</summary>
    public IReadOnlyList<string> Environments => Profiles.Select(p => p.Environment).Distinct().ToList();

    public async Task<IReadOnlyList<ShellProfile>> DetectAsync(
        IEnumerable<ShellProfile> custom,
        IReadOnlyDictionary<string, WslDistroInfo>? wslInfo = null,
        CancellationToken ct = default)
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

        _windows = list;
        _wsl = Existing(Win(system32, "wsl.exe"));
        Distros = _wsl is null ? [] : await probe.ListWslDistrosAsync(ct).ConfigureAwait(false);
        _custom = custom.ToList();
        _wslInfo = wslInfo ?? new Dictionary<string, WslDistroInfo>();
        Compose();
        return Profiles;
    }

    /// <summary>Lê os shells e a home de uma distro, por dentro dela. Null se ela não responder.</summary>
    public Task<WslDistroInfo?> DescribeAsync(string distro, CancellationToken ct = default) =>
        _wsl is null ? Task.FromResult<WslDistroInfo?>(null) : probe.DescribeWslAsync(_wsl, distro, ct);

    /// <summary>Remonta a lista com o que se sabe agora das distros (sem detectar o Windows de novo).</summary>
    public void Update(IReadOnlyDictionary<string, WslDistroInfo> wslInfo)
    {
        _wslInfo = wslInfo;
        Compose();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Compose()
    {
        var list = new List<ShellProfile>(_windows);
        foreach (var distro in Distros)
            list.AddRange(DistroProfiles(distro, _wslInfo.GetValueOrDefault(distro)));
        list.AddRange(_custom);
        Profiles = list;
    }

    private IEnumerable<ShellProfile> DistroProfiles(string distro, WslDistroInfo? info)
    {
        // Antes da primeira detecção, a distro oferece o bash (o padrão de quase todas).
        var defaultShell = info?.DefaultShell ?? "bash";
        foreach (var shell in info?.Shells ?? ["bash"])
        {
            var isDefault = shell == defaultShell;
            yield return new ShellProfile
            {
                Id = isDefault ? ShellIds.Wsl(distro) : ShellIds.Wsl(distro, shell),
                Name = $"{distro} · {shell}",
                Terminal = isDefault ? $"{shell} (padrão)" : shell,
                Kind = ShellKind.Wsl,
                Flavor = shell switch
                {
                    "bash" => ShellFlavor.Bash,
                    "zsh" => ShellFlavor.Zsh,
                    _ => ShellFlavor.Unknown,
                },
                Executable = _wsl!,
                WslExecutable = _wsl,
                LinuxShell = shell,
                Distro = distro,
                WslHome = info?.Home,
            };
        }

        // Os terminais do Windows na pasta da distro: para ferramentas instaladas só no Windows.
        foreach (var windows in _windows)
        {
            yield return windows with
            {
                Id = ShellIds.Wsl(distro, windows.Id),
                Name = $"{distro} · {windows.Name}",
                Terminal = windows.Name + " (Windows)",
                SettingsId = windows.Id,
                Distro = distro,
                WslExecutable = _wsl,
                WslHome = info?.Home,
            };
        }
    }

    public ShellProfile? Find(string id) => Profiles.FirstOrDefault(p => p.Id == id);

    /// <summary>O shell da aba nova: o escolhido nas configurações, ou o primeiro que existir.</summary>
    public ShellProfile? Default(string preferredId) => Find(preferredId) ?? Profiles.FirstOrDefault();

    private string? Existing(string path) => probe.FileExists(path) ? path : null;

    // Caminhos do Windows montados sempre com "\", mesmo quando o Core roda nos testes em Linux.
    private static string Win(params string[] parts) => string.Join('\\', parts.Select(p => p.TrimEnd('\\')));
}
