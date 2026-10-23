using Forge.Core.Models;

namespace Forge.Core.Workspaces;

/// <summary>Uma aba a iniciar na restauração e se o comando de inicialização roda junto.</summary>
public sealed record StartStep(WorkspaceTab Tab, bool RunStartup);

/// <summary>
/// Quem ganha shell ao abrir o app. Todas as abas aparecem na hora, mas só iniciam a selecionada e as marcadas
/// para iniciar automaticamente; as outras iniciam quando o usuário clicar nelas. Assim abrir dezenas de abas
/// continua rápido e não vira uma rajada de processos.
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
            steps.Add(new StartStep(selected, RunStartup: selected.Autostart && HasStartup(selected)));

        foreach (var tab in tabs)
            if (tab != selected && tab.Autostart)
                steps.Add(new StartStep(tab, RunStartup: HasStartup(tab)));

        return steps;
    }

    private static bool HasStartup(WorkspaceTab tab) => !string.IsNullOrWhiteSpace(tab.Startup);
}
