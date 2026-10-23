using Forge.Core.Diagnostics;
using Forge.Core.Models;
using Forge.Core.Persistence;
using Forge.Core.Shells;
using Forge.Terminal;

namespace Forge.App.Services;

/// <summary>
/// Os shells abertos, um por aba iniciada. Cria a sessão sob demanda (aba não iniciada não tem processo nem
/// controle), avisa a view para colocar o controle na tela e aplica tema e fonte quando as preferências mudam.
/// </summary>
public sealed class SessionManager : IDisposable
{
    private readonly LaunchBuilder _launch;
    private readonly SettingsStore _settings;
    private readonly AuditLog _audit;
    private readonly Dictionary<string, TerminalSession> _sessions = [];

    public SessionManager(LaunchBuilder launch, SettingsStore settings, AuditLog audit)
    {
        _launch = launch;
        _settings = settings;
        _audit = audit;
        settings.Changed += (_, _) => ApplyAppearance();
    }

    public IReadOnlyCollection<TerminalSession> All => _sessions.Values;

    public event EventHandler<TerminalSession>? Added;

    public event EventHandler<TerminalSession>? Removed;

    public TerminalSession Start(WorkspaceTab tab, ShellProfile shell)
    {
        if (_sessions.TryGetValue(tab.Id, out var existing))
            return existing;

        var plan = _launch.Build(tab, shell, _settings.Current, Environment.UserName, Environment.MachineName);
        var appearance = _settings.Current.Appearance;
        // Os valores das variáveis ficam de fora do log: podem ser senhas e tokens.
        var detail = $"shell: {shell.Name} ({shell.Id})\ncomando: {SecretMask.Apply(plan.CommandLine)}\npasta: {tab.LastCwd ?? tab.Cwd ?? "(padrão)"}" +
                     (tab.Env.Count > 0 ? "\nvariáveis: " + string.Join(", ", tab.Env.Keys) : "") +
                     (plan.Integration ? "" : "\nsem integração de shell");
        TerminalSession session;
        try
        {
            session = new TerminalSession(tab.Id, plan, TerminalThemes.From(appearance), appearance.FontFamily, appearance.FontSize);
        }
        catch (Exception ex)
        {
            _audit.Error("aba", $"Não foi possível abrir o shell da aba \"{tab.Name}\"", detail + "\n" + ex);
            throw;
        }
        _audit.Info("aba", $"Shell aberto na aba \"{tab.Name}\"", detail);
        _sessions[tab.Id] = session;
        Added?.Invoke(this, session);
        return session;
    }

    public TerminalSession? Find(string tabId) => _sessions.GetValueOrDefault(tabId);

    public void Close(string tabId)
    {
        if (!_sessions.Remove(tabId, out var session))
            return;
        Removed?.Invoke(this, session);
        session.Dispose();
    }

    private void ApplyAppearance()
    {
        var appearance = _settings.Current.Appearance;
        var theme = TerminalThemes.From(appearance);
        foreach (var session in _sessions.Values)
            session.ApplyTheme(theme, appearance.FontFamily, appearance.FontSize);
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
            session.Dispose();
        _sessions.Clear();
    }
}
