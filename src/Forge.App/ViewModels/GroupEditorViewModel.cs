using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Models;
using Forge.Core.Shells;
using Microsoft.Win32;

namespace Forge.App.ViewModels;

/// <summary>Uma cor na escolha de cor do grupo ou da aba, marcada quando é a atual.</summary>
public sealed partial class ColorOption(string hex, string name) : ObservableObject
{
    public string Hex => hex;

    public string Name => name;

    [ObservableProperty]
    private bool _isSelected;

    public static IReadOnlyList<ColorOption> CreateSet() =>
    [
        new("#4F8EF7", "Azul"),
        new("#A47CF3", "Violeta"),
        new("#2BB5B0", "Turquesa"),
        new("#E58AC1", "Rosa"),
        new("#8A9BB0", "Ardósia"),
        new("#C9A27A", "Areia"),
    ];
}

/// <summary>
/// Criar ou configurar um grupo. O grupo é um projeto: nome, cor, a pasta onde as abas abrem e o ambiente em que
/// elas rodam (Windows, com o shell escolhido, ou WSL, com a distro).
/// </summary>
public sealed partial class GroupEditorViewModel : ObservableObject
{
    public GroupEditorViewModel(IReadOnlyList<ShellProfile> shells, string defaultShell, WorkspaceGroup? existing, string suggestedColor)
    {
        IsNew = existing is null;
        WindowsShells = shells.Where(s => !s.IsWsl).ToList();
        Distros = shells.Where(s => s.IsWsl).ToList();

        var current = shells.FirstOrDefault(s => s.Id == (existing?.Shell ?? defaultShell));
        WindowsShell = current is { IsWsl: false }
            ? current
            : WindowsShells.FirstOrDefault(s => s.Id == defaultShell) ?? WindowsShells.FirstOrDefault();
        Distro = current is { IsWsl: true } ? current : Distros.FirstOrDefault();
        IsWsl = current is { IsWsl: true };

        Name = existing?.Name ?? "";
        Folder = existing?.Folder ?? "";
        Color = existing?.Color ?? suggestedColor;
    }

    public bool IsNew { get; }

    public string Title => IsNew ? "Novo grupo" : "Configurar grupo";

    public string SaveLabel => IsNew ? "Criar grupo" : "Salvar";

    public IReadOnlyList<ColorOption> Colors { get; } = ColorOption.CreateSet();

    public IReadOnlyList<ShellProfile> WindowsShells { get; }

    public IReadOnlyList<ShellProfile> Distros { get; }

    public bool HasWsl => Distros.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewName))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [ObservableProperty]
    private string _color = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewDetail))]
    private string _folder = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWindows), nameof(FolderHint), nameof(PreviewDetail), nameof(ShellId))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isWsl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewDetail), nameof(ShellId))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private ShellProfile? _windowsShell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewDetail), nameof(ShellId))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private ShellProfile? _distro;

    public bool IsWindows
    {
        get => !IsWsl;
        set => IsWsl = !value;
    }

    /// <summary>O ambiente escolhido, como perfil de shell ("pwsh", "wsl:Ubuntu").</summary>
    public string? ShellId => IsWsl ? Distro?.Id : WindowsShell?.Id;

    public string PreviewName => string.IsNullOrWhiteSpace(Name) ? "Nome do grupo" : Name.Trim();

    public string PreviewDetail
    {
        get
        {
            var env = IsWsl ? $"WSL · {Distro?.Name ?? "sem distro"}" : $"Windows · {WindowsShell?.Name ?? "sem shell"}";
            return string.IsNullOrWhiteSpace(Folder) ? env : $"{env} · {Folder.Trim()}";
        }
    }

    public string FolderHint => IsWsl
        ? "Caminho do Linux (~/projetos/app) ou uma pasta do Windows, que no WSL vira /mnt/c/…"
        : @"Ex.: C:\dev\meu-projeto. Toda aba do grupo abre aqui.";

    /// <summary>O diálogo deve fechar: true salva, false desiste.</summary>
    public event EventHandler<bool>? CloseRequested;

    partial void OnColorChanged(string value)
    {
        foreach (var option in Colors)
            option.IsSelected = string.Equals(option.Hex, value, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void PickColor(ColorOption option) => Color = option.Hex;

    /// <summary>Escolher a pasta. No WSL, uma pasta de \\wsl.localhost vira o caminho do Linux.</summary>
    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFolderDialog { Title = "Pasta do projeto" };
        if (IsWsl && Distro?.Distro is { } distro)
            dialog.InitialDirectory = $@"\\wsl.localhost\{distro}\home";
        else if (Directory.Exists(Folder))
            dialog.InitialDirectory = Folder;
        if (dialog.ShowDialog() != true)
            return;
        var path = dialog.FolderName;
        Folder = IsWsl && PathMapper.IsWslUnc(path) ? PathMapper.ToWsl(path) : path;
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && ShellId is not null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
