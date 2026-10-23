using Forge.Core.Models;

namespace Forge.Core.Workspaces;

/// <summary>Uma aba a abrir na restauração e se o "comando ao abrir" dela roda junto.</summary>
public sealed record StartStep(WorkspaceTab Tab, bool RunStartup);

/// <summary>
/// Quem ganha shell ao abrir o app. Todas as abas aparecem na hora, mas só abrem a selecionada e as marcadas para
/// abrir junto com o app; as outras abrem quando o usuário clicar nelas. Assim abrir dezenas de abas continua rápido e
/// não vira uma rajada de processos. O "comando ao abrir" roda sempre que o shell da aba abre.
/// </summary>
public static class RestorePlanner
{
    public static IReadOnlyList<StartStep> Plan(WorkspaceFile file)
    {
        var steps = new List<StartStep>();
        var tabs = WorkspaceEditor.AllTabs(file).ToList();
        var selected = tabs.FirstOrDefault(t => t.Id == file.SelectedTabId) ?? tabs.FirstOrDefault();

        // A visível primeiro: é a que o usuário está olhando.
        if (selected is not null)
            steps.Add(new StartStep(selected, HasStartup(selected)));

        foreach (var tab in tabs)
            if (tab != selected && tab.Autostart)
                steps.Add(new StartStep(tab, HasStartup(tab)));

        return steps;
    }

    private static bool HasStartup(WorkspaceTab tab) => !string.IsNullOrWhiteSpace(tab.Startup);
}
