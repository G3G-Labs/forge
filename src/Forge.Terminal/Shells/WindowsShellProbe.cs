using System.Text;
using Forge.Core.Processes;
using Forge.Core.Shells;
using Microsoft.Win32;

namespace Forge.Terminal.Shells;

/// <summary>Onde os shells estão nesta máquina: pastas do sistema, PATH, registro do Git e <c>wsl -l -q</c>.</summary>
public sealed class WindowsShellProbe(IProcessRunner runner) : IShellProbe
{
    public string SystemRoot { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public string ProgramFiles { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    public bool FileExists(string path) => File.Exists(path);

    public string? FindOnPath(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(Environment.ExpandEnvironmentVariables(dir), exe);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (ArgumentException)
            {
                // Entrada inválida no PATH: segue para a próxima.
            }
        }
        return null;
    }

    public string? GitInstallPath()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GitForWindows");
        return key?.GetValue("InstallPath") as string;
    }

    public async Task<IReadOnlyList<string>> ListWslDistrosAsync(CancellationToken ct)
    {
        var wsl = Path.Combine(SystemRoot, "System32", "wsl.exe");
        if (!File.Exists(wsl))
            return [];
        try
        {
            // O wsl.exe responde em UTF-16.
            var result = await runner.RunAsync(wsl, ["-l", "-q"], Encoding.Unicode, ct).ConfigureAwait(false);
            return result.ExitCode == 0 ? WslListParser.Parse(result.Output) : [];
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return [];
        }
    }
}
