using Forge.Core.Models;
using Forge.Core.Shells;

namespace Forge.Core.Tests;

public class ShellsTests
{
    [Fact]
    public void Lista_do_wsl_em_UTF16_lida_como_texto_comum()
    {
        // Saída de "wsl -l -q" lida sem a codificação certa: nulos entre as letras e BOM.
        var raw = "\uFEFFU\0b\0u\0n\0t\0u\0\r\0\n\0D\0e\0b\0i\0a\0n\0\r\0\n\0d\0o\0c\0k\0e\0r\0-\0d\0e\0s\0k\0t\0o\0p\0\r\0\n\0";

        Assert.Equal(new[] { "Ubuntu", "Debian" }, WslListParser.Parse(raw));
    }

    [Theory]
    [InlineData(@"C:\dev\cliente-a\web", "/mnt/c/dev/cliente-a/web")]
    [InlineData(@"D:\", "/mnt/d")]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\dev\api", "/home/dev/api")]
    [InlineData(@"\\wsl$\Ubuntu\home\dev", "/home/dev")]
    [InlineData("/home/dev/api", "/home/dev/api")]
    [InlineData("~/api", "~/api")]
    public void Converte_pastas_para_o_WSL(string path, string expected) => Assert.Equal(expected, PathMapper.ToWsl(path));

    [Theory]
    [InlineData("/home/dev/api", @"\\wsl.localhost\Ubuntu\home\dev\api")]
    [InlineData("/mnt/c/dev/web", @"C:\dev\web")]
    [InlineData("/mnt/c", @"C:\")]
    public void Converte_pastas_do_WSL_para_o_Windows(string path, string expected) =>
        Assert.Equal(expected, PathMapper.ToWindows(path, "Ubuntu"));

    [Theory]
    [InlineData(@"C:\Users\dev\AppData\Roaming\Forge\shell\tab-1.bash", "/c/Users/dev/AppData/Roaming/Forge/shell/tab-1.bash")]
    [InlineData("~", "~")]
    public void Converte_pastas_para_o_Git_Bash(string path, string expected) => Assert.Equal(expected, PathMapper.ToMsys(path));

    [Theory]
    [InlineData("file://pc/home/dev/meu%20projeto", "/home/dev/meu projeto")]
    [InlineData("file:///C:/dev/web", @"C:\dev\web")]
    [InlineData("file:////wsl.localhost/Debian/home/dev", @"\\wsl.localhost\Debian\home\dev")]
    [InlineData("file://pc//wsl.localhost/Debian/home/dev", @"\\wsl.localhost\Debian\home\dev")]
    [InlineData("http://pc/x", null)]
    public void Le_a_pasta_do_OSC_7(string uri, string? expected) => Assert.Equal(expected, PathMapper.FromFileUri(uri));

    [Fact]
    public async Task Detecta_os_shells_instalados()
    {
        var probe = new FakeProbe(
            existing:
            [
                @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                @"C:\Windows\System32\cmd.exe",
                @"C:\Program Files\Git\bin\bash.exe",
                @"C:\Windows\System32\wsl.exe",
            ],
            onPath: new() { ["pwsh.exe"] = @"C:\Program Files\PowerShell\7\pwsh.exe" },
            distros: ["Ubuntu", "Debian"]);
        var catalog = new ShellCatalog(probe);

        var profiles = await catalog.DetectAsync([]);

        Assert.Equal(
            new[]
            {
                "pwsh", "powershell", "cmd", "gitbash",
                "wsl:Ubuntu", "wsl:Ubuntu:pwsh", "wsl:Ubuntu:powershell", "wsl:Ubuntu:cmd", "wsl:Ubuntu:gitbash",
                "wsl:Debian", "wsl:Debian:pwsh", "wsl:Debian:powershell", "wsl:Debian:cmd", "wsl:Debian:gitbash",
            },
            profiles.Select(p => p.Id));
        Assert.Equal("Ubuntu", catalog.Find("wsl:Ubuntu")!.Distro);
        Assert.Equal("bash (padrão)", catalog.Find("wsl:Ubuntu")!.Terminal);
        Assert.Equal("pwsh", catalog.Default("nao-existe")!.Id);
        Assert.Equal(new[] { "Windows", "WSL · Ubuntu", "WSL · Debian" }, catalog.Environments);
    }

    [Fact]
    public async Task Distro_lida_oferece_os_shells_do_Linux_e_os_terminais_do_Windows_na_pasta_dela()
    {
        var probe = new FakeProbe(
            existing: [@"C:\Windows\System32\cmd.exe", @"C:\Windows\System32\wsl.exe"],
            onPath: new() { ["pwsh.exe"] = @"C:\Program Files\PowerShell\7\pwsh.exe" },
            distros: ["Debian"]);
        var catalog = new ShellCatalog(probe);
        var info = new Dictionary<string, WslDistroInfo> { ["Debian"] = new("/home/dev", "zsh", ["zsh", "bash", "fish"]) };

        await catalog.DetectAsync([], info);

        var debian = catalog.Profiles.Where(p => p.Environment == "WSL · Debian").ToList();
        Assert.Equal(
            new[] { "zsh (padrão)", "bash", "fish", "PowerShell 7 (Windows)", "Prompt de Comando (Windows)" },
            debian.Select(p => p.Terminal));
        var zsh = catalog.Find("wsl:Debian")!;
        Assert.Equal((ShellKind.Wsl, ShellFlavor.Zsh, "zsh", "/home/dev"), (zsh.Kind, zsh.Flavor, zsh.LinuxShell, zsh.WslHome));
        Assert.Equal(ShellFlavor.Unknown, catalog.Find("wsl:Debian:fish")!.Flavor);
        var pwsh = catalog.Find("wsl:Debian:pwsh")!;
        Assert.Equal((ShellKind.PowerShell, "pwsh", "Debian", @"C:\Windows\System32\wsl.exe"), (pwsh.Kind, pwsh.SettingsId, pwsh.Distro, pwsh.WslExecutable));
        Assert.False(pwsh.IsWsl);
        Assert.True(pwsh.InWsl);
    }

    [Fact]
    public void Le_os_shells_e_a_home_de_dentro_da_distro()
    {
        var output = "# /etc/shells: valid login shells\n/bin/sh\n/bin/bash\n/usr/bin/bash\n/bin/rbash\n/usr/bin/dash\n/usr/bin/zsh\n/bin/zsh\n/usr/bin/fish\n/usr/bin/tmux\n---\ndev:x:1000:1000:Dev,,,:/home/dev:/usr/bin/zsh\n";

        var info = WslShellsParser.Parse(output)!;

        Assert.Equal("/home/dev", info.Home);
        Assert.Equal("zsh", info.DefaultShell);
        Assert.Equal(new[] { "zsh", "bash", "fish" }, info.Shells);
    }

    [Fact]
    public void Shells_das_distros_ficam_no_settings_json()
    {
        using var temp = new TempFolder();
        var store = new Forge.Core.Persistence.SettingsStore(temp.File("settings.json"));
        store.Update(s => s.WslDistros["Debian"] = new WslDistroInfo("/home/dev", "zsh", ["zsh", "bash"]));

        var reloaded = new Forge.Core.Persistence.SettingsStore(temp.File("settings.json"));
        reloaded.Load();

        var info = reloaded.Current.WslDistros["Debian"];
        Assert.Equal(("/home/dev", "zsh"), (info.Home, info.DefaultShell));
        Assert.Equal(new[] { "zsh", "bash" }, info.Shells);
    }

    [Fact]
    public void Saida_sem_separador_nao_e_da_distro()
    {
        Assert.Null(WslShellsParser.Parse("wsl: erro ao iniciar a distro"));
        Assert.Equal("bash", WslShellsParser.Parse("/bin/bash\n---\n")!.DefaultShell);
    }

    private sealed class FakeProbe(string[] existing, Dictionary<string, string> onPath, string[] distros) : IShellProbe
    {
        public string SystemRoot => @"C:\Windows";

        public string ProgramFiles => @"C:\Program Files";

        public bool FileExists(string path) => existing.Contains(path, StringComparer.OrdinalIgnoreCase);

        public string? FindOnPath(string exe) => onPath.GetValueOrDefault(exe);

        public string? GitInstallPath() => null;

        public Task<IReadOnlyList<string>> ListWslDistrosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(distros);

        public Task<WslDistroInfo?> DescribeWslAsync(string wslExecutable, string distro, CancellationToken ct) => Task.FromResult<WslDistroInfo?>(null);
    }
}
