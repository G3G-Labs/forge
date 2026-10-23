using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Actions;
using Forge.Core.Diagnostics;
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

    /// <summary>Iniciais para a barra recolhida: "Front-end" → "FE", "api" → "AP".</summary>
    public string Initials
    {
        get
        {
            var words = Name.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
            var text = words.Length >= 2 ? $"{words[0][0]}{words[1][0]}" : Name.Length >= 2 ? Name[..2] : Name;
            return text.ToUpperInvariant();
        }
    }

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
    [NotifyPropertyChangedFor(nameof(Branch), nameof(GitSummary), nameof(ChangesText), nameof(HasGit), nameof(AheadBehind))]
    private GitStatus? _git;

    /// <summary>Pasta atual: a informada pelo shell (OSC 7) ou, antes disso, a inicial.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Folder), nameof(Subtitle), nameof(IsFolderPinned))]
    private string? _cwd;

    /// <summary>Id do shell em uso: o da aba ou, se ela segue o grupo, o ambiente do grupo.</summary>
    public string ShellId => WorkspaceEditor.ShellOf(Model, _ctx.Settings.Current.DefaultShell);

    public ShellProfile? Shell => _ctx.Shells.Find(ShellId);

    public string ShellName => Shell?.Name ?? ShellId;


    /// <summary>Cor da aba; sem cor própria, a do grupo.</summary>
    public string Color => Model.Color ?? Group.Color;

    public bool HasStartup => !string.IsNullOrWhiteSpace(Model.Startup);

    public string Subtitle => HasStartup ? $"{ShellName} · ao abrir: {Model.Startup}" : $"{ShellName} · {Folder}";

    public bool IsRunning => State == TabRunState.Running;

    public bool IsElevated => _ctx.IsAdmin;

    public string StatusText => State switch
    {
        TabRunState.Running => "Rodando",
        TabRunState.Failed => $"Último comando: código {ExitCode}",
        TabRunState.NotStarted => "Não iniciada",
        TabRunState.Exited => "Encerrada",
        _ => "Parada",
    };

    /// <summary>Pasta atual da aba (a que o shell informou), ou onde ela vai abrir.</summary>
    public string Folder => Cwd ?? LaunchBuilder.ResolveCwd(Model, _ctx.Settings.Current.FolderOnRestore) ?? "~";

    /// <summary>A pasta atual já é a pasta fixa da aba.</summary>
    public bool IsFolderPinned => Model.Cwd is not null && Model.Cwd == Cwd;

    public string? Branch => Git?.Branch;

    public bool HasGit => Git is not null;

    /// <summary>"↑2 ↓1": commits à frente e atrás do remoto.</summary>
    public string AheadBehind => Git is { } g ? $"↑{g.Ahead} ↓{g.Behind}" : "";

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
        OnPropertyChanged(nameof(Initials));
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
            _session = _ctx.Sessions.Start(Model, shell);
            _session.ShellEventReceived += OnShellEvent;
            _status.ShellStarted();
            State = _status.State;
            Cwd = LaunchBuilder.ResolveCwd(Model, _ctx.Settings.Current.FolderOnRestore);
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
        _ctx.Audit.Info("aba", $"Shell encerrado na aba \"{Name}\"", $"grupo: {Group.Name}");
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
            _ctx.Audit.Warning("git", $"Não foi possível ler o status do git em {Folder}", ex.ToString());
            Git = null;
        }
    }

    /// <summary>A pasta atual vira a pasta fixa da aba ("Configurar aba" mostra e permite trocar).</summary>
    [RelayCommand]
    private void PinFolder()
    {
        if (Cwd is not { } folder)
            return;
        _ctx.Store.Update(_ => Model.Cwd = folder);
        OnPropertyChanged(nameof(IsFolderPinned));
        _ctx.Notifier.Show("Pasta fixada", folder);
    }

    /// <summary>Branches locais, para o menu da barra de status.</summary>
    public async Task<IReadOnlyList<string>> BranchesAsync()
    {
        if (Shell is not { } shell || Git is null)
            return [];
        return await _ctx.Git.BranchesAsync(Folder, shell);
    }

    /// <summary>Troca de branch pelo git, sem digitar nada no terminal (um servidor rodando nele continua rodando).</summary>
    [RelayCommand]
    private async Task SwitchBranchAsync(string branch)
    {
        if (Shell is not { } shell || branch == Git?.Branch)
            return;
        _ctx.Audit.Info("git", $"git switch {branch}", $"pasta: {Folder}");
        if (await _ctx.Git.SwitchAsync(Folder, shell, branch) is { } error)
            _ctx.Notifier.Show($"Não foi possível trocar para {branch}", error, isError: true);
        await RefreshGitAsync(fresh: true);
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
        OnPropertyChanged(nameof(IsFolderPinned));
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
        _ctx.Audit.Info("comando", $"Comando ao abrir na aba \"{Name}\": {Model.Startup}", $"grupo: {Group.Name}\npasta: {Cwd}");
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
            case CwdChanged when _status.Cwd != Model.LastCwd:
                // Guarda a pasta onde a aba está: ao reabrir o app, ela volta aqui (como no Warp).
                Cwd = _status.Cwd;
                _ctx.Store.Update(_ => Model.LastCwd = Cwd);
                break;
            case CwdChanged:
                Cwd = _status.Cwd;
                break;
        }

        if (finished is not null)
        {
            // Comando com código diferente de zero sai como aviso: é o primeiro lugar a olhar quando algo deu errado.
            _ctx.Audit.Write(finished.ExitCode is null or 0 ? AuditLevel.Info : AuditLevel.Warning, "comando",
                finished.Command,
                $"aba: {Name} · grupo: {Group.Name}\npasta: {finished.Cwd ?? Cwd}\ncódigo de saída: {finished.ExitCode?.ToString() ?? "?"}");
            _ctx.History.Append(new HistoryEntry(DateTimeOffset.Now, finished.Command, Id, Name, Group.Name, finished.Cwd ?? Cwd, finished.ExitCode));
            // Um checkout ou commit acabou de acontecer? A branch na barra lateral muda sozinha.
            _ = RefreshGitAsync(fresh: true);
        }
    }
}
