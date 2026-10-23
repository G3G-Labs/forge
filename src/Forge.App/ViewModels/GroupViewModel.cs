using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Models;
using Forge.Core.Workspaces;

namespace Forge.App.ViewModels;

/// <summary>Um grupo (workspace) na barra lateral: nome, cor, recolhido e as abas.</summary>
public sealed partial class GroupViewModel : ObservableObject, IRenamable
{
    private readonly WorkspaceContext _ctx;

    public GroupViewModel(WorkspaceGroup model, MainWindowViewModel owner, WorkspaceContext ctx)
    {
        _ctx = ctx;
        Model = model;
        Owner = owner;
        Name = model.Name;
        Color = model.Color;
        IsCollapsed = model.Collapsed;
        foreach (var tab in model.Tabs)
            Tabs.Add(new TabViewModel(tab, this, ctx));
        Tabs.CollectionChanged += (_, _) => Refresh();
    }

    public WorkspaceGroup Model { get; }

    public MainWindowViewModel Owner { get; }

    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _color = "";

    [ObservableProperty]
    private bool _isCollapsed;

    [ObservableProperty]
    private bool _isRenaming;

    public string? Folder => Model.Folder;

    /// <summary>"Windows · PowerShell 7" ou "WSL · Ubuntu": onde as abas do grupo rodam.</summary>
    public string EnvironmentLabel
    {
        get
        {
            var shell = _ctx.Shells.Find(Model.Shell ?? _ctx.Settings.Current.DefaultShell);
            return shell is null ? "Shell padrão" : shell.IsWsl ? $"WSL · {shell.Name}" : $"Windows · {shell.Name}";
        }
    }

    /// <summary>Dica da linha do grupo: ambiente e pasta.</summary>
    public string Details => Folder is { } folder ? $"{EnvironmentLabel}\n{folder}" : $"{EnvironmentLabel}\nsem pasta definida";

    /// <summary>Iniciais para a barra recolhida: "Cliente A" → "CA", "Pessoal" → "P".</summary>
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    public int RunningCount => Tabs.Count(t => t.State == TabRunState.Running);

    public bool HasError => Tabs.Any(t => t.State == TabRunState.Failed);

    /// <summary>"3" com o grupo aberto; "2 abas · 1 rodando" recolhido, ou "1 erro" quando alguma aba falhou.</summary>
    public string Summary
    {
        get
        {
            var errors = Tabs.Count(t => t.State == TabRunState.Failed);
            if (errors > 0)
                return errors == 1 ? "1 erro" : $"{errors} erros";
            if (!IsCollapsed)
                return Tabs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var label = Tabs.Count == 1 ? "1 aba" : $"{Tabs.Count} abas";
            return RunningCount > 0 ? $"{label} · {RunningCount} rodando" : label;
        }
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(Initials));
        if (value != Model.Name)
            _ctx.Store.Update(_ => Model.Name = value);
    }

    partial void OnColorChanged(string value)
    {
        if (value != Model.Color)
            _ctx.Store.Update(_ => Model.Color = value);
        foreach (var tab in Tabs)
            tab.GroupColorChanged();
    }

    partial void OnIsCollapsedChanged(bool value)
    {
        if (value != Model.Collapsed)
            _ctx.Store.Update(_ => Model.Collapsed = value);
        OnPropertyChanged(nameof(Summary));
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(Summary));
    }

    public TabViewModel InsertTab(WorkspaceTab tab, int index)
    {
        var vm = new TabViewModel(tab, this, _ctx);
        Tabs.Insert(Math.Clamp(index, 0, Tabs.Count), vm);
        return vm;
    }

    [RelayCommand]
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    private void BeginRename() => IsRenaming = true;

    [RelayCommand]
    private void SetColor(string color) => Color = color;

    /// <summary>"+" no grupo: aba nova já no ambiente e na pasta do grupo.</summary>
    [RelayCommand]
    private void NewTab(ShellProfile? shell) => Owner.NewTab(this, shell);

    [RelayCommand]
    private void Configure() => Owner.EditGroup(this);

    /// <summary>Configurar grupo mudou a pasta ou o ambiente: atualiza o grupo e as abas que o seguem.</summary>
    public void ModelChanged()
    {
        OnPropertyChanged(nameof(Folder));
        OnPropertyChanged(nameof(EnvironmentLabel));
        OnPropertyChanged(nameof(Details));
        foreach (var tab in Tabs)
            tab.ModelChanged();
    }

    [RelayCommand]
    private void Remove() => Owner.RemoveGroup(this);
}
