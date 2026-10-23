using Forge.Core.Models;
using Forge.Core.Util;
using Forge.Core.Workspaces;

namespace Forge.Core.Persistence;

/// <summary>
/// Dono do workspaces.json. Toda alteração passa por <see cref="Update"/> e é gravada 500 ms depois da última,
/// sem botão "salvar"; <see cref="Dispose"/> grava o que estiver pendente ao fechar o app.
/// </summary>
public sealed class WorkspaceStore : IDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(500);

    private readonly JsonFile<WorkspaceFile> _file;
    private readonly Debouncer _debouncer;
    private readonly Lock _gate = new();
    private bool _dirty;

    public WorkspaceStore(string path, TimeProvider? time = null, TimeSpan? delay = null)
    {
        _file = new JsonFile<WorkspaceFile>(path, ForgeJson.Indented);
        _debouncer = new Debouncer(delay ?? DefaultDelay, Flush, time);
    }

    public WorkspaceFile Current { get; private set; } = new();

    public string? LoadProblem => _file.Problem;

    /// <summary>Uma gravação falhou (disco cheio, sem permissão). Disparado fora da thread da interface.</summary>
    public event EventHandler<Exception>? SaveFailed;

    public void Load()
    {
        lock (_gate)
        {
            Current = _file.Load();
            Normalize(Current);
            _dirty = false;
        }
    }

    /// <summary>Altera o modelo e agenda a gravação. Use sempre este caminho: ele protege contra a gravação em andamento.</summary>
    public void Update(Action<WorkspaceFile> change)
    {
        lock (_gate)
        {
            change(Current);
            _dirty = true;
        }
        _debouncer.Trigger();
    }

    public void Flush()
    {
        lock (_gate)
        {
            if (!_dirty)
                return;
            try
            {
                _file.Save(Current);
                _dirty = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SaveFailed?.Invoke(this, ex);
            }
        }
    }

    public void Dispose()
    {
        _debouncer.Dispose();
        Flush();
    }

    /// <summary>Corrige o que um arquivo editado à mão pode trazer: ids repetidos ou vazios, nomes em branco.</summary>
    internal static void Normalize(WorkspaceFile file)
    {
        file.Version = WorkspaceFile.CurrentVersion;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in file.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.Id) || !seen.Add(group.Id))
                group.Id = Ids.New("g");
            if (string.IsNullOrWhiteSpace(group.Name))
                group.Name = "Sem nome";
            foreach (var tab in group.Tabs)
            {
                if (string.IsNullOrWhiteSpace(tab.Id) || !seen.Add(tab.Id))
                    tab.Id = Ids.New("t");
                if (string.IsNullOrWhiteSpace(tab.Name))
                    tab.Name = "aba";
            }
        }
        if (file.SelectedTabId is not null && WorkspaceEditor.FindTab(file, file.SelectedTabId) is null)
            file.SelectedTabId = null;
    }
}
