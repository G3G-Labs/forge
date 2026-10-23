using System.Text;
using Forge.Core.Actions;
using Forge.Core.Models;
using Forge.Core.Processes;
using Forge.Core.Shells;
using Forge.Core.Util;

namespace Forge.Core.Tests;

public class ExternalAppsTests
{
    private const string CodeCmd = @"C:\Users\dev\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd";
    private const string CodeExe = @"C:\Users\dev\AppData\Local\Programs\Microsoft VS Code\Code.exe";

    private static readonly ShellProfile Pwsh = new() { Id = "pwsh", Name = "PowerShell 7", Kind = ShellKind.PowerShell, Flavor = ShellFlavor.PowerShell, Executable = "pwsh.exe" };
    private static readonly ShellProfile Ubuntu = new() { Id = "wsl:Ubuntu", Name = "Ubuntu", Kind = ShellKind.Wsl, Flavor = ShellFlavor.Bash, Executable = "wsl.exe", Distro = "Ubuntu" };

    [Fact]
    public void VS_Code_abre_pelo_Code_exe_sem_passar_pelo_cmd()
    {
        var runner = new FakeRunner();
        var apps = new ExternalApps(runner, new FakeProbe(onPath: CodeCmd, existing: CodeExe));

        apps.OpenInVsCode(@"C:\proj&calc", Pwsh);

        var (file, args) = Assert.Single(runner.Launched);
        Assert.Equal(CodeExe, file);
        Assert.Equal([@"C:\proj&calc"], args);
    }

    [Fact]
    public void VS_Code_numa_aba_WSL_abre_conectado_a_distro()
    {
        var runner = new FakeRunner();
        var apps = new ExternalApps(runner, new FakeProbe(onPath: CodeCmd, existing: CodeExe));

        apps.OpenInVsCode("/home/dev/api", Ubuntu);

        Assert.Equal(["--remote", "wsl+Ubuntu", "/home/dev/api"], Assert.Single(runner.Launched).Args);
    }

    [Fact]
    public void Sem_o_Code_exe_o_code_cmd_recebe_a_pasta_entre_aspas()
    {
        var runner = new FakeRunner();
        var apps = new ExternalApps(runner, new FakeProbe(onPath: null, existing: null));

        apps.OpenInVsCode(@"C:\proj&calc", Pwsh);

        var (file, args) = Assert.Single(runner.Launched);
        Assert.Equal("cmd.exe /c code \"C:\\proj&calc\"", CommandLine.Join(file, args));
    }

    [Theory]
    [InlineData(@"C:\a%PATH%b")]
    [InlineData("/home/dev/a\"&calc")]
    public void Sem_o_Code_exe_pasta_que_o_cmd_interpretaria_e_recusada(string folder)
    {
        var runner = new FakeRunner();
        var apps = new ExternalApps(runner, new FakeProbe(onPath: null, existing: null));

        Assert.Throws<InvalidOperationException>(() => apps.OpenInVsCode(folder, folder.StartsWith('/') ? Ubuntu : Pwsh));
        Assert.Empty(runner.Launched);
    }

    [Theory]
    [InlineData(@"C:\proj&calc", "\"C:\\proj&calc\"")]
    [InlineData("a|b", "\"a|b\"")]
    [InlineData("x^y", "\"x^y\"")]
    public void Caracteres_especiais_do_cmd_ganham_aspas(string arg, string expected) =>
        Assert.Equal(expected, CommandLine.Quote(arg));

    private sealed class FakeRunner : IProcessRunner
    {
        public List<(string File, IReadOnlyList<string> Args)> Launched { get; } = [];

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> args, Encoding? encoding = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public void Launch(string fileName, IReadOnlyList<string> args, bool hidden = false) => Launched.Add((fileName, args));
    }

    private sealed class FakeProbe(string? onPath, string? existing) : IShellProbe
    {
        public string SystemRoot => @"C:\Windows";

        public string ProgramFiles => @"C:\Program Files";

        public bool FileExists(string path) => path == existing;

        public string? FindOnPath(string exe) => exe == "code.cmd" ? onPath : null;

        public string? GitInstallPath() => null;

        public Task<IReadOnlyList<string>> ListWslDistrosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<WslDistroInfo?> DescribeWslAsync(string wslExecutable, string distro, CancellationToken ct) => Task.FromResult<WslDistroInfo?>(null);
    }
}
