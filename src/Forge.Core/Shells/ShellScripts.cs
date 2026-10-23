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
    string? PromptMask,
    string Nonce,
    bool LoginProfile = false,
    string? LinuxHome = null)
{
    /// <summary>A máscara substitui o prompt do shell por "máscara:pasta".</summary>
    public bool HasMask => PromptMask is not null;
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

        // Máscara: o prompt vira "máscara:pasta>" (verde e azul); sem ela, fica o prompt do perfil do usuário.
        Line(sb, "$global:__forgeMask = " + (o.HasMask ? PsQuote(o.PromptMask!) : "$null"), "\r\n");
        Line(sb, "$global:__forgeLinuxHome = " + (o.LinuxHome is { } home ? PsQuote(home) : "$null"), "\r\n");
        sb.Append(PowerShellPrompt.ReplaceLineEndings("\r\n"));
        if (o.Integration)
            sb.Append(PowerShellIntegration.Replace("__FORGE_NONCE__", PsQuote(o.Nonce), StringComparison.Ordinal).ReplaceLineEndings("\r\n"));
        else if (o.HasMask)
            Line(sb, "function global:prompt { __forgePromptText }", "\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// Script para <c>bash --rcfile</c> (Git Bash e distros WSL). Com <see cref="ScriptOptions.LoginProfile"/> (WSL),
    /// carrega o que um shell de login carregaria, como o Windows Terminal faz: /etc/profile e ~/.bash_profile ou
    /// ~/.profile, que no Debian e no Ubuntu põe ~/.local/bin no PATH (onde ficam claude, pipx…) e chama o ~/.bashrc.
    /// </summary>
    public static string Bash(ScriptOptions o)
    {
        var sb = new StringBuilder();
        Line(sb, "# " + string.Format(Header, o.TabId));
        if (o.LoginProfile)
        {
            Line(sb, "[ -f /etc/profile ] && . /etc/profile");
            Line(sb, "if [ -f ~/.bash_profile ]; then . ~/.bash_profile; elif [ -f ~/.bash_login ]; then . ~/.bash_login; elif [ -f ~/.profile ]; then . ~/.profile; fi");
        }
        else
        {
            Line(sb, "[ -f ~/.bashrc ] && . ~/.bashrc");
        }
        if (o.Cwd is { } cwd)
            Line(sb, $"cd -- {ShCdTarget(cwd)} 2>/dev/null");
        foreach (var (name, value) in o.Env)
            Line(sb, $"export {name}={ShQuote(value)}");
        if (o.FlushHistory)
            Line(sb, "shopt -s histappend");
        if (o.CaseInsensitiveCompletion)
            Line(sb, "bind 'set completion-ignore-case on' 2>/dev/null");
        if (o.HasMask)
            Line(sb, "PS1='\\[\\e[32m\\]" + o.PromptMask + "\\[\\e[0m\\]:\\[\\e[34m\\]\\w\\[\\e[0m\\]\\$ '");

        if (o.Integration)
            sb.Append(BashIntegration
                .Replace("__FORGE_FLUSH__", o.FlushHistory ? "builtin history -a" : ":", StringComparison.Ordinal)
                .Replace("__FORGE_NONCE__", ShQuote(o.Nonce), StringComparison.Ordinal)
                .ReplaceLineEndings("\n"));
        else if (o.FlushHistory)
            Line(sb, "PROMPT_COMMAND=\"history -a${PROMPT_COMMAND:+;$PROMPT_COMMAND}\"");
        return sb.ToString();
    }

    /// <summary>.zshenv da pasta ZDOTDIR temporária: só repassa para o original.</summary>
    public static string ZshEnv(string tabId) =>
        $"# {string.Format(Header, tabId)}\n[ -f \"$HOME/.zshenv\" ] && . \"$HOME/.zshenv\"\n";

    /// <summary>.zprofile da pasta ZDOTDIR temporária (o zsh do WSL abre como shell de login): repassa para o original.</summary>
    public static string ZProfile(string tabId) =>
        $"# {string.Format(Header, tabId)}\n[ -f \"$HOME/.zprofile\" ] && . \"$HOME/.zprofile\"\n";

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
        if (o.HasMask)
            Line(sb, "PROMPT='%F{green}" + o.PromptMask + "%f:%F{blue}%~%f%# '");
        if (o.Integration)
            sb.Append(ZshIntegration.Replace("__FORGE_NONCE__", ShQuote(o.Nonce), StringComparison.Ordinal).ReplaceLineEndings("\n"));
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
        // O CMD não entra em pasta de rede (\\wsl.localhost\…) com cd: o pushd mapeia uma letra de unidade para ela.
        if (o.Cwd is { } cwd)
            Line(sb, cwd.StartsWith(@"\\", StringComparison.Ordinal) ? $"pushd \"{cwd}\"" : $"cd /d \"{cwd}\"", "\r\n");
        foreach (var (name, value) in o.Env)
            Line(sb, $"set \"{name}={value.Replace("%", "%%", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal)}\"", "\r\n");
        // Máscara: "máscara:pasta>" em verde e azul, como nos outros shells ($E é o ESC, $P a pasta, $G o >).
        if (o.HasMask)
            Line(sb, $"prompt $E[32m{o.PromptMask}$E[0m:$E[34m$P$E[0m$G", "\r\n");
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

    // A pasta da máscara, como no bash: sem o "Microsoft.PowerShell.Core\FileSystem::" que o PowerShell põe nas pastas de
    // rede, a pasta da distro (\\wsl.localhost\Debian\home\dev) como no Linux (/home/dev) e a home como ~.
    private const string PowerShellPrompt = """
        $global:__forgeOriginalPrompt = $function:prompt
        function global:__forgePromptPath {
            $location = $executionContext.SessionState.Path.CurrentLocation
            if ($location.Provider.Name -ne 'FileSystem') { return "$location" }
            $path = $location.ProviderPath
            $base = $HOME
            $sep = '\'
            if ($path -match '^\\\\wsl(\.localhost|\$)\\[^\\]+(\\.*)?$') {
                $path = if ($Matches[2]) { $Matches[2] -replace '\\', '/' } else { '/' }
                $base = $global:__forgeLinuxHome
                $sep = '/'
            }
            if ($base) {
                $base = $base.TrimEnd($sep)
                if ($path -eq $base) { return '~' }
                if ($path.StartsWith($base + $sep, [StringComparison]::OrdinalIgnoreCase)) { return '~' + $path.Substring($base.Length) }
            }
            $path
        }
        function global:__forgePromptText {
            if (-not $global:__forgeMask) { return (& $global:__forgeOriginalPrompt) }
            $e = [string][char]0x1b
            "$e[32m$($global:__forgeMask)$e[0m:$e[34m$(__forgePromptPath)$e[0m> "
        }

        """;

    // OSC 133 (A prompt, C comando começou, D terminou com código) e 633 (E texto do comando, P;Cwd= pasta atual).
    // O texto do comando escapa "\" e ";" (e quebras de linha) para não quebrar a sequência. E e P terminam com o nonce
    // da sessão: um programa que só imprime texto não o conhece, então não forja comando no histórico nem troca a pasta.
    // Linha que o próprio shell deixa fora do histórico (começa com espaço, HISTCONTROL, filtro de senhas do PSReadLine)
    // também não vai para o histórico do Forge.
    private const string PowerShellIntegration = """
        $global:__forgeEsc = [string][char]0x1b
        $global:__forgeBel = [string][char]0x07
        $global:__forgeNonce = __FORGE_NONCE__
        $global:__forgeRan = $false
        function global:__forgeOsc([string]$body) {
            [Console]::Write("$($global:__forgeEsc)]$body$($global:__forgeBel)")
        }
        function global:__forgeRecord([string]$line) {
            if ($line.StartsWith(' ')) { return $false }
            try {
                $handler = (Get-PSReadLineOption).AddToHistoryHandler
                if ($handler) {
                    $answer = $handler.Invoke($line)
                    if ($answer -is [bool]) { return $answer }
                    return "$answer" -eq 'MemoryAndFile'
                }
            } catch { }
            return $true
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
                # C:\dev vira file:///C:/dev; a pasta de rede \\wsl.localhost\Debian vira file:////wsl.localhost/Debian.
                $path = $location.ProviderPath -replace '\\', '/'
                $uri = if ($path.StartsWith('//')) { 'file://' + $path } else { 'file:///' + $path }
                __forgeOsc "633;P;Cwd=$uri;$($global:__forgeNonce)"
            }
            __forgeOsc '133;A'
            $text = __forgePromptText
            $global:LASTEXITCODE = $native
            $text + $global:__forgeEsc + ']133;B' + $global:__forgeBel
        }
        if (Get-Module -Name PSReadLine) {
            Set-PSReadLineKeyHandler -Chord Enter -ScriptBlock {
                $line = $null
                $cursor = $null
                [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)
                if ($line.Trim().Length -gt 0) {
                    if (__forgeRecord $line) {
                        $escaped = $line.Replace('\', '\\').Replace(';', '\x3b').Replace("`r", '').Replace("`n", '\x0a')
                        __forgeOsc "633;E;$escaped;$($global:__forgeNonce)"
                    }
                    __forgeOsc '133;C'
                    $global:__forgeRan = $true
                }
                [Microsoft.PowerShell.PSConsoleReadLine]::AcceptLine()
            }
        }

        """;

    // PS0 roda num subshell ao aceitar o comando: lê o texto pelo histórico e emite E e C. O D sai sempre no
    // PROMPT_COMMAND; o parser ignora o D que não teve um C antes (o primeiro prompt, Enter vazio).
    // O precmd guarda o número do último item do histórico: se o PS0 encontra o mesmo número, o bash não guardou a
    // linha (HISTCONTROL=ignorespace, HISTIGNORE…) e o E não sai.
    private const string BashIntegration = """
        __forge_nonce=__FORGE_NONCE__
        __forge_osc() { printf '\033]%s\007' "$1"; }
        __forge_escape() {
          local s=${1//\\/\\\\}
          s=${s//;/\\x3b}
          printf '%s' "${s//$'\n'/\\x0a}"
        }
        __forge_histnum() {
          local line
          line=$(HISTTIMEFORMAT= builtin history 1)
          [[ $line =~ ^[[:space:]]*([0-9]+) ]] && printf '%s' "${BASH_REMATCH[1]}"
        }
        __forge_precmd() {
          local code=$?
          __FORGE_FLUSH__
          __forge_lasthist=$(__forge_histnum)
          __forge_osc "133;D;$code"
          __forge_osc "633;P;Cwd=file://${HOSTNAME}${PWD};${__forge_nonce}"
          return $code
        }
        __forge_preexec() {
          local line
          line=$(HISTTIMEFORMAT= builtin history 1)
          if [[ $line =~ ^[[:space:]]*([0-9]+)\*?[[:space:]]+(.*)$ ]]; then
            if [[ ${BASH_REMATCH[1]} != "$__forge_lasthist" ]]; then
              __forge_osc "633;E;$(__forge_escape "${BASH_REMATCH[2]}");${__forge_nonce}"
            fi
          fi
          __forge_osc '133;C'
        }
        PROMPT_COMMAND="__forge_precmd${PROMPT_COMMAND:+;$PROMPT_COMMAND}"
        PS0='$(__forge_preexec)'"${PS0:-}"
        PS1="\[\033]133;A\007\]${PS1}\[\033]133;B\007\]"

        """;

    // Temas do zsh redesenham o PROMPT a cada precmd: o A sai no próprio precmd, em vez de dentro do PROMPT.
    // Comando que começa com espaço fica fora do histórico do Forge, como no HIST_IGNORE_SPACE.
    private const string ZshIntegration = """
        autoload -Uz add-zsh-hook
        __forge_nonce=__FORGE_NONCE__
        __forge_osc() { printf '\033]%s\007' "$1"; }
        __forge_precmd() {
          local code=$?
          __forge_osc "133;D;$code"
          __forge_osc "633;P;Cwd=file://${HOST}${PWD};${__forge_nonce}"
          __forge_osc '133;A'
        }
        __forge_preexec() {
          if [[ $1 != ' '* ]]; then
            local s=${1//\\/\\\\}
            s=${s//;/\\x3b}
            s=${s//$'\n'/\\x0a}
            __forge_osc "633;E;$s;${__forge_nonce}"
          fi
          __forge_osc '133;C'
        }
        add-zsh-hook precmd __forge_precmd
        add-zsh-hook preexec __forge_preexec

        """;
}
