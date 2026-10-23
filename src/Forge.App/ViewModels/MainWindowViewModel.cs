using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.App.Services;
using Forge.Core.Models;
using Forge.Core.Workspaces;
using Forge.Terminal.Native;

namespace Forge.App.ViewModels;

/// <summary>
/// A janela: árvore de grupos e abas, aba selecionada, restauração ao abrir e as ações com atalho.
/// A janela de administrador usa a mesma classe, com <see cref="IsAdmin"/> e o próprio workspaces-admin.json.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly WorkspaceContext _ctx;

    public MainWindowViewModel(WorkspaceContext ctx)
    {
        _ctx = ctx;
        History = new HistorySearchViewModel(ctx.History, this);
        Groups.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public WorkspaceContext Context => _ctx;

    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    public ObservableCollection<ShellProfile> Shells { get; } = [];

    public HistorySearchViewModel History { get; }

    public Notifier Notifier => _ctx.Notifier;

    public bool IsAdmin => _ctx.IsAdmin;

    public bool IsEmpty => Groups.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HasSelection))]
    private TabViewModel? _selectedTab;

    [ObservableProperty]
    private bool _sidebarCollapsed;

    public bool HasSelection => SelectedTab is not null;

    public string Title => SelectedTab is { } tab ? $"{tab.Group.Name} / {tab.Name}" : "";

    /// <summary>"dev@desk-01" no cabeçalho da aba, como o usuário definiu nas configurações.</summary>
    public string Identity => _ctx.Settings.Current.Identity.Display(Environment.UserName, Environment.MachineName);

    public bool ShowIdentity => _ctx.Settings.Current.Identity.ShowInHeader;

    public event EventHandler? SettingsRequested;

    /// <summary>Detecta os shells, monta a árvore e restaura as abas. Chamado uma vez, com a janela já visível.</summary>
    public async Task InitializeAsync()
    {
        var detected = await _ctx.Shells.DetectAsync(_ctx.Settings.Current.CustomProfiles);
        Shells.Clear();
        foreach (var shell in detected)
            Shells.Add(shell);

        SidebarCollapsed = _ctx.Store.Current.Window.SidebarCollapsed;
        Groups.Clear();
        foreach (var group in _ctx.Store.Current.Groups)
            Groups.Add(new GroupViewModel(group, this, _ctx));

        // Nenhuma configuração obrigatória antes do primeiro uso: na primeira vez já existe um grupo com uma aba.
        if (Groups.Count == 0 && !IsAdmin)
            CreateFirstWorkspace();

        await RestoreAsync();
    }

    private void CreateFirstWorkspace()
    {
        if (_ctx.Shells.Default(_ctx.Settings.Current.DefaultShell) is not { } shell)
            return;
        WorkspaceGroup? group = null;
        _ctx.Store.Update(f =>
        {
            group = WorkspaceEditor.AddGroup(f, "Meu workspace");
            WorkspaceEditor.AddTab(group, shell, name: shell.Name);
        });
        Groups.Add(new GroupViewModel(group!, this, _ctx));
    }

    /// <summary>Abas aparecem na hora; ganham shell só a selecionada e as automáticas, uma de cada vez.</summary>
    private async Task RestoreAsync()
    {
        var first = true;
        var started = 0;
        foreach (var step in RestorePlanner.Plan(_ctx.Store.Current))
        {
            if (FindTab(step.Tab.Id) is not { } tab)
                continue;
            if (first)
            {
                SelectedTab = tab;
                first = false;
            }
            tab.EnsureStarted();
            if (step.RunStartup)
                started++;
            await Task.Delay(150);
        }

        var tabs = AllTabs().Count();
        if (tabs > 1 && !IsAdmin)
        {
            var detail = started > 0
                ? $"{Groups.Count} grupos e {tabs} abas, como você deixou. {started} abas rodaram o comando ao abrir; as demais abrem o shell quando você clicar nelas."
                : $"{Groups.Count} grupos e {tabs} abas, como você deixou.";
            _ctx.Notifier.Show("Workspaces restaurados", detail);
        }
    }

    partial void OnSelectedTabChanged(TabViewModel? oldValue, TabViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;
        if (newValue is null)
            return;
        newValue.IsSelected = true;
        newValue.EnsureStarted();
        _ctx.Store.Update(f => f.SelectedTabId = newValue.Id);
        _ = newValue.RefreshGitAsync();
    }

    partial void OnSidebarCollapsedChanged(bool value) => _ctx.Store.Update(f => f.Window.SidebarCollapsed = value);

    public IEnumerable<TabViewModel> AllTabs() => Groups.SelectMany(g => g.Tabs);

    public TabViewModel? FindTab(string id) => AllTabs().FirstOrDefault(t => t.Id == id);

    [RelayCommand]
    private void Select(TabViewModel tab) => SelectedTab = tab;

    /// <summary>Mostra a janela de criar ou configurar grupo. A view liga; devolve true se o usuário salvou.</summary>
    public Func<GroupEditorViewModel, bool>? ShowGroupEditor { get; set; }

    /// <summary>
    /// Grupo novo: nome e cor. Como no Warp, ele só organiza; as abas dele são criadas no "+", cada uma do tipo
    /// escolhido (PowerShell, CMD, Git Bash, WSL).
    /// </summary>
    [RelayCommand]
    private void NewGroup()
    {
        var editor = new GroupEditorViewModel(existing: null, Core.Util.Palette.NextGroupColor(Groups.Select(g => g.Color)));
        if (ShowGroupEditor?.Invoke(editor) != true)
            return;

        WorkspaceGroup? model = null;
        _ctx.Store.Update(f => model = WorkspaceEditor.AddGroup(f, editor.Name.Trim(), editor.Color));
        Groups.Add(new GroupViewModel(model!, this, _ctx));
    }

    public void EditGroup(GroupViewModel group)
    {
        var editor = new GroupEditorViewModel(group.Model, group.Color);
        if (ShowGroupEditor?.Invoke(editor) != true)
            return;
        group.Name = editor.Name.Trim();
        group.Color = editor.Color;
    }

    /// <summary>Aba nova no grupo, do tipo escolhido; sem tipo (Ctrl+T), o shell padrão das configurações.</summary>
    public void NewTab(GroupViewModel group, ShellProfile? shell)
    {
        shell ??= _ctx.Shells.Default(_ctx.Settings.Current.DefaultShell);
        if (shell is null)
        {
            _ctx.Notifier.Show("Nenhum shell encontrado", "Cadastre um perfil em Configurações › Shells e perfis.", isError: true);
            return;
        }
        WorkspaceTab? tab = null;
        _ctx.Store.Update(_ => tab = WorkspaceEditor.AddTab(group.Model, shell));
        group.IsCollapsed = false;
        SelectedTab = group.InsertTab(tab!, group.Tabs.Count);
    }

    [RelayCommand]
    private void NewTabInCurrentGroup()
    {
        var group = SelectedTab?.Group ?? Groups.FirstOrDefault();
        if (group is null)
        {
            NewGroup();
            group = Groups.LastOrDefault();
            if (group is null)
                return;
        }
        NewTab(group, null);
    }

    public void CloseTab(TabViewModel tab)
    {
        var next = AllTabs().SkipWhile(t => t != tab).Skip(1).FirstOrDefault()
            ?? AllTabs().TakeWhile(t => t != tab).LastOrDefault();
        tab.CloseSession();
        _ctx.Store.Update(f => WorkspaceEditor.RemoveTab(f, tab.Id));
        tab.Group.Tabs.Remove(tab);
        if (SelectedTab == tab)
            SelectedTab = next;
    }

    public void RemoveGroup(GroupViewModel group)
    {
        foreach (var tab in group.Tabs)
            tab.CloseSession();
        _ctx.Store.Update(f => WorkspaceEditor.RemoveGroup(f, group.Model.Id));
        Groups.Remove(group);
        if (SelectedTab?.Group == group)
            SelectedTab = AllTabs().FirstOrDefault();
    }

    public void DuplicateTab(TabViewModel tab)
    {
        WorkspaceTab? copy = null;
        _ctx.Store.Update(f => copy = WorkspaceEditor.Duplicate(f, tab.Id));
        if (copy is not null)
            SelectedTab = tab.Group.InsertTab(copy, tab.Group.Tabs.IndexOf(tab) + 1);
    }

    /// <summary>Arrastar uma aba para outro grupo (ou outra posição). O processo dela continua rodando.</summary>
    public void MoveTab(TabViewModel tab, GroupViewModel target, int index)
    {
        _ctx.Store.Update(f => WorkspaceEditor.MoveTab(f, tab.Id, target.Model.Id, index));
        tab.Group.Tabs.Remove(tab);
        tab.Group = target;
        target.Tabs.Insert(Math.Clamp(index, 0, target.Tabs.Count), tab);
    }

    /// <summary>Uma cópia da aba na janela de administrador, depois da confirmação do UAC.</summary>
    public void OpenAsAdmin(TabViewModel tab)
    {
        // A cópia leva o shell e a pasta onde a aba está agora.
        var copy = tab.Model.CloneWithNewId();
        copy.Shell = tab.Shell?.Id ?? copy.Shell;
        copy.Cwd = tab.Cwd ?? copy.Cwd;
        var payload = new AdminTabPayload(tab.Group.Name, tab.Group.Color, copy).Encode();
        _ctx.Audit.Info("aba", $"Aba \"{tab.Name}\" aberta como administrador", $"pasta: {copy.Cwd}");
        if (!Elevation.StartAdminWindow(["--add-tab", payload]))
            _ctx.Notifier.Show("Aba de administrador cancelada", "O Windows não recebeu a confirmação do UAC.");
    }

    /// <summary>Janela de administrador: recebe a aba e a põe num grupo de mesmo nome (criado se preciso).</summary>
    public void AddTabFromPayload(string text)
    {
        if (AdminTabPayload.Decode(text) is not { } payload)
            return;
        var group = Groups.FirstOrDefault(g => g.Name == payload.GroupName);
        if (group is null)
        {
            WorkspaceGroup? model = null;
            _ctx.Store.Update(f =>
            {
                model = WorkspaceEditor.AddGroup(f, payload.GroupName);
                model.Color = payload.GroupColor;
            });
            group = new GroupViewModel(model!, this, _ctx);
            Groups.Add(group);
        }
        _ctx.Store.Update(_ => group.Model.Tabs.Add(payload.Tab));
        SelectedTab = group.InsertTab(payload.Tab, group.Tabs.Count);
    }

    [RelayCommand]
    private void CloseCurrentTab()
    {
        if (SelectedTab is { } tab)
            CloseTab(tab);
    }

    [RelayCommand]
    private void RenameCurrent()
    {
        if (SelectedTab is { } tab)
            tab.IsRenaming = true;
    }

    [RelayCommand]
    private void NextTab() => Step(+1);

    [RelayCommand]
    private void PreviousTab() => Step(-1);

    [RelayCommand]
    private void GoToTab(int index)
    {
        if (AllTabs().ElementAtOrDefault(index) is { } tab)
            SelectedTab = tab;
    }

    [RelayCommand]
    private void RestartShellCurrent() => SelectedTab?.RestartShell();

    [RelayCommand]
    private void OpenCurrentInVsCode() => SelectedTab?.OpenInVsCodeCommand.Execute(null);

    [RelayCommand]
    private void OpenHistory() => History.Open(SelectedTab?.Folder);

    /// <summary>
    /// A busca no terminal (Ctrl+Shift+F) depende do controle nativo expor a busca do Windows Terminal.
    /// Fica registrada no mapa de atalhos e é ligada quando o TerminalSession oferecer a busca (ver ARQUITETURA.md).
    /// </summary>
    [RelayCommand]
    private void FindInTerminal() =>
        _ctx.Notifier.Show("Busca no terminal ainda não disponível", "Está prevista para a fase de polimento.");

    [RelayCommand]
    private void ToggleSidebar() => SidebarCollapsed = !SidebarCollapsed;

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Reabre o shell de todas as abas abertas (vale máscara, auxílios e tema de prompt novos).</summary>
    public void RestartAllShells()
    {
        foreach (var tab in AllTabs().Where(t => t.IsStarted).ToList())
            tab.RestartShell();
    }

    /// <summary>Cola na aba atual um comando escolhido no histórico.</summary>
    public void PasteToCurrent(string command) => SelectedTab?.Paste(command);

    public void SaveWindow(WindowLayout layout) => _ctx.Store.Update(f =>
    {
        layout.SidebarCollapsed = SidebarCollapsed;
        f.Window = layout;
    });

    private void Step(int delta)
    {
        var tabs = AllTabs().ToList();
        if (tabs.Count == 0)
            return;
        var index = SelectedTab is null ? 0 : tabs.IndexOf(SelectedTab);
        SelectedTab = tabs[((index + delta) % tabs.Count + tabs.Count) % tabs.Count];
    }
}
