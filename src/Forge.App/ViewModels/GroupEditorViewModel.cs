using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Models;

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
/// Criar ou configurar um grupo: só nome e cor, com prévia. Como no Warp, o grupo organiza as abas; o tipo (CMD,
/// PowerShell, Git Bash, WSL) e a pasta são de cada aba.
/// </summary>
public sealed partial class GroupEditorViewModel : ObservableObject
{
    public GroupEditorViewModel(WorkspaceGroup? existing, string suggestedColor)
    {
        IsNew = existing is null;
        Name = existing?.Name ?? "";
        Color = existing?.Color ?? suggestedColor;
    }

    public bool IsNew { get; }

    public string Title => IsNew ? "Novo grupo" : "Configurar grupo";

    public string SaveLabel => IsNew ? "Criar grupo" : "Salvar";

    public IReadOnlyList<ColorOption> Colors { get; } = ColorOption.CreateSet();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewName))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [ObservableProperty]
    private string _color = "";

    public string PreviewName => string.IsNullOrWhiteSpace(Name) ? "Nome do grupo" : Name.Trim();

    /// <summary>O diálogo deve fechar: true salva, false desiste.</summary>
    public event EventHandler<bool>? CloseRequested;

    partial void OnColorChanged(string value)
    {
        foreach (var option in Colors)
            option.IsSelected = string.Equals(option.Hex, value, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void PickColor(ColorOption option) => Color = option.Hex;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
