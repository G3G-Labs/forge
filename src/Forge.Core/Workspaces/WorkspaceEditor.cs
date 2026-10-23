using Forge.Core.Models;
using Forge.Core.Util;

namespace Forge.Core.Workspaces;

/// <summary>
/// Operações sobre a árvore de grupos e abas. Funções puras sobre o <see cref="WorkspaceFile"/>: a interface as
/// chama dentro de <c>WorkspaceStore.Update</c>, e os testes as chamam direto.
/// </summary>
public static class WorkspaceEditor
{
    public static (WorkspaceGroup Group, WorkspaceTab Tab)? FindTab(WorkspaceFile file, string tabId)
    {
        foreach (var group in file.Groups)
            foreach (var tab in group.Tabs)
                if (tab.Id == tabId)
                    return (group, tab);
        return null;
    }

    public static WorkspaceGroup? FindGroup(WorkspaceFile file, string groupId) =>
        file.Groups.FirstOrDefault(g => g.Id == groupId);

    /// <summary>Grupo novo: só organiza (nome e cor). Cada aba dele escolhe o próprio tipo e a própria pasta.</summary>
    public static WorkspaceGroup AddGroup(WorkspaceFile file, string name, string? color = null)
    {
        var group = new WorkspaceGroup
        {
            Name = UniqueName(file.Groups.Select(g => g.Name), name),
            Color = color ?? Palette.NextGroupColor(file.Groups.Select(g => g.Color)),
        };
        file.Groups.Add(group);
        return group;
    }

    /// <summary>Aba nova no grupo, do tipo escolhido (PowerShell, CMD, Git Bash, WSL…). O nome vem do tipo ("pwsh", "ubuntu"…).</summary>
    public static WorkspaceTab AddTab(WorkspaceGroup group, ShellProfile shell, string? name = null, string? cwd = null)
    {
        var baseName = name ?? DefaultTabName(shell);
        var tab = new WorkspaceTab
        {
            Name = UniqueName(group.Tabs.Select(t => t.Name), baseName),
            Shell = shell.Id,
            Cwd = cwd,
        };
        group.Tabs.Add(tab);
        return tab;
    }

    public static WorkspaceTab? Duplicate(WorkspaceFile file, string tabId)
    {
        if (FindTab(file, tabId) is not (var group, var tab))
            return null;
        var copy = tab.CloneWithNewId();
        copy.Name = UniqueName(group.Tabs.Select(t => t.Name), tab.Name);
        group.Tabs.Insert(group.Tabs.IndexOf(tab) + 1, copy);
        return copy;
    }

    /// <summary>Move a aba para outro grupo (ou outra posição no mesmo). O índice é limitado ao tamanho do destino.</summary>
    public static bool MoveTab(WorkspaceFile file, string tabId, string targetGroupId, int index)
    {
        if (FindTab(file, tabId) is not (var source, var tab) || FindGroup(file, targetGroupId) is not { } target)
            return false;
        source.Tabs.Remove(tab);
        target.Tabs.Insert(Math.Clamp(index, 0, target.Tabs.Count), tab);
        return true;
    }

    public static bool MoveGroup(WorkspaceFile file, string groupId, int index)
    {
        if (FindGroup(file, groupId) is not { } group)
            return false;
        file.Groups.Remove(group);
        file.Groups.Insert(Math.Clamp(index, 0, file.Groups.Count), group);
        return true;
    }

    public static bool RemoveTab(WorkspaceFile file, string tabId)
    {
        if (FindTab(file, tabId) is not (var group, var tab))
            return false;
        group.Tabs.Remove(tab);
        if (file.SelectedTabId == tabId)
            file.SelectedTabId = null;
        return true;
    }

    public static bool RemoveGroup(WorkspaceFile file, string groupId)
    {
        if (FindGroup(file, groupId) is not { } group)
            return false;
        if (group.Tabs.Any(t => t.Id == file.SelectedTabId))
            file.SelectedTabId = null;
        return file.Groups.Remove(group);
    }

    /// <summary>O shell que a aba usa: o dela, senão o padrão das configurações.</summary>
    public static string ShellOf(WorkspaceTab tab, string defaultShell) => tab.Shell ?? defaultShell;

    /// <summary>Todas as abas na ordem da árvore (Ctrl+Tab, Ctrl+1…9).</summary>
    public static IEnumerable<WorkspaceTab> AllTabs(WorkspaceFile file) => file.Groups.SelectMany(g => g.Tabs);

    internal static string DefaultTabName(ShellProfile shell) => shell.Kind switch
    {
        ShellKind.Wsl => (shell.Distro ?? "wsl").ToLowerInvariant(),
        ShellKind.GitBash => "bash",
        ShellKind.Cmd => "cmd",
        ShellKind.WindowsPowerShell => "powershell",
        ShellKind.PowerShell => "pwsh",
        _ => shell.Name.ToLowerInvariant(),
    };

    /// <summary>"api", "api 2", "api 3"… sem repetir um nome já usado no mesmo nível.</summary>
    internal static string UniqueName(IEnumerable<string> existing, string name)
    {
        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name))
            return name;
        for (var n = 2; ; n++)
        {
            var candidate = $"{name} {n}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }
}
