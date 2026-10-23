using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Forge.App.Services;
using Forge.App.ViewModels;
using Forge.App.Views;
using Forge.Core.Actions;
using Forge.Core.Diagnostics;
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
    private TrayService? _tray;
    private AuditLog? _audit;
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
        // Shells e servidores encerram junto com o app, mesmo se ele cair.
        JobObject.KillChildrenOnExit();

        // Composição: tudo é criado uma vez e vive enquanto o app estiver aberto.
        var paths = new AppPaths();
        var settings = new SettingsStore(paths.Settings) { IsReadOnly = args.Admin };
        settings.Load();
        ApplyChromeTheme(settings.Current.Appearance);

        // O log de auditoria vem primeiro: daqui em diante, todo erro tem onde ser gravado.
        var audit = _audit = new AuditLog(paths.Logs, args.Admin ? "admin" : "main") { Enabled = settings.Current.AuditLog };
        settings.Changed += (_, _) => audit.Enabled = settings.Current.AuditLog;
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnFatal;
        TaskScheduler.UnobservedTaskException += (_, ev) => audit.Error("erro", "Erro em tarefa de fundo", ev.Exception);
        audit.Info("app", "Forge iniciado", RuntimeInfo());
        if (!args.Admin)
            audit.Prune();

        _workspaces = new WorkspaceStore(paths.Workspaces(args.Admin));
        _workspaces.Load();

        var history = new HistoryStore(paths.History, settings.Current.HistoryLimit);
        if (!args.Admin)
            TrimHistoryQuietly(history, audit);

        var notifier = new Notifier(audit);
        _workspaces.SaveFailed += (_, ex) => notifier.Error(ex, "Não foi possível salvar os workspaces");
        if ((_workspaces.LoadProblem ?? settings.LoadProblem) is { } problem)
            notifier.Show("Atenção", problem, isError: true);

        var runner = new ProcessRunner(audit);
        var shells = new ShellCatalog(new WindowsShellProbe(runner));
        _sessions = new SessionManager(new LaunchBuilder(paths.Scripts), settings, audit);
        var ctx = new WorkspaceContext(_workspaces, settings, shells, _sessions, new GitService(runner), new ExternalApps(runner), history, notifier, audit, args.Admin);

        _vm = new MainWindowViewModel(ctx);
        _vm.SettingsRequested += (_, _) => ShowSettings(settings, shells, history, notifier, paths);
        settings.Changed += (_, _) => ApplyChromeTheme(settings.Current.Appearance);

        var window = new MainWindow(_vm, settings);
        MainWindow = window;

        // Bandeja (só na janela comum): fechar a janela esconde; "Sair" no ícone encerra o Forge e os terminais.
        if (!args.Admin)
        {
            _tray = new TrayService();
            _tray.ShowWindowRequested += (_, _) => window.BringToFront();
            _tray.ExitRequested += (_, _) => ExitApp(window);
            _tray.Initialize();
            window.HiddenToTray += (_, _) => _tray.NotifyHidden();
        }

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
        var vm = new SettingsViewModel(settings, shells, history, notifier, paths) { DetectWsl = _vm is null ? null : _vm.DescribeAllDistrosAsync };
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

    private static void TrimHistoryQuietly(HistoryStore history, AuditLog audit)
    {
        try
        {
            history.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fica para a próxima abertura; a busca funciona com o arquivo como está.
            audit.Warning("histórico", "Não foi possível aparar o history.jsonl", ex.Message);
        }
    }

    /// <summary>Versão do Forge, do Windows e do .NET: a primeira coisa a saber ao ler um log de outra máquina.</summary>
    private static string RuntimeInfo()
    {
        var version = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
        return $"versão: {version}\nWindows: {Environment.OSVersion.Version} ({RuntimeInformation.OSArchitecture})\n.NET: {RuntimeInformation.FrameworkDescription}";
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Um erro inesperado vira aviso na janela em vez de derrubar o app (e todos os terminais com ele).
        if (_vm is not null)
            _vm.Notifier.Error(e.Exception);
        else
            _audit?.Error("erro", "Erro inesperado", e.Exception);
        e.Handled = true;
    }

    // Erro fora da thread da interface: o processo vai cair. Fica o registro do porquê.
    private void OnFatal(object sender, UnhandledExceptionEventArgs e) =>
        _audit?.Error("falha", "Erro fatal: o Forge vai fechar", e.ExceptionObject.ToString() ?? "");

    private void ExitApp(MainWindow window)
    {
        window.AllowClose = true;
        window.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _audit?.Info("app", "Forge encerrado");
        _tray?.Dispose();
        _sessions?.Dispose();
        // Grava o que estiver pendente: fechar o app é um dos dois momentos de salvar (o outro é 500 ms após mudar).
        _workspaces?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
