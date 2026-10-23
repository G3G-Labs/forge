using System.Text;

namespace Forge.Core.Shells;

/// <summary>O que vai no script de uma aba. <see cref="Cwd"/> já está no formato do shell.</summary>
public sealed record ScriptOptions(
    string TabId,
    string? Cwd,
    IReadOnlyList<KeyValuePair<string, string>> Env,
    bool Integration,
    bool CaseInsensitiveCompletion,
    bool FlushHistory,
    string? IdentityUser,
    string? IdentityMachine)
{
    public bool InjectIdentity => IdentityUser is not null && IdentityMachine is not null;
}

/// <summary>
/// Os scripts que o app gera para cada aba. Carregam o perfil original do usuário e só então acrescentam pasta,
/// variáveis, auxílios e a integração de shell (OSC 133). Nada é gravado em .bashrc, .zshrc, .inputrc ou no perfil
/// do PowerShell: o shell recebe o script por argumento (--rcfile, ZDOTDIR, -Command).
/// </summary>
public static class ShellScripts
{
    private const string Header = "Gerado pelo Forge para a aba {0}. É recriado a cada início: não edite.";

    public static string PowerShell(ScriptOptions o)
    {
        var sb = new StringBuilder();
        Line(sb, "# " + string.Format(Header, o.TabId), "\r\n");
        if (o.Cwd is { } cwd)
        {
            // ~ só é expandido sem -LiteralPath.
            var verb = cwd.StartsWith('~') ? "-Path" : "-LiteralPath";
            Line(sb, $"Set-Location {verb} {PsQuote(cwd)} -ErrorAction SilentlyContinue", "\r\n");
        }
        foreach (var (name, value) in o.Env)
            Line(sb, $"$env:{name} = {PsQuote(value)}", "\r\n");
        if (o.FlushHistory)
        {
            Line(sb, "if (Get-Module -ListAvailable -Name PSReadLine) {", "\r\n");
            Line(sb, "    Import-Module PSReadLine", "\r\n");
            Line(sb, "    Set-PSReadLineOption -HistorySaveStyle SaveIncrementally", "\r\n");
            Line(sb, "}", "\r\n");
        }

        var identity = o.InjectIdentity ? PsQuote($"{o.IdentityUser}@{o.IdentityMachine} ") : "''";
        if (o.Integration)
        {
            sb.Append(PowerShellIntegration.Replace("__FORGE_IDENTITY__", identity, StringComparison.Ordinal).ReplaceLineEndings("\r\n"));
        }
        else if (o.InjectIdentity)
        {
            Line(sb, "$global:__forgeOriginalPrompt = $function:prompt", "\r\n");
            Line(sb, $"function global:prompt {{ {identity} + (& $global:__forgeOriginalPrompt) }}", "\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Script para <c>bash --rcfile</c> (Git Bash e distros WSL).</summary>
    public static string Bash(ScriptOptions o)
    {
        var sb = new StringBuilder();
        Line(sb, "# " + string.Format(Header, o.TabId));
        Line(sb, "[ -f ~/.bashrc ] && . ~/.bashrc");
        if (o.Cwd is { } cwd)
            Line(sb, $"cd -- {ShCdTarget(cwd)} 2>/dev/null");
        foreach (var (name, value) in o.Env)
            Line(sb, $"export {name}={ShQuote(value)}");
        if (o.FlushHistory)
            Line(sb, "shopt -s histappend");
        if (o.CaseInsensitiveCompletion)
            Line(sb, "bind 'set completion-ignore-case on' 2>/dev/null");
        if (o.InjectIdentity)
        {
            Line(sb, $"PS1=${{PS1//\\\\u/{ShQuote(o.IdentityUser!)}}}");
            Line(sb, $"PS1=${{PS1//\\\\h/{ShQuote(o.IdentityMachine!)}}}");
        }

        if (o.Integration)
            sb.Append(BashIntegration.Replace("__FORGE_FLUSH__", o.FlushHistory ? "builtin history -a" : ":", StringComparison.Ordinal).ReplaceLineEndings("\n"));
        else if (o.FlushHistory)
            Line(sb, "PROMPT_COMMAND=\"history -a${PROMPT_COMMAND:+;$PROMPT_COMMAND}\"");
        return sb.ToString();
    }

    /// <summary>.zshenv da pasta ZDOTDIR temporária: só repassa para o original.</summary>
    public static string ZshEnv(string tabId) =>
        $"# {string.Format(Header, tabId)}\n[ -f \"$HOME/.zshenv\" ] && . \"$HOME/.zshenv\"\n";

    /// <summary>.zshrc da pasta ZDOTDIR temporária: carrega o original e acrescenta o do Forge.</summary>
    public static string Zsh(ScriptOptions o)
    {
        var sb = new StringBuilder();
        Line(sb, "# " + string.Format(Header, o.TabId));
        Line(sb, "ZDOTDIR=$HOME");
        Line(sb, "[ -f \"$HOME/.zshrc\" ] && . \"$HOME/.zshrc\"");
        if (o.Cwd is { } cwd)
            Line(sb, $"cd -- {ShCdTarget(cwd)} 2>/dev/null");
        foreach (var (name, value) in o.Env)
            Line(sb, $"export {name}={ShQuote(value)}");
        if (o.FlushHistory)
            Line(sb, "setopt INC_APPEND_HISTORY");
        if (o.CaseInsensitiveCompletion)
            Line(sb, "zstyle ':completion:*' matcher-list 'm:{a-zA-Z}={A-Za-z}'");
        if (o.InjectIdentity)
        {
            Line(sb, $"PROMPT=${{PROMPT//\\%n/{ShQuote(o.IdentityUser!)}}}");
            Line(sb, $"PROMPT=${{PROMPT//\\%m/{ShQuote(o.IdentityMachine!)}}}");
        }
        if (o.Integration)
            sb.Append(ZshIntegration.ReplaceLineEndings("\n"));
        return sb.ToString();
    }

    /// <summary>Script para <c>cmd /k</c>. O CMD não tem integração: a aba só sabe que o comando foi enviado.</summary>
    public static string Cmd(ScriptOptions o)
    {
        var sb = new StringBuilder();
        Line(sb, "@echo off", "\r\n");
        // O CMD relê o .cmd linha a linha: depois do chcp, pastas com acento (gravadas em UTF-8) saem certas.
        Line(sb, "chcp 65001 >nul", "\r\n");
        Line(sb, "rem " + string.Format(Header, o.TabId), "\r\n");
        if (o.Cwd is { } cwd)
            Line(sb, $"cd /d \"{cwd}\"", "\r\n");
        foreach (var (name, value) in o.Env)
            Line(sb, $"set \"{name}={value.Replace("%", "%%", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal)}\"", "\r\n");
        Line(sb, "@echo on", "\r\n");
        return sb.ToString();
    }

    public static string ShQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    public static string PsQuote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>Destino de um cd no bash/zsh: ~ fica de fora das aspas para ser expandido.</summary>
    internal static string ShCdTarget(string path) =>
        path == "~" ? "~"
        : path.StartsWith("~/", StringComparison.Ordinal) ? "~/" + ShQuote(path[2..])
        : ShQuote(path);

    private static void Line(StringBuilder sb, string text, string newline = "\n") => sb.Append(text).Append(newline);

    // OSC 133 (A prompt, C comando começou, D terminou com código), 633;E (texto do comando) e 7 (pasta atual).
    // O texto do comando escapa "\" e ";" (e quebras de linha) para não quebrar a sequência.
    private const string PowerShellIntegration = """
        $global:__forgeEsc = [string][char]0x1b
        $global:__forgeBel = [string][char]0x07
        $global:__forgeOriginalPrompt = $function:prompt
        $global:__forgeRan = $false
        $global:__forgeIdentity = __FORGE_IDENTITY__
        function global:__forgeOsc([string]$body) {
            [Console]::Write("$($global:__forgeEsc)]$body$($global:__forgeBel)")
        }
        function global:prompt {
            $ok = $?
            $native = $global:LASTEXITCODE
            if ($global:__forgeRan) {
                $code = if ($ok) { 0 } elseif ($native) { $native } else { 1 }
                __forgeOsc "133;D;$code"
                $global:__forgeRan = $false
            }
            $location = $executionContext.SessionState.Path.CurrentLocation
            if ($location.Provider.Name -eq 'FileSystem') {
                __forgeOsc ('7;file:///' + ($location.ProviderPath -replace '\\', '/'))
            }
            __forgeOsc '133;A'
            $text = $global:__forgeIdentity + (& $global:__forgeOriginalPrompt)
            $global:LASTEXITCODE = $native
            $text + $global:__forgeEsc + ']133;B' + $global:__forgeBel
        }
        if (Get-Module -Name PSReadLine) {
            Set-PSReadLineKeyHandler -Chord Enter -ScriptBlock {
                $line = $null
                $cursor = $null
                [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)
                if ($line.Trim().Length -gt 0) {
                    $escaped = $line.Replace('\', '\\').Replace(';', '\x3b').Replace("`r", '').Replace("`n", '\x0a')
                    __forgeOsc "633;E;$escaped"
                    __forgeOsc '133;C'
                    $global:__forgeRan = $true
                }
                [Microsoft.PowerShell.PSConsoleReadLine]::AcceptLine()
            }
        }

        """;

    // PS0 roda num subshell ao aceitar o comando: lê o texto pelo histórico e emite E e C. O D sai sempre no
    // PROMPT_COMMAND; o parser ignora o D que não teve um C antes (o primeiro prompt, Enter vazio).
    private const string BashIntegration = """
        __forge_osc() { printf '\033]%s\007' "$1"; }
        __forge_escape() {
          local s=${1//\\/\\\\}
          s=${s//;/\\x3b}
          printf '%s' "${s//$'\n'/\\x0a}"
        }
        __forge_precmd() {
          local code=$?
          __FORGE_FLUSH__
          __forge_osc "133;D;$code"
          __forge_osc "7;file://${HOSTNAME}${PWD}"
          return $code
        }
        __forge_preexec() {
          local line
          line=$(HISTTIMEFORMAT= builtin history 1)
          [[ $line =~ ^[[:space:]]*[0-9]+\*?[[:space:]]+(.*)$ ]] && line=${BASH_REMATCH[1]}
          __forge_osc "633;E;$(__forge_escape "$line")"
          __forge_osc '133;C'
        }
        PROMPT_COMMAND="__forge_precmd${PROMPT_COMMAND:+;$PROMPT_COMMAND}"
        PS0='$(__forge_preexec)'"${PS0:-}"
        PS1="\[\033]133;A\007\]${PS1}\[\033]133;B\007\]"

        """;

    // Temas do zsh redesenham o PROMPT a cada precmd: o A sai no próprio precmd, em vez de dentro do PROMPT.
    private const string ZshIntegration = """
        autoload -Uz add-zsh-hook
        __forge_osc() { printf '\033]%s\007' "$1"; }
        __forge_precmd() {
          local code=$?
          __forge_osc "133;D;$code"
          __forge_osc "7;file://${HOST}${PWD}"
          __forge_osc '133;A'
        }
        __forge_preexec() {
          local s=${1//\\/\\\\}
          s=${s//;/\\x3b}
          s=${s//$'\n'/\\x0a}
          __forge_osc "633;E;$s"
          __forge_osc '133;C'
        }
        add-zsh-hook precmd __forge_precmd
        add-zsh-hook preexec __forge_preexec

        """;
}
