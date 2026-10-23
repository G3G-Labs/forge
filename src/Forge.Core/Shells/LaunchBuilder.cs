using System.Text.RegularExpressions;
using Forge.Core.Models;
using Forge.Core.Util;

namespace Forge.Core.Shells;

/// <summary>
/// Monta a linha de comando e os scripts de uma aba. O shell abre interativo, já na pasta e com as variáveis;
/// o comando de inicialização não entra aqui: ele é digitado no terminal pelo app quando o shell fica pronto,
/// igual ao usuário faria, e vai para o histórico do próprio shell.
/// </summary>
public sealed partial class LaunchBuilder(string scriptsFolder)
{
    public LaunchPlan Build(
        WorkspaceTab tab,
        ShellProfile shell,
        AppSettings settings,
        string? groupFolder,
        string fallbackUser,
        string fallbackMachine)
    {
        var cwd = ResolveCwd(tab, groupFolder, settings.FolderOnRestore);
        var integration = settings.Helpers.ShellIntegration
            && shell.Kind != ShellKind.Custom
            && shell.SupportsIntegration
            && !settings.IntegrationDisabled.Contains(shell.Id);

        var identity = settings.Identity;
        var inject = shell.Flavor == ShellFlavor.PowerShell ? identity.InjectInPowerShellPrompt : identity.InjectInBashPrompt;
        string? user = null, machine = null;
        if (inject)
        {
            user = string.IsNullOrWhiteSpace(identity.UserName) ? fallbackUser : identity.UserName;
            machine = string.IsNullOrWhiteSpace(identity.MachineName) ? fallbackMachine : identity.MachineName;
        }

        ScriptOptions Options(string? shellCwd) => new(
            tab.Id,
            shellCwd,
            ValidEnv(tab.Env),
            integration,
            settings.Helpers.CaseInsensitiveCompletion,
            settings.Helpers.FlushHistoryEachCommand,
            user,
            machine);

        return (shell.Kind, shell.Flavor) switch
        {
            (ShellKind.Custom, _) => Raw(shell),
            (_, ShellFlavor.PowerShell) => PowerShell(tab, shell, Options(cwd)),
            (ShellKind.Wsl, ShellFlavor.Zsh) => WslZsh(tab, shell, Options(cwd is null ? null : PathMapper.ToWsl(cwd))),
            (ShellKind.Wsl, _) => WslBash(tab, shell, Options(cwd is null ? null : PathMapper.ToWsl(cwd))),
            (ShellKind.GitBash, _) => GitBash(tab, shell, Options(cwd is null ? null : PathMapper.ToMsys(cwd))),
            (ShellKind.Cmd, _) => Cmd(tab, shell, Options(cwd)),
            _ => Raw(shell),
        };
    }

    /// <summary>A pasta inicial da aba, a última visitada (se a opção estiver ligada) ou a do grupo.</summary>
    public static string? ResolveCwd(WorkspaceTab tab, string? groupFolder, FolderOnRestore mode)
    {
        if (mode == FolderOnRestore.LastVisited && !string.IsNullOrWhiteSpace(tab.LastCwd))
            return tab.LastCwd;
        if (!string.IsNullOrWhiteSpace(tab.Cwd))
            return tab.Cwd;
        return string.IsNullOrWhiteSpace(groupFolder) ? null : groupFolder;
    }

    /// <summary>Só nomes válidos em todos os shells (letras, dígitos e _ sem começar por dígito). O resto é ignorado.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> ValidEnv(IReadOnlyDictionary<string, string> env) =>
        env.Where(kv => EnvName().IsMatch(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();

    private LaunchPlan PowerShell(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var script = ScriptPath(tab, "ps1");
        var args = new[] { "-NoLogo", "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", ". " + ShellScripts.PsQuote(script) };
        return new LaunchPlan(CommandLine.Join(shell.Executable, args), [new ScriptFile(script, ShellScripts.PowerShell(o), Utf8Bom: true)], o.Integration);
    }

    private LaunchPlan WslBash(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var script = ScriptPath(tab, "bash");
        var args = new[] { "-d", shell.Distro!, "--cd", "~", "--", "bash", "--rcfile", PathMapper.ToWsl(script), "-i" };
        return new LaunchPlan(CommandLine.Join(shell.Executable, args), [new ScriptFile(script, ShellScripts.Bash(o))], o.Integration);
    }

    private LaunchPlan WslZsh(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var folder = scriptsFolder.TrimEnd('\\') + @"\zsh-" + tab.Id;
        var args = new[] { "-d", shell.Distro!, "--cd", "~", "--", "env", "ZDOTDIR=" + PathMapper.ToWsl(folder), "zsh", "-i" };
        return new LaunchPlan(
            CommandLine.Join(shell.Executable, args),
            [
                new ScriptFile(folder + @"\.zshenv", ShellScripts.ZshEnv(tab.Id)),
                new ScriptFile(folder + @"\.zshrc", ShellScripts.Zsh(o)),
            ],
            o.Integration);
    }

    private LaunchPlan GitBash(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var script = ScriptPath(tab, "bash");
        var args = new[] { "--rcfile", PathMapper.ToMsys(script), "-i" };
        return new LaunchPlan(CommandLine.Join(shell.Executable, args), [new ScriptFile(script, ShellScripts.Bash(o))], o.Integration);
    }

    private LaunchPlan Cmd(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var script = ScriptPath(tab, "cmd");
        return new LaunchPlan(CommandLine.Join(shell.Executable, ["/k", script]), [new ScriptFile(script, ShellScripts.Cmd(o))], Integration: false);
    }

    /// <summary>Perfil personalizado: o executável e os argumentos do usuário, sem script nem integração.</summary>
    private static LaunchPlan Raw(ShellProfile shell)
    {
        var line = CommandLine.Quote(shell.Executable);
        if (!string.IsNullOrWhiteSpace(shell.Arguments))
            line += " " + shell.Arguments.Trim();
        return new LaunchPlan(line, [], Integration: false);
    }

    private string ScriptPath(WorkspaceTab tab, string extension) =>
        scriptsFolder.TrimEnd('\\') + @"\tab-" + tab.Id + "." + extension;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvName();
}
