using Forge.App.Services;
using Forge.Core.Actions;
using Forge.Core.Git;
using Forge.Core.Persistence;
using Forge.Core.Shells;

namespace Forge.App.ViewModels;

/// <summary>Os serviços que grupos e abas usam, criados uma vez no App e passados juntos.</summary>
public sealed record WorkspaceContext(
    WorkspaceStore Store,
    SettingsStore Settings,
    ShellCatalog Shells,
    SessionManager Sessions,
    GitService Git,
    ExternalApps Apps,
    HistoryStore History,
    Notifier Notifier,
    bool IsAdmin);
