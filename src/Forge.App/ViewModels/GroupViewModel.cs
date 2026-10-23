using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Models;
using Forge.Core.Workspaces;

namespace Forge.App.ViewModels;

/// <summary>Um grupo na barra lateral, como os grupos de abas do Warp: só organiza (nome, cor, recolher e as abas).</summary>
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

    /// <summary>Iniciais para a barra recolhida: "Cliente A" → "CA", "Pessoal" → "P".</summary>
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    public int RunningCount => Tabs.Count(t => t.State == TabRunState.Running);

    public bool IsEmpty => Tabs.Count == 0;

    /// <summary>"3" com o grupo aberto; "2 abas · 1 rodando" recolhido.</summary>
    public string Summary
    {
        get
        {
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
        OnPropertyChanged(nameof(IsEmpty));
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

    /// <summary>Aba nova do tipo escolhido no "+" (PowerShell, CMD, Git Bash, WSL…); sem tipo, o padrão.</summary>
    [RelayCommand]
    private void NewTab(ShellProfile? shell) => Owner.NewTab(this, shell);

    [RelayCommand]
    private void Configure() => Owner.EditGroup(this);


    [RelayCommand]
    private void Remove() => Owner.RemoveGroup(this);
}
