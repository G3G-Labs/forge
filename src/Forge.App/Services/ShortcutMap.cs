using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using Forge.App.ViewModels;
using Forge.Core.Models;

namespace Forge.App.Services;

/// <summary>Uma ação com atalho: o nome usado no settings.json, o texto da tela e a tecla padrão.</summary>
public sealed record ShortcutAction(string Id, string Label, string DefaultGesture);

/// <summary>
/// Atalhos de teclado. Com o foco no terminal, as teclas vão para a janela nativa do controle e não passam pelos
/// KeyBindings do WPF; por isso os atalhos são lidos antes, na fila de mensagens da thread
/// (<see cref="ComponentDispatcher.ThreadPreprocessMessage"/>), valendo com o foco em qualquer lugar da janela.
/// Todos podem ser remapeados no settings.json ("shortcuts": { "NewTab": "Ctrl+Shift+T" }).
/// </summary>
public sealed class ShortcutMap : IDisposable
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;

    public static IReadOnlyList<ShortcutAction> Actions { get; } =
    [
        new("NewTab", "Nova aba no grupo atual", "Ctrl+T"),
        new("NewGroup", "Novo grupo", "Ctrl+Shift+G"),
        new("CloseTab", "Fechar aba", "Ctrl+W"),
        new("RenameTab", "Renomear aba", "F2"),
        new("NextTab", "Próxima aba", "Ctrl+Tab"),
        new("PreviousTab", "Aba anterior", "Ctrl+Shift+Tab"),
        new("RestartTab", "Iniciar / reiniciar comando da aba", "Ctrl+Shift+R"),
        new("History", "Buscar no histórico unificado", "Ctrl+R"),
        new("OpenInVsCode", "Abrir pasta no VS Code", "Ctrl+Shift+E"),
        new("FindInTerminal", "Buscar no terminal", "Ctrl+Shift+F"),
        new("ToggleSidebar", "Recolher barra lateral", "Ctrl+B"),
        new("Settings", "Configurações", "Ctrl+,"),
    ];

    private readonly Window _window;
    private readonly MainWindowViewModel _vm;
    private readonly List<(KeyGesture Gesture, string Action)> _bindings = [];

    public ShortcutMap(Window window, MainWindowViewModel vm, AppSettings settings)
    {
        _window = window;
        _vm = vm;
        Reload(settings);
        ComponentDispatcher.ThreadPreprocessMessage += OnPreprocessMessage;
    }

    /// <summary>A tecla em vigor para cada ação: a remapeada no settings.json ou a padrão.</summary>
    public static string GestureFor(ShortcutAction action, AppSettings settings) =>
        settings.Shortcuts.GetValueOrDefault(action.Id) ?? action.DefaultGesture;

    public void Reload(AppSettings settings)
    {
        _bindings.Clear();
        foreach (var action in Actions)
            if (Parse(GestureFor(action, settings)) is { } gesture)
                _bindings.Add((gesture, action.Id));
    }

    public void Dispose() => ComponentDispatcher.ThreadPreprocessMessage -= OnPreprocessMessage;

    private void OnPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (handled || (msg.message != WmKeyDown && msg.message != WmSysKeyDown) || !_window.IsActive)
            return;
        // Renomeando ou digitando num campo: as teclas são do campo.
        if (Keyboard.FocusedElement is TextBoxBase)
            return;

        var key = KeyInterop.KeyFromVirtualKey((int)msg.wParam);
        var modifiers = Keyboard.Modifiers;

        // Ctrl+1…9: ir para a aba N.
        if (modifiers == ModifierKeys.Control && key is >= Key.D1 and <= Key.D9)
        {
            _vm.GoToTabCommand.Execute(key - Key.D1);
            handled = true;
            return;
        }

        foreach (var (gesture, action) in _bindings)
        {
            if (gesture.Key == key && gesture.Modifiers == modifiers)
            {
                handled = Run(action);
                return;
            }
        }
    }

    private bool Run(string action)
    {
        var command = action switch
        {
            "NewTab" => _vm.NewTabInCurrentGroupCommand,
            "NewGroup" => _vm.NewGroupCommand,
            "CloseTab" => _vm.CloseCurrentTabCommand,
            "RenameTab" => _vm.RenameCurrentCommand,
            "NextTab" => _vm.NextTabCommand,
            "PreviousTab" => _vm.PreviousTabCommand,
            "RestartTab" => _vm.RestartCurrentCommand,
            "History" => _vm.OpenHistoryCommand,
            "OpenInVsCode" => _vm.OpenCurrentInVsCodeCommand,
            "FindInTerminal" => _vm.FindInTerminalCommand,
            "ToggleSidebar" => _vm.ToggleSidebarCommand,
            "Settings" => _vm.OpenSettingsCommand,
            _ => (ICommand?)null,
        };
        if (command is null || !command.CanExecute(null))
            return false;
        command.Execute(null);
        return true;
    }

    /// <summary>"Ctrl+Shift+R" → KeyGesture. Aceita "Ctrl+," como apelido de Ctrl+OemComma.</summary>
    internal static KeyGesture? Parse(string text)
    {
        var normalized = text.EndsWith("+,", StringComparison.Ordinal) ? text[..^1] + "OemComma" : text;
        try
        {
            return new KeyGestureConverter().ConvertFromString(null, CultureInfo.InvariantCulture, normalized) as KeyGesture;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FormatException)
        {
            return null;
        }
    }
}
