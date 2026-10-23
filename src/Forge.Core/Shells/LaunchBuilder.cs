using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Forge.Core.Models;
using Forge.Core.Util;

namespace Forge.Core.Shells;

/// <summary>
/// Monta a linha de comando e os scripts de uma aba. O shell abre interativo, já na pasta e com as variáveis;
/// o comando de inicialização não entra aqui: ele é digitado no terminal pelo app quando o shell fica pronto,
/// igual ao usuário faria, e vai para o histórico do próprio shell.
/// Cada plano ganha um nonce novo (<see cref="LaunchPlan.Nonce"/>); <paramref name="nonce"/> é injetável para os testes.
/// </summary>
public sealed partial class LaunchBuilder(string scriptsFolder, Func<string>? nonce = null)
{
    private readonly Func<string> _nonce = nonce ?? (() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));

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
            && !settings.IntegrationDisabled.Contains(shell.SettingsId);

        var mask = settings.Identity.MaskPrompt ? SanitizeMask(settings.Identity.Display(fallbackUser, fallbackMachine)) : null;
        // Toda sessão tem nonce, mesmo sem integração: aí nada o emite e as sequências forjadas são todas ignoradas.
        var sessionNonce = _nonce();

        // Sem pasta, a aba abre na home do usuário (e não na pasta de onde o Forge foi aberto).
        ScriptOptions Options(string? shellCwd, string home) => new(
            tab.Id,
            shellCwd ?? home,
            ValidEnv(tab.Env),
            integration,
            settings.Helpers.CaseInsensitiveCompletion,
            settings.Helpers.FlushHistoryEachCommand,
            mask,
            sessionNonce);

        // Terminal do Windows numa distro: a pasta é a da distro, vista pelo Windows (\\wsl.localhost\Debian\…).
        if (shell.InWsl && !shell.IsWsl && shell.Kind != ShellKind.Custom)
            cwd = WindowsFolderInDistro(cwd, shell);

        return (shell.Kind, shell.Flavor) switch
        {
            (ShellKind.Custom, _) => Raw(shell, sessionNonce),
            (_, ShellFlavor.PowerShell) => PowerShell(tab, shell, Options(cwd, "~")),
            (ShellKind.Wsl, ShellFlavor.Zsh) => WslZsh(tab, shell, Options(cwd is null ? null : PathMapper.ToWsl(cwd), "~")),
            (ShellKind.Wsl, ShellFlavor.Bash) => WslBash(tab, shell, Options(cwd is null ? null : PathMapper.ToWsl(cwd), "~") with { LoginProfile = true }),
            (ShellKind.Wsl, _) => WslOther(shell, cwd, sessionNonce),
            (ShellKind.GitBash, _) => GitBash(tab, shell, Options(cwd is null ? null : PathMapper.ToMsys(cwd), "~")),
            (ShellKind.Cmd, _) => Cmd(tab, shell, Options(cwd, "%USERPROFILE%")),
            _ => Raw(shell, sessionNonce),
        };
    }

    /// <summary>Onde a aba abre: a última pasta visitada (padrão) ou a pasta fixa dela. null: a home.</summary>
    public static string? ResolveCwd(WorkspaceTab tab, FolderOnRestore mode)
    {
        if (mode == FolderOnRestore.LastVisited && !string.IsNullOrWhiteSpace(tab.LastCwd))
            return tab.LastCwd;
        return string.IsNullOrWhiteSpace(tab.Cwd) ? null : tab.Cwd;
    }

    /// <summary>
    /// Pasta de um terminal do Windows aberto numa distro: /home/dev/api e ~/api viram \\wsl.localhost\Debian\home\dev\api;
    /// sem pasta, a home da distro (ou a raiz, se a home ainda não foi detectada). Pastas do Windows ficam como estão.
    /// </summary>
    internal static string? WindowsFolderInDistro(string? cwd, ShellProfile shell)
    {
        var linux = cwd is null || PathMapper.IsLinuxPath(cwd) ? ExpandHome(cwd ?? "~", shell.WslHome) : null;
        return linux is null ? cwd : PathMapper.ToWindows(linux, shell.Distro!);
    }

    // "~" e "~/api" com a home da distro; sem ela, a raiz.
    private static string ExpandHome(string path, string? home)
    {
        if (path != "~" && !path.StartsWith("~/", StringComparison.Ordinal))
            return path;
        return home is null ? "/" : home.TrimEnd('/') + path[1..];
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
        return new LaunchPlan(CommandLine.Join(shell.Executable, args), [new ScriptFile(script, ShellScripts.PowerShell(o), Utf8Bom: true)], o.Integration, o.Nonce);
    }

    private LaunchPlan WslBash(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var script = ScriptPath(tab, "bash");
        var args = new[] { "-d", shell.Distro!, "--cd", "~", "--", shell.LinuxShell, "--rcfile", PathMapper.ToWsl(script), "-i" };
        return new LaunchPlan(CommandLine.Join(shell.Executable, args), [new ScriptFile(script, ShellScripts.Bash(o))], o.Integration, o.Nonce);
    }

    private LaunchPlan WslZsh(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var folder = scriptsFolder.TrimEnd('\\') + @"\zsh-" + tab.Id;
        // -l: shell de login, como o Windows Terminal abre a distro (carrega o .zprofile).
        var args = new[] { "-d", shell.Distro!, "--cd", "~", "--", "env", "ZDOTDIR=" + PathMapper.ToWsl(folder), shell.LinuxShell, "-l", "-i" };
        return new LaunchPlan(
            CommandLine.Join(shell.Executable, args),
            [
                new ScriptFile(folder + @"\.zshenv", ShellScripts.ZshEnv(tab.Id)),
                new ScriptFile(folder + @"\.zprofile", ShellScripts.ZProfile(tab.Id)),
                new ScriptFile(folder + @"\.zshrc", ShellScripts.Zsh(o)),
            ],
            o.Integration,
            o.Nonce);
    }

    private LaunchPlan GitBash(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var script = ScriptPath(tab, "bash");
        var args = new[] { "--rcfile", PathMapper.ToMsys(script), "-i" };
        return new LaunchPlan(CommandLine.Join(shell.Executable, args), [new ScriptFile(script, ShellScripts.Bash(o))], o.Integration, o.Nonce);
    }

    private LaunchPlan Cmd(WorkspaceTab tab, ShellProfile shell, ScriptOptions o)
    {
        var script = ScriptPath(tab, "cmd");
        return new LaunchPlan(CommandLine.Join(shell.Executable, ["/k", script]), [new ScriptFile(script, ShellScripts.Cmd(o))], Integration: false, o.Nonce);
    }

    /// <summary>
    /// Outros shells do Linux (fish, nu…): abrem como shell de login, direto na pasta da aba, sem script nem integração.
    /// </summary>
    private static LaunchPlan WslOther(ShellProfile shell, string? cwd, string nonce)
    {
        var folder = ExpandHome(cwd is null ? "~" : PathMapper.ToWsl(cwd), shell.WslHome);
        // Sem a home detectada, "~/api" vira "/"; melhor abrir na home de verdade.
        if (cwd is not null && folder == "/" && PathMapper.ToWsl(cwd) != "/")
            folder = "~";
        var args = new[] { "-d", shell.Distro!, "--cd", folder, "--", shell.LinuxShell, "-l" };
        return new LaunchPlan(CommandLine.Join(shell.Executable, args), [], Integration: false, nonce);
    }

    /// <summary>Perfil personalizado: o executável e os argumentos do usuário, sem script nem integração.</summary>
    private static LaunchPlan Raw(ShellProfile shell, string nonce)
    {
        var line = CommandLine.Quote(shell.Executable);
        if (!string.IsNullOrWhiteSpace(shell.Arguments))
            line += " " + shell.Arguments.Trim();
        return new LaunchPlan(line, [], Integration: false, nonce);
    }

    private string ScriptPath(WorkspaceTab tab, string extension) =>
        scriptsFolder.TrimEnd('\\') + @"\tab-" + tab.Id + "." + extension;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvName();
}
