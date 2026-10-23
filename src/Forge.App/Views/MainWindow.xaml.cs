using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Forge.App.Services;
using Forge.App.ViewModels;
using Forge.Core.Models;
using Forge.Core.Persistence;

namespace Forge.App.Views;

/// <summary>A janela: três áreas fixas (barra lateral, terminal e barra de status) e a barra de título própria.</summary>
public partial class MainWindow : Window
{
    private readonly ShortcutMap _shortcuts;
    private readonly SettingsStore _settings;

    public MainWindow(MainWindowViewModel vm, SettingsStore settings)
    {
        InitializeComponent();
        _settings = settings;
        ViewModel = vm;
        DataContext = vm;

        if (settings.Current.RestoreWindow)
            RestoreLayout(vm.Context.Store.Current.Window);

        // A janela de administrador é marcada em amarelo, para nunca ser confundida com a comum.
        if (vm.IsAdmin)
        {
            WindowFrame.BorderBrush = (Brush)FindResource("Forge.AdminBrush");
            WindowFrame.BorderThickness = new Thickness(1, 3, 1, 1);
            Title = "Forge — Administrador";
        }

        // Criar e configurar grupo: a única janela além das Configurações.
        vm.ShowGroupEditor = editor => new GroupDialog(editor) { Owner = this }.ShowDialog() == true;

        _shortcuts = new ShortcutMap(this, vm, settings.Current);
        settings.Changed += (_, _) => _shortcuts.Reload(settings.Current);
        ToastPopup.CustomPopupPlacementCallback = PlaceToast;
        StateChanged += (_, _) => FitMaximized();
        Closing += OnClosing;
    }

    public MainWindowViewModel ViewModel { get; }

    /// <summary>Fechar de verdade (Sair pelo ícone da bandeja), em vez de só esconder a janela.</summary>
    public bool AllowClose { get; set; }

    /// <summary>A janela foi para a bandeja: os terminais continuam rodando.</summary>
    public event EventHandler? HiddenToTray;

    public void BringToFront()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void RestoreLayout(WindowLayout layout)
    {
        Width = Math.Max(MinWidth, layout.Width);
        Height = Math.Max(MinHeight, layout.Height);
        if (layout.Left is { } left && layout.Top is { } top && IsOnScreen(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        if (layout.Maximized)
            WindowState = WindowState.Maximized;
    }

    // Um monitor que não existe mais (notebook fora da dock) não pode esconder a janela.
    private static bool IsOnScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft - 50
        && top >= SystemParameters.VirtualScreenTop - 50
        && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
        && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var bounds = RestoreBounds;
        ViewModel.SaveWindow(new WindowLayout
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = WindowState == WindowState.Maximized,
        });

        // Fechar para a bandeja: a janela some, os shells e o que roda neles continuam. A de administrador fecha mesmo.
        if (!AllowClose && _settings.Current.CloseToTray && !ViewModel.IsAdmin)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
            return;
        }

        _shortcuts.Dispose();
        if (!AllowClose)
            Application.Current.Shutdown();
    }

    // Maximizada com WindowChrome, a janela passa um pouco da tela: a borda compensa.
    private void FitMaximized() =>
        WindowFrame.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);

    // Aviso no canto inferior direito, logo acima da barra de status.
    private CustomPopupPlacement[] PlaceToast(Size popupSize, Size targetSize, Point offset) =>
        [new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width - 16, -popupSize.Height - 12), PopupPrimaryAxis.None)];

    private void OnHistoryOpened(object? sender, EventArgs e) => HistoryView.FocusSearch();

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
