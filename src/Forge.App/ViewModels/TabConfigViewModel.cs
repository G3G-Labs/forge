using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Models;
using Forge.Core.Workspaces;

namespace Forge.App.ViewModels;

/// <summary>Uma variável de ambiente da aba (linha editável no painel).</summary>
public sealed partial class EnvVarViewModel(TabConfigViewModel owner) : ObservableObject
{
    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _value = "";

    partial void OnKeyChanged(string value) => owner.SaveEnv();

    partial void OnValueChanged(string value) => owner.SaveEnv();

    [RelayCommand]
    private void Remove() => owner.RemoveEnv(this);
}

/// <summary>Um tipo de aba na lista do painel ("Windows · PowerShell 7", "WSL · Debian").</summary>
public sealed record ShellChoice(string? Id, string Label);

/// <summary>
/// "Configurar aba": painel no lugar, logo abaixo do cabeçalho, sem modal. Nome e cor mudam na hora, com prévia;
/// shell, pasta e variáveis valem no próximo shell, e o painel oferece reiniciar agora.
/// </summary>
public sealed partial class TabConfigViewModel : ObservableObject
{
    private readonly TabViewModel _tab;
    private readonly WorkspaceContext _ctx;
    private bool _loading;

    public TabConfigViewModel(TabViewModel tab, WorkspaceContext ctx)
    {
        _tab = tab;
        _ctx = ctx;
        Load();
    }

    private WorkspaceTab Model => _tab.Model;

    public ObservableCollection<EnvVarViewModel> Env { get; } = [];

    public IReadOnlyList<ColorOption> Colors { get; } = ColorOption.CreateSet();

    [ObservableProperty]
    private IReadOnlyList<ShellChoice> _shellChoices = [];

    [ObservableProperty]
    private ShellChoice? _shellChoice;

    [ObservableProperty]
    private string _cwd = "";

    [ObservableProperty]
    private string _startup = "";

    [ObservableProperty]
    private bool _autostart;

    /// <summary>Mudou algo que só vale quando o shell reabre.</summary>
    [ObservableProperty]
    private bool _needsRestart;

    /// <summary>Nome da aba, o mesmo da barra lateral.</summary>
    public string Name
    {
        get => _tab.Name;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && value != _tab.Name)
            {
                _tab.Name = value.Trim();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Cor que a aba mostra agora (a própria ou a do grupo), para a prévia.</summary>
    public string PreviewColor => _tab.Color;

    public bool UsesGroupColor => Model.Color is null;

    /// <summary>O que aparece no campo de pasta vazio.</summary>
    public string FolderPlaceholder => "Vazio: volta na última pasta visitada (ou na home)";

    /// <summary>Recarrega do modelo (ao abrir o painel): o grupo pode ter mudado de ambiente ou de pasta.</summary>
    public void Load()
    {
        _loading = true;
        ShellChoices = _ctx.Shells.Profiles
            .Select(p => new ShellChoice(p.Id, p.IsWsl ? $"WSL · {p.Name}" : p.Kind == ShellKind.Custom ? p.Name : $"Windows · {p.Name}"))
            .ToList();
        ShellChoice = ShellChoices.FirstOrDefault(c => c.Id == _tab.ShellId);
        Cwd = Model.Cwd ?? "";
        Startup = Model.Startup ?? "";
        Autostart = Model.Autostart;
        Env.Clear();
        foreach (var (key, value) in Model.Env)
        {
            var row = new EnvVarViewModel(this);
            row.Key = key;
            row.Value = value;
            Env.Add(row);
        }
        RefreshColor();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FolderPlaceholder));
        _loading = false;
    }

    partial void OnShellChoiceChanged(ShellChoice? value)
    {
        if (_loading || value is null || value.Id == _tab.ShellId)
            return;
        Save(m => m.Shell = value.Id, restart: true);
    }

    partial void OnCwdChanged(string value)
    {
        if (!_loading)
            Save(m => m.Cwd = string.IsNullOrWhiteSpace(value) ? null : value.Trim(), restart: true);
    }

    partial void OnStartupChanged(string value)
    {
        if (!_loading)
            Save(m => m.Startup = string.IsNullOrWhiteSpace(value) ? null : value.Trim(), restart: false);
    }

    partial void OnAutostartChanged(bool value)
    {
        if (!_loading)
            Save(m => m.Autostart = value, restart: false);
    }

    internal void SaveEnv()
    {
        if (_loading)
            return;
        Save(m =>
        {
            m.Env.Clear();
            foreach (var row in Env.Where(r => !string.IsNullOrWhiteSpace(r.Key)))
                m.Env[row.Key.Trim()] = row.Value;
        }, restart: true);
    }

    internal void RemoveEnv(EnvVarViewModel row)
    {
        Env.Remove(row);
        SaveEnv();
    }

    [RelayCommand]
    private void PickColor(ColorOption option) => SetColor(option.Hex);

    [RelayCommand]
    private void UseGroupColor() => SetColor(null);

    /// <summary>A pasta onde a aba está agora vira a pasta fixa dela.</summary>
    [RelayCommand]
    private void UseCurrentFolder()
    {
        if (_tab.Cwd is { } folder)
            Cwd = folder;
    }

    [RelayCommand]
    private void AddEnv() => Env.Add(new EnvVarViewModel(this));

    [RelayCommand]
    private void RestartNow()
    {
        NeedsRestart = false;
        _tab.RestartShell();
    }

    [RelayCommand]
    private void Close() => _tab.IsConfigOpen = false;

    private void SetColor(string? hex)
    {
        _ctx.Store.Update(_ => Model.Color = hex);
        _tab.ModelChanged();
        RefreshColor();
    }

    private void RefreshColor()
    {
        foreach (var option in Colors)
            option.IsSelected = string.Equals(option.Hex, Model.Color, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(PreviewColor));
        OnPropertyChanged(nameof(UsesGroupColor));
    }

    private void Save(Action<WorkspaceTab> change, bool restart)
    {
        _ctx.Store.Update(_ => change(Model));
        _tab.ModelChanged();
        if (restart && _tab.State != TabRunState.NotStarted)
            NeedsRestart = true;
    }
}
