using Forge.Core.Models;
using Forge.Core.Persistence;
using Forge.Core.Workspaces;
using Microsoft.Extensions.Time.Testing;

namespace Forge.Core.Tests;

public class WorkspaceStoreTests
{
    private static ShellProfile Pwsh => new() { Id = ShellIds.PowerShell, Name = "PowerShell 7", Kind = ShellKind.PowerShell, Flavor = ShellFlavor.PowerShell, Executable = "pwsh.exe" };

    [Fact]
    public void Grava_500_ms_depois_da_ultima_mudanca()
    {
        using var temp = new TempFolder();
        var time = new FakeTimeProvider();
        var path = temp.File("workspaces.json");
        using var store = new WorkspaceStore(path, time);
        store.Load();

        store.Update(f => WorkspaceEditor.AddGroup(f, "Cliente A"));
        time.Advance(TimeSpan.FromMilliseconds(300));
        store.Update(f => f.Groups[0].Name = "Cliente A2");
        time.Advance(TimeSpan.FromMilliseconds(300));
        Assert.False(File.Exists(path));

        time.Advance(TimeSpan.FromMilliseconds(250));
        Assert.True(File.Exists(path));
        Assert.Contains("Cliente A2", File.ReadAllText(path));
    }

    [Fact]
    public void Reabrir_traz_grupos_abas_e_configuracao_de_volta()
    {
        using var temp = new TempFolder();
        var path = temp.File("workspaces.json");
        using (var store = new WorkspaceStore(path))
        {
            store.Load();
            store.Update(f =>
            {
                var group = WorkspaceEditor.AddGroup(f, "Cliente A");
                var tab = WorkspaceEditor.AddTab(group, Pwsh, "web", cwd: @"C:\dev\cliente-a\web");
                tab.Startup = "npm run dev";
                tab.Autostart = true;
                tab.Env["PORT"] = "3001";
                f.SelectedTabId = tab.Id;
            });
        } // Dispose grava o pendente, como ao fechar o app.

        using var reopened = new WorkspaceStore(path);
        reopened.Load();
        var group2 = Assert.Single(reopened.Current.Groups);
        var tab2 = Assert.Single(group2.Tabs);
        Assert.Equal("Cliente A", group2.Name);
        Assert.Equal("web", tab2.Name);
        Assert.Equal("pwsh", tab2.Shell);
        Assert.Equal(@"C:\dev\cliente-a\web", tab2.Cwd);
        Assert.Equal("npm run dev", tab2.Startup);
        Assert.True(tab2.Autostart);
        Assert.Equal("3001", tab2.Env["PORT"]);
        Assert.Equal(tab2.Id, reopened.Current.SelectedTabId);
    }

    [Fact]
    public void Arquivo_truncado_volta_para_a_ultima_versao_boa()
    {
        using var temp = new TempFolder();
        var path = temp.File("workspaces.json");
        using (var store = new WorkspaceStore(path))
        {
            store.Load();
            store.Update(f => WorkspaceEditor.AddGroup(f, "Versão 1"));
            store.Flush();
            store.Update(f => WorkspaceEditor.AddGroup(f, "Versão 2"));
            store.Flush();
        }
        // Simula o PC desligando no meio de uma escrita feita por fora do app.
        File.WriteAllText(path, "{ \"groups\": [ { \"name\": \"Vers");

        using var reopened = new WorkspaceStore(path);
        reopened.Load();
        Assert.NotNull(reopened.LoadProblem);
        Assert.Contains(reopened.Current.Groups, g => g.Name == "Versão 1");
        Assert.True(File.Exists(path + ".corrompido"));
    }

    [Fact]
    public void Ids_repetidos_e_nomes_vazios_sao_corrigidos()
    {
        var file = new WorkspaceFile
        {
            SelectedTabId = "sumiu",
            Groups =
            [
                new WorkspaceGroup { Id = "g", Name = " ", Tabs = [new WorkspaceTab { Id = "x", Name = "" }] },
                new WorkspaceGroup { Id = "g", Name = "B", Tabs = [new WorkspaceTab { Id = "x", Name = "b" }] },
            ],
        };

        WorkspaceStore.Normalize(file);


        Assert.NotEqual(file.Groups[0].Id, file.Groups[1].Id);
        Assert.NotEqual(file.Groups[0].Tabs[0].Id, file.Groups[1].Tabs[0].Id);
        Assert.Equal("Sem nome", file.Groups[0].Name);
        Assert.Equal("aba", file.Groups[0].Tabs[0].Name);
        Assert.Null(file.SelectedTabId);
    }

    [Fact]
    public void Grupo_antigo_como_ambiente_passa_shell_e_pasta_para_as_abas()
    {
        // Formato da versão anterior: o grupo guardava o ambiente e a pasta; as abas seguiam o grupo.
        var file = new WorkspaceFile
        {
            Groups =
            [
                new WorkspaceGroup
                {
                    Name = "Rentalz",
                    Shell = "wsl:Debian",
                    Folder = "~/Projetos/Rentalz",
                    Tabs = [new WorkspaceTab { Name = "Front-end" }, new WorkspaceTab { Name = "Back-end", Shell = "pwsh", Cwd = @"C:\dev\api" }],
                },
            ],
        };

        WorkspaceStore.Normalize(file);

        var group = Assert.Single(file.Groups);
        Assert.Null(group.Shell);
        Assert.Null(group.Folder);
        Assert.Equal(("wsl:Debian", "~/Projetos/Rentalz"), (group.Tabs[0].Shell, group.Tabs[0].Cwd));
        Assert.Equal(("pwsh", @"C:\dev\api"), (group.Tabs[1].Shell, group.Tabs[1].Cwd));
    }
}
