using Forge.Core.Models;
using Forge.Core.Shells;

namespace Forge.Core.Tests;

public class LaunchBuilderTests
{
    private const string Scripts = @"C:\Users\dev\AppData\Roaming\Forge\shell";

    private static readonly ShellProfile Pwsh = new() { Id = "pwsh", Name = "PowerShell 7", Kind = ShellKind.PowerShell, Flavor = ShellFlavor.PowerShell, Executable = @"C:\Program Files\PowerShell\7\pwsh.exe" };
    private static readonly ShellProfile Ubuntu = new() { Id = "wsl:Ubuntu", Name = "Ubuntu", Kind = ShellKind.Wsl, Flavor = ShellFlavor.Bash, Executable = @"C:\Windows\System32\wsl.exe", Distro = "Ubuntu" };
    private static readonly ShellProfile Cmd = new() { Id = "cmd", Name = "Prompt de Comando", Kind = ShellKind.Cmd, Flavor = ShellFlavor.Cmd, Executable = @"C:\Windows\System32\cmd.exe" };

    private static LaunchPlan Build(WorkspaceTab tab, ShellProfile shell, AppSettings? settings = null, string? groupFolder = null) =>
        new LaunchBuilder(Scripts).Build(tab, shell, settings ?? new AppSettings(), groupFolder, "dev", "desk-01");

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
        Assert.Contains("[ -f ~/.bashrc ] && . ~/.bashrc", content);
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
    public void Sem_pasta_propria_usa_a_do_grupo_e_com_a_opcao_ligada_a_ultima_visitada()
    {
        var tab = new WorkspaceTab { Cwd = null, LastCwd = @"C:\dev\outra" };

        Assert.Equal(@"C:\dev\grupo", LaunchBuilder.ResolveCwd(tab, @"C:\dev\grupo", FolderOnRestore.StartFolder));
        Assert.Equal(@"C:\dev\outra", LaunchBuilder.ResolveCwd(tab, @"C:\dev\grupo", FolderOnRestore.LastVisited));
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
        settings.Identity.InjectInBashPrompt = true;
        settings.Identity.UserName = "gabriel";

        var content = Assert.Single(Build(new WorkspaceTab { Id = "t6" }, Ubuntu, settings).Scripts).Content;

        Assert.Contains("PS1=${PS1//\\\\u/'gabriel'}", content);
        Assert.Contains("PS1=${PS1//\\\\h/'desk-01'}", content);
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
