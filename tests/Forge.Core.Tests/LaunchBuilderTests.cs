using Forge.Core.Models;
using Forge.Core.Shells;

namespace Forge.Core.Tests;

public class LaunchBuilderTests
{
    private const string Scripts = @"C:\Users\dev\AppData\Roaming\Forge\shell";

    private static readonly ShellProfile Pwsh = new() { Id = "pwsh", Name = "PowerShell 7", Kind = ShellKind.PowerShell, Flavor = ShellFlavor.PowerShell, Executable = @"C:\Program Files\PowerShell\7\pwsh.exe" };
    private static readonly ShellProfile Ubuntu = new() { Id = "wsl:Ubuntu", Name = "Ubuntu", Kind = ShellKind.Wsl, Flavor = ShellFlavor.Bash, Executable = @"C:\Windows\System32\wsl.exe", Distro = "Ubuntu" };
    private static readonly ShellProfile PwshInUbuntu = Pwsh with { Id = "wsl:Ubuntu:pwsh", Name = "Ubuntu · PowerShell 7", SettingsId = "pwsh", Distro = "Ubuntu", WslExecutable = @"C:\Windows\System32\wsl.exe", WslHome = "/home/dev" };
    private static readonly ShellProfile Cmd = new() { Id = "cmd", Name = "Prompt de Comando", Kind = ShellKind.Cmd, Flavor = ShellFlavor.Cmd, Executable = @"C:\Windows\System32\cmd.exe" };

    private const string Nonce = "n0nce";

    private static LaunchPlan Build(WorkspaceTab tab, ShellProfile shell, AppSettings? settings = null) =>
        new LaunchBuilder(Scripts, () => Nonce).Build(tab, shell, settings ?? new AppSettings(), "dev", "desk-01");

    [Fact]
    public void Cada_sessao_ganha_um_nonce_novo()
    {
        var builder = new LaunchBuilder(Scripts);
        var tab = new WorkspaceTab { Id = "n1" };

        var first = builder.Build(tab, Pwsh, new AppSettings(), "dev", "desk-01").Nonce;
        var second = builder.Build(tab, Pwsh, new AppSettings(), "dev", "desk-01").Nonce;

        Assert.Equal(32, first.Length);
        Assert.NotEqual(first, second);
        // Sem integração também: nada emite o nonce, então nenhuma sequência forjada vale.
        Assert.NotEmpty(builder.Build(tab, Cmd, new AppSettings(), "dev", "desk-01").Nonce);
    }

    [Fact]
    public void Scripts_assinam_comando_e_pasta_com_o_nonce()
    {
        var zsh = Ubuntu with { Id = "wsl:Ubuntu:zsh", Flavor = ShellFlavor.Zsh, LinuxShell = "zsh" };

        var pwsh = Assert.Single(Build(new WorkspaceTab { Id = "s1" }, Pwsh).Scripts).Content;
        var bash = Assert.Single(Build(new WorkspaceTab { Id = "s2" }, Ubuntu).Scripts).Content;
        var zshrc = Build(new WorkspaceTab { Id = "s3" }, zsh).Scripts.Single(s => s.WindowsPath.EndsWith(".zshrc", StringComparison.Ordinal)).Content;

        Assert.Contains("$global:__forgeNonce = 'n0nce'", pwsh);
        Assert.Contains("\"633;E;$escaped;$($global:__forgeNonce)\"", pwsh);
        Assert.Contains("\"633;P;Cwd=$uri;$($global:__forgeNonce)\"", pwsh);
        Assert.Contains("__forge_nonce='n0nce'", bash);
        Assert.Contains(";${__forge_nonce}\"", bash);
        Assert.Contains("__forge_nonce='n0nce'", zshrc);
        Assert.Contains("\"633;E;$s;${__forge_nonce}\"", zshrc);
        // O OSC 7 sem assinatura saiu dos scripts.
        Assert.DoesNotContain("7;file:", pwsh + bash + zshrc);
        Assert.DoesNotContain("__FORGE_", pwsh + bash + zshrc);
    }

    [Fact]
    public void PowerShell_na_pasta_da_distro_manda_a_pasta_de_rede_com_quatro_barras()
    {
        var pwsh = Assert.Single(Build(new WorkspaceTab { Id = "u1" }, PwshInUbuntu).Scripts).Content;

        // \\wsl.localhost\Ubuntu → //wsl.localhost/Ubuntu → file:////wsl.localhost/Ubuntu (e não file://///).
        Assert.Contains("$uri = if ($path.StartsWith('//')) { 'file://' + $path } else { 'file:///' + $path }", pwsh);
    }

    [Fact]
    public void Linha_que_o_shell_deixa_fora_do_historico_fica_fora_do_Forge()
    {
        var zsh = Ubuntu with { Id = "wsl:Ubuntu:zsh", Flavor = ShellFlavor.Zsh, LinuxShell = "zsh" };

        var pwsh = Assert.Single(Build(new WorkspaceTab { Id = "h1" }, Pwsh).Scripts).Content;
        var bash = Assert.Single(Build(new WorkspaceTab { Id = "h2" }, Ubuntu).Scripts).Content;
        var zshrc = Build(new WorkspaceTab { Id = "h3" }, zsh).Scripts.Single(s => s.WindowsPath.EndsWith(".zshrc", StringComparison.Ordinal)).Content;

        // PowerShell: espaço no começo e o filtro do PSReadLine (senhas, tokens).
        Assert.Contains("if (__forgeRecord $line) {", pwsh);
        Assert.Contains("(Get-PSReadLineOption).AddToHistoryHandler", pwsh);
        // bash: o mesmo número de histórico no PS0 e no último prompt = a linha não entrou (HISTCONTROL, HISTIGNORE).
        Assert.Contains("__forge_lasthist=$(__forge_histnum)", bash);
        Assert.Contains("if [[ ${BASH_REMATCH[1]} != \"$__forge_lasthist\" ]]; then", bash);
        // zsh: espaço no começo.
        Assert.Contains("if [[ $1 != ' '* ]]; then", zshrc);
    }

    [Fact]
    public void PowerShell_carrega_o_script_da_aba_com_dot_source()
    {
        var tab = new WorkspaceTab { Id = "t1", Cwd = @"C:\dev\cliente-a\web" };

        var plan = Build(tab, Pwsh);

        Assert.Equal(
            "\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -NoLogo -NoExit -ExecutionPolicy Bypass -Command \". 'C:\\Users\\dev\\AppData\\Roaming\\Forge\\shell\\tab-t1.ps1'\"",
            plan.CommandLine);
        var script = Assert.Single(plan.Scripts);
        Assert.True(script.Utf8Bom);
        Assert.Contains(@"Set-Location -LiteralPath 'C:\dev\cliente-a\web'", script.Content);
        Assert.Contains("133;A", script.Content);
        Assert.True(plan.Integration);
    }

    [Fact]
    public void WSL_usa_rcfile_com_caminho_do_Linux_e_pasta_convertida()
    {
        var tab = new WorkspaceTab { Id = "t2", Cwd = @"C:\dev\api" };
        tab.Env["PORT"] = "3001";
        tab.Env["NOME INVALIDO"] = "x";

        var plan = Build(tab, Ubuntu);

        Assert.Equal(
            @"C:\Windows\System32\wsl.exe -d Ubuntu --cd ~ -- bash --rcfile /mnt/c/Users/dev/AppData/Roaming/Forge/shell/tab-t2.bash -i",
            plan.CommandLine);
        var content = Assert.Single(plan.Scripts).Content;
        // Shell de login, como o Windows Terminal: o ~/.profile põe ~/.local/bin no PATH e chama o ~/.bashrc.
        Assert.Contains("[ -f /etc/profile ] && . /etc/profile", content);
        Assert.Contains("elif [ -f ~/.profile ]; then . ~/.profile; fi", content);
        Assert.DoesNotContain("[ -f ~/.bashrc ] && . ~/.bashrc", content);
        Assert.Contains("cd -- '/mnt/c/dev/api'", content);
        Assert.Contains("export PORT='3001'", content);
        Assert.DoesNotContain("INVALIDO", content);
        Assert.Contains("bind 'set completion-ignore-case on'", content);
        Assert.Contains("history -a", content);
        Assert.DoesNotContain("\r\n", content);
    }

    [Fact]
    public void Pasta_relativa_a_home_fica_fora_das_aspas()
    {
        var plan = Build(new WorkspaceTab { Id = "t3", Cwd = "~/meu projeto" }, Ubuntu);

        Assert.Contains("cd -- ~/'meu projeto'", Assert.Single(plan.Scripts).Content);
    }

    [Fact]
    public void Volta_na_ultima_pasta_visitada_ou_na_pasta_fixa()
    {
        var tab = new WorkspaceTab { Cwd = @"C:\dev\fixa", LastCwd = @"C:\dev\outra" };

        Assert.Equal(@"C:\dev\outra", LaunchBuilder.ResolveCwd(tab, FolderOnRestore.LastVisited));
        Assert.Equal(@"C:\dev\fixa", LaunchBuilder.ResolveCwd(tab, FolderOnRestore.StartFolder));
        Assert.Null(LaunchBuilder.ResolveCwd(new WorkspaceTab(), FolderOnRestore.LastVisited));
    }

    [Fact]
    public void Sem_pasta_a_aba_abre_na_home_e_nao_onde_o_Forge_foi_aberto()
    {
        Assert.Contains("Set-Location -Path '~'", Assert.Single(Build(new WorkspaceTab { Id = "h1" }, Pwsh).Scripts).Content);
        Assert.Contains("cd -- ~ 2>/dev/null", Assert.Single(Build(new WorkspaceTab { Id = "h2" }, Ubuntu).Scripts).Content);
        Assert.Contains("cd /d \"%USERPROFILE%\"", Assert.Single(Build(new WorkspaceTab { Id = "h3" }, Cmd).Scripts).Content);
    }

    [Fact]
    public void CMD_nao_tem_integracao_e_usa_script_com_chcp()
    {
        var plan = Build(new WorkspaceTab { Id = "t4", Cwd = @"C:\dev\web" }, Cmd);

        Assert.False(plan.Integration);
        Assert.Equal(@"C:\Windows\System32\cmd.exe /k C:\Users\dev\AppData\Roaming\Forge\shell\tab-t4.cmd", plan.CommandLine);
        var content = Assert.Single(plan.Scripts).Content;
        Assert.StartsWith("@echo off\r\nchcp 65001 >nul\r\n", content);
        Assert.Contains("cd /d \"C:\\dev\\web\"", content);
        Assert.EndsWith("@echo on\r\n", content);
    }

    [Fact]
    public void Integracao_desligada_para_o_shell_nao_entra_no_script()
    {
        var settings = new AppSettings { IntegrationDisabled = ["wsl:Ubuntu"] };

        var plan = Build(new WorkspaceTab { Id = "t5" }, Ubuntu, settings);

        Assert.False(plan.Integration);
        Assert.DoesNotContain("133;", Assert.Single(plan.Scripts).Content);
    }

    [Fact]
    public void Identidade_injetada_no_prompt_do_bash()
    {
        var settings = new AppSettings();
        settings.Identity.MaskPrompt = true;
        settings.Identity.UserName = "gabriel";
        settings.Identity.MachineName = "forge $; rm";

        var bash = Assert.Single(Build(new WorkspaceTab { Id = "t6" }, Ubuntu, settings).Scripts).Content;
        var pwsh = Assert.Single(Build(new WorkspaceTab { Id = "t7" }, Pwsh, settings).Scripts).Content;

        // Caracteres que o shell interpretaria saem da máscara.
        Assert.Contains("PS1='\\[\\e[32m\\]gabriel@forge-rm\\[\\e[0m\\]:\\[\\e[34m\\]\\w\\[\\e[0m\\]\\$ '", bash);
        Assert.Contains("$global:__forgeMask = 'gabriel@forge-rm'", pwsh);
    }

    [Fact]
    public void Git_Bash_continua_carregando_so_o_bashrc()
    {
        var gitBash = new ShellProfile { Id = "gitbash", Name = "Git Bash", Kind = ShellKind.GitBash, Flavor = ShellFlavor.Bash, Executable = @"C:\Program Files\Git\bin\bash.exe" };

        var content = Assert.Single(Build(new WorkspaceTab { Id = "g1" }, gitBash).Scripts).Content;

        Assert.Contains("[ -f ~/.bashrc ] && . ~/.bashrc", content);
        Assert.DoesNotContain("/etc/profile", content);
    }

    [Fact]
    public void Zsh_do_WSL_abre_como_shell_de_login_e_repassa_o_zprofile()
    {
        var zsh = Ubuntu with { Id = "wsl:Ubuntu:zsh", Flavor = ShellFlavor.Zsh, LinuxShell = "zsh" };

        var plan = Build(new WorkspaceTab { Id = "z1" }, zsh);

        Assert.EndsWith("zsh -l -i", plan.CommandLine);
        Assert.Contains(plan.Scripts, s => s.WindowsPath.EndsWith(@"\.zprofile", StringComparison.Ordinal) && s.Content.Contains(". \"$HOME/.zprofile\""));
    }

    [Fact]
    public void Fish_abre_direto_na_pasta_sem_script()
    {
        var fish = Ubuntu with { Id = "wsl:Ubuntu:fish", Flavor = ShellFlavor.Unknown, LinuxShell = "fish", WslHome = "/home/dev" };

        var plan = Build(new WorkspaceTab { Id = "f1", Cwd = "~/api" }, fish);

        Assert.Equal(@"C:\Windows\System32\wsl.exe -d Ubuntu --cd /home/dev/api -- fish -l", plan.CommandLine);
        Assert.Empty(plan.Scripts);
        Assert.False(plan.Integration);
    }

    [Theory]
    [InlineData(null, @"\\wsl.localhost\Ubuntu\home\dev")]
    [InlineData("~/api", @"\\wsl.localhost\Ubuntu\home\dev\api")]
    [InlineData("/srv/app", @"\\wsl.localhost\Ubuntu\srv\app")]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\dev\web", @"\\wsl.localhost\Ubuntu\home\dev\web")]
    [InlineData(@"C:\dev\web", @"C:\dev\web")]
    public void Terminal_do_Windows_na_distro_abre_na_pasta_dela(string? cwd, string expected)
    {
        var content = Assert.Single(Build(new WorkspaceTab { Id = "w1", Cwd = cwd }, PwshInUbuntu).Scripts).Content;

        Assert.Contains($"Set-Location -LiteralPath '{expected}'", content);
    }

    [Fact]
    public void Sem_a_home_detectada_o_terminal_do_Windows_abre_na_raiz_da_distro()
    {
        var content = Assert.Single(Build(new WorkspaceTab { Id = "w2" }, PwshInUbuntu with { WslHome = null }).Scripts).Content;

        Assert.Contains(@"Set-Location -LiteralPath '\\wsl.localhost\Ubuntu\'", content);
    }

    [Fact]
    public void CMD_e_Git_Bash_na_distro_entram_na_pasta_de_rede()
    {
        var cmd = Cmd with { Id = "wsl:Ubuntu:cmd", SettingsId = "cmd", Distro = "Ubuntu", WslHome = "/home/dev" };
        var gitBash = new ShellProfile { Id = "wsl:Ubuntu:gitbash", Name = "Ubuntu · Git Bash", Kind = ShellKind.GitBash, Flavor = ShellFlavor.Bash, Executable = @"C:\Program Files\Git\bin\bash.exe", SettingsId = "gitbash", Distro = "Ubuntu", WslHome = "/home/dev" };

        Assert.Contains(@"pushd ""\\wsl.localhost\Ubuntu\home\dev""", Assert.Single(Build(new WorkspaceTab { Id = "c1" }, cmd).Scripts).Content);
        var bash = Assert.Single(Build(new WorkspaceTab { Id = "b1" }, gitBash).Scripts).Content;
        Assert.Contains("cd -- '//wsl.localhost/Ubuntu/home/dev'", bash);
        Assert.Contains("[ -f ~/.bashrc ] && . ~/.bashrc", bash);
    }

    [Fact]
    public void Integracao_desligada_no_PowerShell_vale_tambem_dentro_da_distro()
    {
        var plan = Build(new WorkspaceTab { Id = "w3" }, PwshInUbuntu, new AppSettings { IntegrationDisabled = ["pwsh"] });

        Assert.False(plan.Integration);
    }

    [Theory]
    [InlineData("simples", "simples")]
    [InlineData("com espaço", "\"com espaço\"")]
    [InlineData(@"C:\pasta com espaço\", "\"C:\\pasta com espaço\\\\\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("", "\"\"")]
    public void Aspas_seguem_as_regras_do_Windows(string arg, string expected) =>
        Assert.Equal(expected, Forge.Core.Util.CommandLine.Quote(arg));
}
