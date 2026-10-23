using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Models;
using Forge.Core.Util;

namespace Forge.App.ViewModels;

/// <summary>Uma cor na escolha de cor do grupo ou da aba, marcada quando é a atual.</summary>
public sealed partial class ColorOption(string hex, string name) : ObservableObject
{
    public string Hex => hex;

    public string Name => name;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>A paleta do Forge (Core.Util.Palette), uma opção por cor.</summary>
    public static IReadOnlyList<ColorOption> CreateSet() =>
        Palette.Colors.Select(c => new ColorOption(c.Hex, c.Name)).ToList();
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

    /// <summary>Qualquer cor, além da paleta: #RRGGBB (ou #RGB) digitado pelo usuário.</summary>
    [ObservableProperty]
    private string _customHex = "";

    partial void OnColorChanged(string value)
    {
        foreach (var option in Colors)
            option.IsSelected = string.Equals(option.Hex, value, StringComparison.OrdinalIgnoreCase);
        if (Palette.NormalizeHex(CustomHex) != value)
            CustomHex = value;
    }

    partial void OnCustomHexChanged(string value)
    {
        if (Palette.NormalizeHex(value) is { } hex && hex != Color)
            Color = hex;
    }

    [RelayCommand]
    private void PickColor(ColorOption option) => Color = option.Hex;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}

/// <summary>
/// Uma cor no submenu "Cor da aba" / "Cor do grupo": nome, amostra e o comando que a aplica (com <see cref="Hex"/> como
/// parâmetro). Hex null: "Do grupo" ou "Personalizada…", que abre a configuração.
/// </summary>
public sealed record ColorMenuItem(string Name, string? Hex, System.Windows.Input.ICommand Command)
{
    /// <summary>A paleta inteira com o mesmo comando, entre os itens de antes e de depois.</summary>
    public static IReadOnlyList<ColorMenuItem> Build(System.Windows.Input.ICommand setColor, IEnumerable<ColorMenuItem> before, ColorMenuItem after) =>
        [.. before, .. Palette.Colors.Select(c => new ColorMenuItem(c.Name, c.Hex, setColor)), after];
}
