using Forge.Core.Models;
using Forge.Core.Workspaces;

namespace Forge.Core.Tests;

public class WorkspaceTests
{
    private static readonly ShellProfile Pwsh = new() { Id = "pwsh", Name = "PowerShell 7", Kind = ShellKind.PowerShell, Flavor = ShellFlavor.PowerShell, Executable = "pwsh.exe" };
    private static readonly ShellProfile Ubuntu = new() { Id = "wsl:Ubuntu", Name = "Ubuntu", Kind = ShellKind.Wsl, Flavor = ShellFlavor.Bash, Executable = "wsl.exe", Distro = "Ubuntu" };

    [Fact]
    public void Restauracao_comeca_pela_selecionada_e_depois_as_automaticas()
    {
        var file = new WorkspaceFile();
        var a = WorkspaceEditor.AddGroup(file, "Cliente A");
        var api = WorkspaceEditor.AddTab(a, Ubuntu, "api");
        api.Autostart = true;
        api.Startup = "npm run dev";
        var web = WorkspaceEditor.AddTab(a, Pwsh, "web");
        web.Startup = "npm run dev";
        var worker = WorkspaceEditor.AddTab(a, Pwsh, "worker");
        worker.Startup = "npm run worker";
        file.SelectedTabId = worker.Id;

        var plan = RestorePlanner.Plan(file);

        // worker: visível → abre, com o comando ao abrir. api: marcada para abrir junto com o app → abre também.
        // web: nem visível nem automática → espera o clique.
        Assert.Collection(plan,
            s => Assert.Equal((worker.Id, true), (s.Tab.Id, s.RunStartup)),
            s => Assert.Equal((api.Id, true), (s.Tab.Id, s.RunStartup)));
    }

    [Fact]
    public void Sem_selecionada_a_primeira_aba_ganha_shell()
    {
        var file = new WorkspaceFile();
        var tab = WorkspaceEditor.AddTab(WorkspaceEditor.AddGroup(file, "G"), Pwsh);

        Assert.Equal(tab.Id, Assert.Single(RestorePlanner.Plan(file)).Tab.Id);
    }

    [Fact]
    public void Aba_nova_segue_a_pasta_e_o_ambiente_do_grupo()
    {
        var file = new WorkspaceFile();
        var group = WorkspaceEditor.AddGroup(file, "Cliente A", folder: "~/cliente-a", shell: Ubuntu.Id);

        var first = WorkspaceEditor.AddTab(group, Ubuntu);
        var second = WorkspaceEditor.AddTab(group, Ubuntu);
        var other = WorkspaceEditor.AddTab(group, Pwsh);

        // Mesmo ambiente do grupo: a aba não guarda shell nem pasta, e acompanha o grupo se ele mudar.
        Assert.Null(first.Shell);
        Assert.Null(first.Cwd);
        Assert.Equal("~/cliente-a", Forge.Core.Shells.LaunchBuilder.ResolveCwd(first, group.Folder, FolderOnRestore.StartFolder));
        Assert.Equal("wsl:Ubuntu", WorkspaceEditor.ShellOf(group, first, "pwsh"));
        Assert.Equal("ubuntu", first.Name);
        Assert.Equal("ubuntu 2", second.Name);

        // Outro shell: fica gravado na aba.
        Assert.Equal("pwsh", other.Shell);

        group.Shell = "wsl:Debian";
        Assert.Equal("wsl:Debian", WorkspaceEditor.ShellOf(group, first, "pwsh"));
        Assert.Equal("pwsh", WorkspaceEditor.ShellOf(group, other, "pwsh"));
    }

    [Fact]
    public void Grupos_novos_recebem_cores_diferentes()
    {
        var file = new WorkspaceFile();

        var colors = Enumerable.Range(0, 3).Select(i => WorkspaceEditor.AddGroup(file, $"G{i}").Color).ToList();

        Assert.Equal(3, colors.Distinct().Count());
    }

    [Fact]
    public void Mover_aba_entre_grupos_e_duplicar()
    {
        var file = new WorkspaceFile();
        var a = WorkspaceEditor.AddGroup(file, "A");
        var b = WorkspaceEditor.AddGroup(file, "B");
        var tab = WorkspaceEditor.AddTab(a, Pwsh, "api");
        tab.Env["PORT"] = "3001";

        Assert.True(WorkspaceEditor.MoveTab(file, tab.Id, b.Id, 99));
        Assert.Empty(a.Tabs);
        Assert.Same(tab, Assert.Single(b.Tabs));

        var copy = WorkspaceEditor.Duplicate(file, tab.Id)!;
        Assert.NotEqual(tab.Id, copy.Id);
        Assert.Equal("api 2", copy.Name);
        Assert.Equal("3001", copy.Env["PORT"]);
        Assert.Equal(1, b.Tabs.IndexOf(copy));
    }

    [Fact]
    public void Remover_a_aba_selecionada_limpa_a_selecao()
    {
        var file = new WorkspaceFile();
        var tab = WorkspaceEditor.AddTab(WorkspaceEditor.AddGroup(file, "G"), Pwsh);
        file.SelectedTabId = tab.Id;

        Assert.True(WorkspaceEditor.RemoveTab(file, tab.Id));
        Assert.Null(file.SelectedTabId);
    }
}
