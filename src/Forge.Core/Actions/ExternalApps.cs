using Forge.Core.Models;
using Forge.Core.Processes;
using Forge.Core.Shells;

namespace Forge.Core.Actions;

/// <summary>Ações de um clique que saem do Forge: abrir a pasta da aba no VS Code e no Explorer.</summary>
public sealed class ExternalApps(IProcessRunner runner)
{
    /// <summary>
    /// Abre a pasta no VS Code. Em abas WSL, abre já conectado à distro (extensão WSL do VS Code), na pasta Linux.
    /// O "code" do PATH é um .cmd: roda pelo cmd.exe, sem janela.
    /// </summary>
    public void OpenInVsCode(string folder, ShellProfile shell)
    {
        if (shell.InWsl)
            runner.Launch("cmd.exe", ["/c", "code", "--remote", "wsl+" + shell.Distro, PathMapper.ToWsl(folder)], hidden: true);
        else
            runner.Launch("cmd.exe", ["/c", "code", WindowsPath(folder, shell)], hidden: true);
    }

    public void OpenInExplorer(string folder, ShellProfile shell) =>
        runner.Launch("explorer.exe", [WindowsPath(folder, shell)]);

    /// <summary>A pasta como o Windows a enxerga, para o Explorer, o VS Code e o "Copiar caminho".</summary>
    public static string WindowsPath(string folder, ShellProfile shell)
    {
        if (shell.InWsl && PathMapper.IsLinuxPath(folder))
            return PathMapper.ToWindows(folder, shell.Distro!);
        if (shell.Kind == ShellKind.GitBash && folder.Length >= 2 && folder[0] == '/' && (folder.Length == 2 || folder[2] == '/'))
            return char.ToUpperInvariant(folder[1]) + @":\" + (folder.Length > 3 ? folder[3..].Replace('/', '\\') : "");
        return folder;
    }
}
