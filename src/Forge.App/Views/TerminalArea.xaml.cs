using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Forge.App.Services;
using Forge.App.ViewModels;
using Forge.Core.Actions;
using Forge.Core.ShellIntegration;
using Forge.Terminal;

namespace Forge.App.Views;

/// <summary>
/// Os terminais nativos: um controle por aba iniciada, todos no mesmo lugar, e só o da aba selecionada visível.
/// Trocar de aba ou recolher um grupo não fecha nada: os processos continuam rodando por trás.
/// </summary>
public partial class TerminalArea : UserControl
{
    private const int WmKeyDown = 0x0100;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int VkTab = 0x09;
    private const int VkC = 0x43;
    private const int VkV = 0x56;

    private MainWindowViewModel? _vm;
    private SessionManager? _sessions;

    public TerminalArea()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as MainWindowViewModel);
        ComponentDispatcher.ThreadPreprocessMessage += OnThreadMessage;
        Dispatcher.ShutdownStarted += (_, _) => ComponentDispatcher.ThreadPreprocessMessage -= OnThreadMessage;
    }

    /// <summary>
    /// Teclas e cliques que o terminal nativo não trata como o Forge quer, lidos antes de chegarem a ele:
    /// - Tab e Shift+Tab são do shell (autocomplete). Sem isto, o WPF trata o Tab como navegação entre controles e o move
    ///   para outro lugar da janela. A tecla vai direto para o shell: Tab como "\t" e Shift+Tab como ESC [ Z, como faz o
    ///   Windows Terminal.
    /// - Ctrl+Shift+C e Ctrl+Shift+V copiam a seleção e colam, como no Windows Terminal.
    /// - O clique direito abre o menu (Copiar, Colar, ações da aba), no lugar do copiar/colar direto do controle.
    /// </summary>
    private void OnThreadMessage(ref MSG msg, ref bool handled)
    {
        if (handled || !IsVisible || _sessions is null)
            return;
        if (msg.message is not (WmKeyDown or WmRButtonDown or WmRButtonUp))
            return;
        // Só o que vai para uma janela filha da janela do app, ou seja, o terminal nativo.
        var main = Window.GetWindow(this) is { } window ? new WindowInteropHelper(window).Handle : IntPtr.Zero;
        if (main == IntPtr.Zero || msg.hwnd == main || !IsChild(main, msg.hwnd))
            return;
        if (_vm?.SelectedTab is not { } tab || _sessions.Find(tab.Id) is not { } session)
            return;

        if (msg.message == WmRButtonDown)
        {
            ShowMenu(tab, session);
            handled = true;
            return;
        }
        if (msg.message == WmRButtonUp)
        {
            handled = true;
            return;
        }

        var key = (int)msg.wParam;
        var modifiers = Keyboard.Modifiers;
        if (key == VkTab && modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            session.Send(modifiers == ModifierKeys.Shift ? "\x1b[Z" : "\t");
            handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key is VkC or VkV)
        {
            if (key == VkC)
                Copy(session.SelectedText());
            else
                Paste(session);
            handled = true;
        }
    }

    // Menu do clique direito no terminal. Menus do WPF abrem numa janela própria, então aparecem por cima do controle nativo.
    private void ShowMenu(TabViewModel tab, TerminalSession session)
    {
        var selection = session.SelectedText();
        var menu = new ContextMenu { PlacementTarget = session.Control, Placement = PlacementMode.MousePoint };
        menu.Items.Add(Item("Copiar", "Ctrl+Shift+C", selection.Length > 0, () => Copy(selection)));
        menu.Items.Add(Item("Colar", "Ctrl+Shift+V", ClipboardHasText(), () => Paste(session)));
        AddLinks(menu, session, selection);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Copiar caminho da pasta", Command = tab.CopyPathCommand });
        menu.Items.Add(new MenuItem { Header = "Abrir pasta no Explorer", Command = tab.OpenInExplorerCommand });
        menu.Items.Add(new MenuItem { Header = "Abrir pasta no VS Code", InputGestureText = "Ctrl+Shift+E", Command = tab.OpenInVsCodeCommand });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Reiniciar shell", InputGestureText = "Ctrl+Shift+R", Command = tab.RestartShellCommand });
        // Fechado o menu, o teclado volta para o terminal.
        menu.Closed += (_, _) => Dispatcher.BeginInvoke(() => session.Control.Focus(), DispatcherPriority.Input);
        menu.IsOpen = true;
    }

    /// <summary>
    /// Links para abrir no navegador: o da seleção e os que apareceram na saída (o "http://localhost:5173" do bun dev).
    /// O controle nativo não diz qual texto está sob o mouse, então o Ctrl+clique não é possível: o caminho é este menu.
    /// </summary>
    private void AddLinks(ContextMenu menu, TerminalSession session, string selection)
    {
        if (_vm is not { } vm || !vm.Context.Settings.Current.Helpers.ClickableLinks)
            return;
        var selected = LinkTracker.Find(selection).Where(ExternalApps.IsWebLink).ToList();
        var recent = session.RecentLinks().Where(ExternalApps.IsWebLink).Except(selected).Take(8).ToList();
        if (selected.Count == 0 && recent.Count == 0)
            return;

        menu.Items.Add(new Separator());
        foreach (var url in selected.Take(3))
            menu.Items.Add(Item("Abrir no navegador: " + Short(url), "", true, () => OpenUrl(url)));
        if (recent.Count == 0)
            return;
        var recentMenu = new MenuItem { Header = "Links recentes do terminal" };
        foreach (var url in recent)
            recentMenu.Items.Add(Item(Short(url), "", true, () => OpenUrl(url)));
        menu.Items.Add(recentMenu);
    }

    private void OpenUrl(string url)
    {
        try
        {
            _vm?.Context.Apps.OpenUrl(url);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            _vm?.Notifier.Show("Não foi possível abrir o link", ex.Message, isError: true);
        }
    }

    // Links longos (tokens, query strings) ficam com reticências no menu; o link aberto é o inteiro.
    private static string Short(string url) => url.Length <= 72 ? url : url[..69] + "…";

    private static MenuItem Item(string header, string gesture, bool enabled, Action action)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    private void Copy(string text)
    {
        if (text.Length == 0)
            return;
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException)
        {
            // Área de transferência presa por outro programa: o usuário tenta de novo.
            _vm?.Notifier.Show("Não foi possível copiar", "A área de transferência está em uso por outro programa.", isError: true);
        }
    }

    private void Paste(TerminalSession session)
    {
        try
        {
            if (Clipboard.ContainsText())
                session.Paste(Clipboard.GetText());
        }
        catch (ExternalException)
        {
            _vm?.Notifier.Show("Não foi possível colar", "A área de transferência está em uso por outro programa.", isError: true);
        }
    }

    private static bool ClipboardHasText()
    {
        try
        {
            return Clipboard.ContainsText();
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(IntPtr parent, IntPtr child);

    private void Attach(MainWindowViewModel? vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelChanged;
        if (_sessions is not null)
        {
            _sessions.Added -= OnSessionAdded;
            _sessions.Removed -= OnSessionRemoved;
        }

        _vm = vm;
        _sessions = vm?.Context.Sessions;
        if (vm is null || _sessions is null)
            return;

        vm.PropertyChanged += OnViewModelChanged;
        _sessions.Added += OnSessionAdded;
        _sessions.Removed += OnSessionRemoved;
        foreach (var session in _sessions.All)
            Add(session);
        ShowCurrent();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTab))
            ShowCurrent();
    }

    private void OnSessionAdded(object? sender, TerminalSession session)
    {
        Add(session);
        ShowCurrent();
    }

    private void OnSessionRemoved(object? sender, TerminalSession session)
    {
        TermHost.Children.Remove(session.Control);
        ShowCurrent();
    }

    private void Add(TerminalSession session)
    {
        if (!TermHost.Children.Contains(session.Control))
            TermHost.Children.Add(session.Control);
    }

    private void ShowCurrent()
    {
        if (_sessions is null)
            return;
        var current = _vm?.SelectedTab?.Id;
        TerminalSession? visible = null;
        foreach (var session in _sessions.All)
        {
            var show = session.TabId == current;
            session.Control.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
                visible = session;
        }
        EmptyText.Visibility = visible is null ? Visibility.Visible : Visibility.Collapsed;
        if (visible is not null)
            Dispatcher.BeginInvoke(() => visible.Control.Focus(), DispatcherPriority.Input);
    }
}
