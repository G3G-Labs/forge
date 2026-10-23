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
        string fallbackUser,
        string fallbackMachine)
    {
        var cwd = ResolveCwd(tab, settings.FolderOnRestore);
        var integration = settings.Helpers.ShellIntegration
            && shell.Kind != ShellKind.Custom
            && shell.SupportsIntegration
            && !settings.IntegrationDisabled.Contains(shell.Id);

        var mask = settings.Identity.MaskPrompt ? SanitizeMask(settings.Identity.Display(fallbackUser, fallbackMachine)) : null;

        // Sem pasta, a aba abre na home do usuário (e não na pasta de onde o Forge foi aberto).
        ScriptOptions Options(string? shellCwd, string home) => new(
            tab.Id,
            shellCwd ?? home,
            ValidEnv(tab.Env),
            integration,
            settings.Helpers.CaseInsensitiveCompletion,
            settings.Helpers.FlushHistoryEachCommand,
            mask);

        return (shell.Kind, shell.Flavor) switch
        {
            (ShellKind.Custom, _) => Raw(shell),
            (_, ShellFlavor.PowerShell) => PowerShell(tab, shell, Options(cwd, "~")),
            (ShellKind.Wsl, ShellFlavor.Zsh) => WslZsh(tab, shell, Options(cwd is null ? null : PathMapper.ToWsl(cwd), "~")),
            (ShellKind.Wsl, _) => WslBash(tab, shell, Options(cwd is null ? null : PathMapper.ToWsl(cwd), "~")),
            (ShellKind.GitBash, _) => GitBash(tab, shell, Options(cwd is null ? null : PathMapper.ToMsys(cwd), "~")),
            (ShellKind.Cmd, _) => Cmd(tab, shell, Options(cwd, "%USERPROFILE%")),
            _ => Raw(shell),
        };
    }

    /// <summary>Onde a aba abre: a última pasta visitada (padrão) ou a pasta fixa dela. null: a home.</summary>
    public static string? ResolveCwd(WorkspaceTab tab, FolderOnRestore mode)
    {
        if (mode == FolderOnRestore.LastVisited && !string.IsNullOrWhiteSpace(tab.LastCwd))
            return tab.LastCwd;
        return string.IsNullOrWhiteSpace(tab.Cwd) ? null : tab.Cwd;
    }

    /// <summary>A máscara vai crua para dentro do prompt de três shells: só letras, dígitos e @ . _ -.</summary>
    internal static string SanitizeMask(string mask)
    {
        var clean = new string(mask.Trim().Replace(' ', '-')
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '@' or '.' or '_' or '-').ToArray());
        while (clean.Contains("--", StringComparison.Ordinal))
            clean = clean.Replace("--", "-", StringComparison.Ordinal);
        clean = clean.Trim('-');
        return clean.Length == 0 ? "forge" : clean;
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
