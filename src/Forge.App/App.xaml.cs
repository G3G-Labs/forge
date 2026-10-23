using System.Windows;
using System.Windows.Threading;
using Forge.App.Services;
using Forge.App.ViewModels;
using Forge.App.Views;
using Forge.Core.Actions;
using Forge.Core.Git;
using Forge.Core.Models;
using Forge.Core.Persistence;
using Forge.Core.Processes;
using Forge.Core.Shells;
using Forge.Terminal.Native;
using Forge.Terminal.Shells;

namespace Forge.App;

public partial class App : Application
{
    private SingleInstance? _single;
    private WorkspaceStore? _workspaces;
    private SessionManager? _sessions;
    private MainWindowViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        var args = StartupArgs.Parse(e.Args);
        _single = new SingleInstance(args.Admin ? "admin" : "main", forwarded => Dispatcher.BeginInvoke(() => OnForwarded(forwarded)));
        if (!_single.IsFirst)
        {
            // Já existe uma janela deste tipo: ela recebe os argumentos e vem para a frente.
            _single.Forward(e.Args);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        // Shells e servidores encerram junto com o app, mesmo se ele cair.
        JobObject.KillChildrenOnExit();

        // Composição: tudo é criado uma vez e vive enquanto o app estiver aberto.
        var paths = new AppPaths();
        var settings = new SettingsStore(paths.Settings) { IsReadOnly = args.Admin };
        settings.Load();
        ApplyChromeTheme(settings.Current.Appearance);

        _workspaces = new WorkspaceStore(paths.Workspaces(args.Admin));
        _workspaces.Load();

        var history = new HistoryStore(paths.History, settings.Current.HistoryLimit);
        if (!args.Admin)
            TrimHistoryQuietly(history);

        var notifier = new Notifier();
        _workspaces.SaveFailed += (_, ex) => notifier.Show("Não foi possível salvar os workspaces", ex.Message, isError: true);
        if ((_workspaces.LoadProblem ?? settings.LoadProblem) is { } problem)
            notifier.Show("Atenção", problem, isError: true);

        var runner = new ProcessRunner();
        var shells = new ShellCatalog(new WindowsShellProbe(runner));
        _sessions = new SessionManager(new LaunchBuilder(paths.Scripts), settings);
        var ctx = new WorkspaceContext(_workspaces, settings, shells, _sessions, new GitService(runner), new ExternalApps(runner), history, notifier, args.Admin);

        _vm = new MainWindowViewModel(ctx);
        _vm.SettingsRequested += (_, _) => ShowSettings(settings, shells, history, notifier, paths);
        settings.Changed += (_, _) => ApplyChromeTheme(settings.Current.Appearance);

        var window = new MainWindow(_vm, settings);
        MainWindow = window;
        if (args.Minimized)
            window.WindowState = WindowState.Minimized;
        window.Show();

        _ = StartAsync(args);
    }

    private async Task StartAsync(StartupArgs args)
    {
        try
        {
            await _vm!.InitializeAsync();
            if (args.AddTab is { } payload)
                _vm.AddTabFromPayload(payload);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _vm?.Notifier.Error(ex);
        }
    }

    private void OnForwarded(string[] forwarded)
    {
        var args = StartupArgs.Parse(forwarded);
        if (args.AddTab is { } payload)
            _vm?.AddTabFromPayload(payload);
        if (MainWindow is MainWindow main)
            main.BringToFront();
    }

    private void ShowSettings(SettingsStore settings, ShellCatalog shells, HistoryStore history, Notifier notifier, AppPaths paths)
    {
        var vm = new SettingsViewModel(settings, shells, history, notifier, paths);
        vm.RestartShellsRequested += (_, _) => _vm?.RestartAllShells();
        var window = new SettingsWindow(vm) { Owner = MainWindow };
        window.ShowDialog();
    }

    /// <summary>
    /// Tema da janela: o claro acompanha o esquema "Forge Claro"; qualquer outro usa o escuro. As telas pegam as
    /// cores por DynamicResource, então trocar o dicionário repinta tudo na hora.
    /// </summary>
    private void ApplyChromeTheme(AppearanceSettings appearance)
    {
        var light = appearance.Scheme == BuiltInSchemes.ForgeLight.Name;
        var source = new Uri(light ? "Themes/Tokens.Light.xaml" : "Themes/Tokens.xaml", UriKind.Relative);
        var dictionaries = Resources.MergedDictionaries;
        const int TokensIndex = 2;
        if (dictionaries.Count > TokensIndex && dictionaries[TokensIndex].Source?.OriginalString != source.OriginalString)
            dictionaries[TokensIndex] = new ResourceDictionary { Source = source };
    }

    private static void TrimHistoryQuietly(HistoryStore history)
    {
        try
        {
            history.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fica para a próxima abertura; a busca funciona com o arquivo como está.
        }
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Um erro inesperado vira aviso na janela em vez de derrubar o app (e todos os terminais com ele).
        _vm?.Notifier.Error(e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _sessions?.Dispose();
        // Grava o que estiver pendente: fechar o app é um dos dois momentos de salvar (o outro é 500 ms após mudar).
        _workspaces?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
