using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Actions;
using Forge.Core.Models;
using Forge.Core.ShellIntegration;
using Forge.Core.Shells;
using Forge.Core.Workspaces;
using Forge.Terminal;

namespace Forge.App.ViewModels;

/// <summary>
/// Uma aba: o que está salvo (<see cref="Model"/>) e o que está acontecendo agora (shell, estado, Git).
/// Sem sessão, a aba é só uma linha na árvore; o shell nasce em <see cref="EnsureStarted"/>.
/// </summary>
public sealed partial class TabViewModel : ObservableObject, IRenamable
{
    private readonly WorkspaceContext _ctx;
    private TabStatus _status = new();
    private TerminalSession? _session;
    private bool _startupPending;

    public TabViewModel(WorkspaceTab model, GroupViewModel group, WorkspaceContext ctx)
    {
        _ctx = ctx;
        Model = model;
        Group = group;
        Name = model.Name;
        Config = new TabConfigViewModel(this, ctx);
    }

    public WorkspaceTab Model { get; }

    public string Id => Model.Id;

    public TabConfigViewModel Config { get; }

    /// <summary>O grupo atual. Muda quando a aba é arrastada para outro grupo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color), nameof(ShellId), nameof(Shell), nameof(ShellName), nameof(Subtitle))]
    private GroupViewModel _group = null!;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private bool _isConfigOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsRunning))]
    private TabRunState _state = TabRunState.NotStarted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int? _exitCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Branch), nameof(GitSummary), nameof(ChangesText), nameof(HasGit))]
    private GitStatus? _git;

    /// <summary>Pasta atual: a informada pelo shell (OSC 7) ou, antes disso, a inicial.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Folder), nameof(Subtitle))]
    private string? _cwd;

    /// <summary>Id do shell em uso: o da aba ou, se ela segue o grupo, o ambiente do grupo.</summary>
    public string ShellId => WorkspaceEditor.ShellOf(Group.Model, Model, _ctx.Settings.Current.DefaultShell);

    public ShellProfile? Shell => _ctx.Shells.Find(ShellId);

    public string ShellName => Shell?.Name ?? ShellId;

    /// <summary>A aba segue o ambiente do grupo (não escolheu um shell próprio).</summary>
    public bool FollowsGroupShell => Model.Shell is null;

    /// <summary>Cor da aba; sem cor própria, a do grupo.</summary>
    public string Color => Model.Color ?? Group.Color;

    public bool HasStartup => !string.IsNullOrWhiteSpace(Model.Startup);

    public string Subtitle => HasStartup ? $"{ShellName} · ao abrir: {Model.Startup}" : $"{ShellName} · {Folder}";

    public bool IsRunning => State == TabRunState.Running;

    public bool IsElevated => _ctx.IsAdmin;

    public string StatusText => State switch
    {
        TabRunState.Running => "Rodando",
        TabRunState.Failed => $"Erro · código {ExitCode}",
        TabRunState.NotStarted => "Não iniciada",
        TabRunState.Exited => "Encerrada",
        _ => "Parada",
    };

    public string Folder => Cwd ?? LaunchBuilder.ResolveCwd(Model, Group.Folder, _ctx.Settings.Current.FolderOnRestore) ?? "~";

    public string? Branch => Git?.Branch;

    public bool HasGit => Git is not null;

    public string GitSummary => Git is { } g ? $"{g.Branch}  ↑{g.Ahead} ↓{g.Behind}" : "sem repositório Git";

    public string ChangesText => Git switch
    {
        null => "",
        { Changes: 0 } => "sem alterações",
        { Changes: 1 } => "● 1 alteração",
        var g => $"● {g.Changes} alterações",
    };

    partial void OnNameChanged(string value)
    {
        if (value != Model.Name)
            _ctx.Store.Update(_ => Model.Name = value);
    }

    partial void OnStateChanged(TabRunState value) => Group.Refresh();

    // Ao abrir o painel, ele relê o modelo: o grupo pode ter mudado de ambiente ou de pasta.
    partial void OnIsConfigOpenChanged(bool value)
    {
        if (value)
            Config.Load();
    }

    public bool IsStarted => _session is not null;

    /// <summary>
    /// Abre o shell, se ainda não abriu. Se a aba tem "comando ao abrir", ele é digitado assim que o shell mostrar o
    /// primeiro prompt. O Forge não inicia nem para projetos por conta própria: só roda o que o usuário escreveu.
    /// </summary>
    public void EnsureStarted()
    {
        if (_session is null)
        {
            if (Shell is not { } shell)
            {
                _ctx.Notifier.Show($"O shell \"{ShellId}\" não foi encontrado nesta máquina.", "Escolha outro em Configurar aba ou Configurar grupo.", isError: true);
                return;
            }
            _status = new TabStatus();
            _session = _ctx.Sessions.Start(Model, shell, Group.Folder);
            _session.ShellEventReceived += OnShellEvent;
            _status.ShellStarted();
            State = _status.State;
            Cwd = LaunchBuilder.ResolveCwd(Model, Group.Folder, _ctx.Settings.Current.FolderOnRestore);
            OnPropertyChanged(nameof(IsStarted));
            _ = RefreshGitAsync();
            if (HasStartup)
                RunStartupWhenReady();
        }
    }

    /// <summary>Encerra o shell da aba (fechar a aba, ou reabrir com shell/pasta/variáveis novas).</summary>
    public void CloseSession()
    {
        if (_session is null)
            return;
        _session.ShellEventReceived -= OnShellEvent;
        _ctx.Sessions.Close(Id);
        _session = null;
        _startupPending = false;
        State = TabRunState.NotStarted;
        OnPropertyChanged(nameof(IsStarted));
    }

    /// <summary>Fecha e reabre o shell da aba (vale o shell, a pasta e as variáveis atuais; o comando ao abrir roda de novo).</summary>
    [RelayCommand]
    public void RestartShell()
    {
        CloseSession();
        EnsureStarted();
    }

    /// <summary>Cola um comando do histórico (Ctrl+R) sem executar: o usuário confere e dá Enter.</summary>
    public void Paste(string command)
    {
        EnsureStarted();
        _session?.Send(command);
    }

    public async Task RefreshGitAsync(bool fresh = false)
    {
        if (Shell is not { } shell)
            return;
        try
        {
            Git = await _ctx.Git.QueryAsync(Folder, shell, fresh);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception)
        {
            Git = null;
        }
    }

    [RelayCommand]
    private void OpenInVsCode()
    {
        if (Shell is { } shell)
            _ctx.Apps.OpenInVsCode(Folder, shell);
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        if (Shell is { } shell)
            _ctx.Apps.OpenInExplorer(Folder, shell);
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (Shell is not { } shell)
            return;
        var path = ExternalApps.WindowsPath(Folder, shell);
        Clipboard.SetText(path);
        _ctx.Notifier.Show("Caminho copiado", path);
    }

    [RelayCommand]
    private void Duplicate() => Group.Owner.DuplicateTab(this);

    [RelayCommand]
    private void OpenAsAdmin() => Group.Owner.OpenAsAdmin(this);

    [RelayCommand]
    private void Configure() => IsConfigOpen = !IsConfigOpen;

    [RelayCommand]
    private void BeginRename() => IsRenaming = true;

    [RelayCommand]
    private void Close() => Group.Owner.CloseTab(this);

    /// <summary>Cor da aba no menu "…": null volta para a cor do grupo.</summary>
    [RelayCommand]
    private void SetColor(string? color)
    {
        _ctx.Store.Update(_ => Model.Color = color);
        OnPropertyChanged(nameof(Color));
    }

    /// <summary>O grupo mudou de cor: a aba sem cor própria acompanha.</summary>
    public void GroupColorChanged() => OnPropertyChanged(nameof(Color));

    /// <summary>Configurar aba alterou o comando ou o shell: atualiza o que a tela mostra.</summary>
    public void ModelChanged()
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ShellName));
        OnPropertyChanged(nameof(HasStartup));
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(FollowsGroupShell));
    }

    private void RunStartupWhenReady()
    {
        if (_session is null || !HasStartup)
            return;
        // Com integração, espera o primeiro prompt (o script já entrou na pasta). Sem ela, a sessão guarda o texto
        // até o processo responder.
        if (!_session.Plan.Integration || _status.IsReady)
            SendStartup();
        else
            _startupPending = true;
    }

    private void SendStartup()
    {
        _startupPending = false;
        _session?.Run(Model.Startup!);
        _status.CommandSent();
        State = _status.State;
    }

    // Chega na thread de leitura do ConPTY.
    private void OnShellEvent(object? sender, ShellEvent e) =>
        Application.Current.Dispatcher.BeginInvoke(() => Apply(e));

    private void Apply(ShellEvent e)
    {
        if (_session is null)
            return;
        var finished = _status.Apply(e);
        State = _status.State;
        ExitCode = _status.LastExitCode;

        switch (e)
        {
            case PromptStarted when _startupPending:
                SendStartup();
                break;
            case CwdChanged:
                Cwd = _status.Cwd;
                if (_ctx.Settings.Current.FolderOnRestore == FolderOnRestore.LastVisited)
                    _ctx.Store.Update(_ => Model.LastCwd = Cwd);
                break;
        }

        if (finished is not null)
        {
            _ctx.History.Append(new HistoryEntry(DateTimeOffset.Now, finished.Command, Id, Name, Group.Name, finished.Cwd ?? Cwd, finished.ExitCode));
            // Um checkout ou commit acabou de acontecer? A branch na barra lateral muda sozinha.
            _ = RefreshGitAsync(fresh: true);
        }
    }
}
