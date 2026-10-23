using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.App.Services;
using Forge.Core.Appearance;
using Forge.Core.Models;
using Forge.Core.Persistence;
using Forge.Core.Shells;
using Forge.Terminal.Native;
using Microsoft.Win32;

namespace Forge.App.ViewModels;

/// <summary>Um shell na lista de Configurações › Shells, com a chave de integração.</summary>
public sealed partial class ShellRowViewModel(ShellProfile profile, SettingsStore settings) : ObservableObject
{
    public ShellProfile Profile => profile;

    public bool IsDefault => settings.Current.DefaultShell == profile.Id;

    public bool CanIntegrate => profile.SupportsIntegration && profile.Kind != ShellKind.Custom;

    public bool Integration
    {
        get => !settings.Current.IntegrationDisabled.Contains(profile.Id);
        set
        {
            settings.Update(s =>
            {
                s.IntegrationDisabled.Remove(profile.Id);
                if (!value)
                    s.IntegrationDisabled.Add(profile.Id);
            });
            OnPropertyChanged();
        }
    }

    public void RefreshDefault() => OnPropertyChanged(nameof(IsDefault));
}

/// <summary>Uma linha da tabela de atalhos.</summary>
public sealed record ShortcutRow(string Label, string Gesture);

/// <summary>
/// Configurações globais (Ctrl+,): o único modal do app. Seções Geral, Shells e perfis, Aparência, Terminal,
/// Atalhos e Sobre. Tudo salva na hora, em settings.json.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly HistoryStore _history;
    private readonly Notifier _notifier;
    private readonly AppPaths _paths;

    public SettingsViewModel(SettingsStore settings, ShellCatalog shells, HistoryStore history, Notifier notifier, AppPaths paths)
    {
        _settings = settings;
        _history = history;
        _notifier = notifier;
        _paths = paths;
        About = new AboutViewModel(notifier);
        foreach (var profile in shells.Profiles)
            ShellRows.Add(new ShellRowViewModel(profile, settings));
        foreach (var action in ShortcutMap.Actions)
            Shortcuts.Add(new ShortcutRow(action.Label, ShortcutMap.GestureFor(action, settings.Current)));
        Shortcuts.Add(new ShortcutRow("Ir para a aba 1–9", "Ctrl+1…9"));
    }

    private AppSettings S => _settings.Current;

    public IReadOnlyList<string> Sections { get; } = ["Geral", "Shells e perfis", "Aparência", "Terminal", "Atalhos", "Sobre"];

    [ObservableProperty]
    private string _section = "Geral";

    public AboutViewModel About { get; }

    public ObservableCollection<ShellRowViewModel> ShellRows { get; } = [];

    public ObservableCollection<ShortcutRow> Shortcuts { get; } = [];

    public string DataFolder => _paths.Root;

    // Geral

    public bool StartWithWindows
    {
        get => S.StartWithWindows;
        set
        {
            StartupRegistration.Set(value);
            Set(s => s.StartWithWindows = value);
        }
    }

    public bool CloseToTray
    {
        get => S.CloseToTray;
        set => Set(s => s.CloseToTray = value);
    }

    public bool RestoreWindow
    {
        get => S.RestoreWindow;
        set => Set(s => s.RestoreWindow = value);
    }

    public bool RestoreLastFolder
    {
        get => S.FolderOnRestore == FolderOnRestore.LastVisited;
        set => Set(s => s.FolderOnRestore = value ? FolderOnRestore.LastVisited : FolderOnRestore.StartFolder);
    }

    public int HistoryLimit
    {
        get => S.HistoryLimit;
        set
        {
            Set(s => s.HistoryLimit = Math.Max(1000, value));
            _history.Limit = S.HistoryLimit;
        }
    }

    public bool FileLog
    {
        get => S.FileLog;
        set => Set(s => s.FileLog = value);
    }

    public bool CheckUpdates
    {
        get => S.CheckUpdates;
        set => Set(s => s.CheckUpdates = value);
    }

    // Shells e perfis

    [ObservableProperty]
    private string _newProfileName = "";

    [ObservableProperty]
    private string _newProfileExecutable = "";

    [ObservableProperty]
    private string _newProfileArguments = "";

    [RelayCommand]
    private void SetDefaultShell(ShellRowViewModel row)
    {
        Set(s => s.DefaultShell = row.Profile.Id);
        foreach (var r in ShellRows)
            r.RefreshDefault();
    }

    [RelayCommand]
    private void AddProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName) || string.IsNullOrWhiteSpace(NewProfileExecutable))
            return;
        var profile = new ShellProfile
        {
            Id = ShellIds.Custom(NewProfileName.Trim()),
            Name = NewProfileName.Trim(),
            Executable = NewProfileExecutable.Trim(),
            Arguments = NewProfileArguments.Trim(),
        };
        Set(s => s.CustomProfiles.Add(profile));
        ShellRows.Add(new ShellRowViewModel(profile, _settings));
        NewProfileName = NewProfileExecutable = NewProfileArguments = "";
        _notifier.Show("Perfil adicionado", "Ele aparece no \"+\" dos grupos depois de reabrir o app.");
    }

    [RelayCommand]
    private void RemoveProfile(ShellRowViewModel row)
    {
        Set(s => s.CustomProfiles.RemoveAll(p => p.Id == row.Profile.Id));
        ShellRows.Remove(row);
    }

    // Aparência

    public IReadOnlyList<ColorScheme> Schemes => [.. BuiltInSchemes.All, .. S.Appearance.ImportedSchemes];

    public ColorScheme? Scheme
    {
        get => Schemes.FirstOrDefault(s => s.Name == S.Appearance.Scheme);
        set
        {
            if (value is not null)
                Set(s => s.Appearance.Scheme = value.Name);
        }
    }

    public string FontFamily
    {
        get => S.Appearance.FontFamily;
        set => Set(s => s.Appearance.FontFamily = value);
    }

    public double FontSize
    {
        get => S.Appearance.FontSize;
        set => Set(s => s.Appearance.FontSize = Math.Clamp(value, 8, 32));
    }

    public CursorShape Cursor
    {
        get => S.Appearance.Cursor;
        set => Set(s => s.Appearance.Cursor = value);
    }

    public IReadOnlyList<CursorShape> CursorShapes { get; } = Enum.GetValues<CursorShape>();

    public bool CursorBlink
    {
        get => S.Appearance.CursorBlink;
        set => Set(s => s.Appearance.CursorBlink = value);
    }

    /// <summary>Importa esquemas de um settings.json do Windows Terminal (ou de um arquivo com um esquema).</summary>
    [RelayCommand]
    private void ImportScheme()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json", Title = "Importar tema do Windows Terminal" };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            var schemes = WindowsTerminalImport.Parse(File.ReadAllText(dialog.FileName));
            Set(s =>
            {
                foreach (var scheme in schemes)
                {
                    s.Appearance.ImportedSchemes.RemoveAll(x => x.Name == scheme.Name);
                    s.Appearance.ImportedSchemes.Add(scheme);
                }
            });
            OnPropertyChanged(nameof(Schemes));
            _notifier.Show(schemes.Count == 1 ? "1 tema importado" : $"{schemes.Count} temas importados");
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            _notifier.Show("Não foi possível importar", ex.Message, isError: true);
        }
    }

    // Terminal: identificação e auxílios

    public string IdentityUser
    {
        get => S.Identity.UserName ?? "";
        set => Set(s => s.Identity.UserName = string.IsNullOrWhiteSpace(value) ? null : value.Trim(), nameof(IdentityPreview));
    }

    public string IdentityMachine
    {
        get => S.Identity.MachineName ?? "";
        set => Set(s => s.Identity.MachineName = string.IsNullOrWhiteSpace(value) ? null : value.Trim(), nameof(IdentityPreview));
    }

    public string IdentityPreview => S.Identity.Display(Environment.UserName, Environment.MachineName);

    public bool ShowIdentity
    {
        get => S.Identity.ShowInHeader;
        set => Set(s => s.Identity.ShowInHeader = value);
    }

    /// <summary>Máscara no prompt do PowerShell, bash e zsh: "usuário@máquina:pasta", com os nomes acima.</summary>
    public bool MaskPrompt
    {
        get => S.Identity.MaskPrompt;
        set => Set(s => s.Identity.MaskPrompt = value);
    }

    /// <summary>A máscara e os auxílios valem quando o shell abre: este pedido reabre os shells das abas abertas.</summary>
    public event EventHandler? RestartShellsRequested;

    [RelayCommand]
    private void RestartShells()
    {
        RestartShellsRequested?.Invoke(this, EventArgs.Empty);
        _notifier.Show("Shells reabertos", "As abas abertas já usam as configurações novas.");
    }

    public bool CaseInsensitiveCompletion
    {
        get => S.Helpers.CaseInsensitiveCompletion;
        set => Set(s => s.Helpers.CaseInsensitiveCompletion = value);
    }

    public bool FlushHistory
    {
        get => S.Helpers.FlushHistoryEachCommand;
        set => Set(s => s.Helpers.FlushHistoryEachCommand = value);
    }

    public bool ShellIntegration
    {
        get => S.Helpers.ShellIntegration;
        set => Set(s => s.Helpers.ShellIntegration = value);
    }

    public bool ClickableLinks
    {
        get => S.Helpers.ClickableLinks;
        set => Set(s => s.Helpers.ClickableLinks = value);
    }

    // Dados

    [RelayCommand]
    private void ClearHistory()
    {
        _history.Clear();
        _notifier.Show("Histórico limpo");
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(_paths.Root);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_paths.Root}\"") { UseShellExecute = true })?.Dispose();
    }

    [RelayCommand]
    private void OpenSettingsFile() =>
        Process.Start(new ProcessStartInfo(_paths.Settings) { UseShellExecute = true })?.Dispose();

    [RelayCommand]
    private void Select(string section) => Section = section;

    private void Set(Action<AppSettings> change, string? alsoNotify = null, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        _settings.Update(change);
        OnPropertyChanged(property);
        if (alsoNotify is not null)
            OnPropertyChanged(alsoNotify);
    }
}
