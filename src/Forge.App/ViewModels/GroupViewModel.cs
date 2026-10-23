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

    /// <summary>"+" no grupo: sem shell escolhido, usa o padrão das configurações.</summary>
    [RelayCommand]
    private void NewTab(ShellProfile? shell) => Owner.NewTab(this, shell);

    /// <summary>Sobe todas as abas com comando, uma de cada vez (sem rajada de processos).</summary>
    [RelayCommand]
    private async Task StartGroupAsync()
    {
        foreach (var tab in Tabs.Where(t => t.HasStartup && !t.IsRunning).ToList())
        {
            tab.EnsureStarted(runStartup: true);
            await Task.Delay(150);
        }
    }

    [RelayCommand]
    private void StopGroup()
    {
        foreach (var tab in Tabs.Where(t => t.IsRunning))
            tab.StopCommand.Execute(null);
    }

    [RelayCommand]
    private void Remove() => Owner.RemoveGroup(this);
}
