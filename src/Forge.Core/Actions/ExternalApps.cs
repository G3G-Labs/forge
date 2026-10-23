using Forge.Core.Models;
using Forge.Core.Processes;
using Forge.Core.Shells;
using Forge.Core.Util;

namespace Forge.Core.Actions;

/// <summary>Ações de um clique que saem do Forge: abrir a pasta da aba no VS Code e no Explorer.</summary>
public sealed class ExternalApps(IProcessRunner runner, IShellProbe probe)
{
    // O "code" do PATH é bin\code.cmd; o executável fica uma pasta acima.
    private static readonly (string Script, string Executable)[] VsCodeEditions =
    [
        ("code.cmd", "Code.exe"),
        ("code-insiders.cmd", "Code - Insiders.exe"),
    ];

    /// <summary>
    /// Abre a pasta no VS Code. Em abas WSL, abre já conectado à distro (extensão WSL do VS Code), na pasta Linux.
    /// Chama o Code.exe direto: a pasta vem do shell (pode ter &amp;, ^, %…) e não pode passar pelo cmd.exe, onde viraria
    /// comando. Só sem o Code.exe à vista o code.cmd é usado, e aí pastas que o cmd não aceita com segurança são recusadas.
    /// </summary>
    /// <exception cref="InvalidOperationException">A pasta não pode ser aberta pelo code.cmd.</exception>
    public void OpenInVsCode(string folder, ShellProfile shell)
    {
        string[] args = shell.InWsl
            ? ["--remote", "wsl+" + shell.Distro, PathMapper.ToWsl(folder)]
            : [WindowsPath(folder, shell)];

        if (VsCodeExecutable() is { } code)
        {
            runner.Launch(code, args);
            return;
        }
        if (!args.All(CommandLine.IsSafeForCmd))
            throw new InvalidOperationException("O VS Code não foi encontrado e esta pasta tem caracteres que o code.cmd não aceita com segurança (aspas ou %).");
        // O code.cmd é um .cmd: roda pelo cmd.exe, sem janela. As aspas do Quote protegem & | < > ^ ( ).
        runner.Launch("cmd.exe", ["/c", "code", .. args], hidden: true);
    }

    public void OpenInExplorer(string folder, ShellProfile shell) =>
        runner.Launch("explorer.exe", [WindowsPath(folder, shell)]);

    /// <summary>O Code.exe da instalação cujo bin\code.cmd está no PATH, ou null.</summary>
    internal string? VsCodeExecutable()
    {
        foreach (var (script, executable) in VsCodeEditions)
        {
            if (probe.FindOnPath(script) is not { } found)
                continue;
            var bin = Parent(found);
            var root = bin is null ? null : Parent(bin);
            if (root is not null && probe.FileExists(root + @"\" + executable))
                return root + @"\" + executable;
        }
        return null;
    }

    // Pasta de cima com "\" (o Core também roda nos testes em Linux, onde Path não entende "\").
    private static string? Parent(string path)
    {
        var slash = path.TrimEnd('\\').LastIndexOf('\\');
        return slash > 0 ? path[..slash] : null;
    }

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
