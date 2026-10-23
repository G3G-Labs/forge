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

/// <summary>
/// "Configurar aba": painel no lugar, logo abaixo do cabeçalho, sem modal. Cada campo salva ao mudar.
/// Shell, pasta e variáveis valem no próximo início do shell; o painel oferece reiniciar agora.
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

    public IReadOnlyList<ShellProfile> Shells => _ctx.Shells.Profiles;

    public ObservableCollection<EnvVarViewModel> Env { get; } = [];

    [ObservableProperty]
    private ShellProfile? _shell;

    [ObservableProperty]
    private string _cwd = "";

    [ObservableProperty]
    private string _startup = "";

    [ObservableProperty]
    private bool _autostart;

    /// <summary>Mudou algo que só vale quando o shell reabre.</summary>
    [ObservableProperty]
    private bool _needsRestart;

    private void Load()
    {
        _loading = true;
        Shell = _ctx.Shells.Find(Model.Shell);
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
        _loading = false;
    }

    partial void OnShellChanged(ShellProfile? value)
    {
        if (_loading || value is null || value.Id == Model.Shell)
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
    private void AddEnv() => Env.Add(new EnvVarViewModel(this));

    [RelayCommand]
    private void RestartNow()
    {
        NeedsRestart = false;
        _tab.RestartShell();
    }

    [RelayCommand]
    private void Close() => _tab.IsConfigOpen = false;

    private void Save(Action<WorkspaceTab> change, bool restart)
    {
        _ctx.Store.Update(_ => change(Model));
        _tab.ModelChanged();
        if (restart && _tab.State != TabRunState.NotStarted)
            NeedsRestart = true;
    }
}
