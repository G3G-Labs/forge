using Forge.Core.Models;

namespace Forge.Core.Persistence;

/// <summary>
/// Dono do settings.json. Muda pouco, então grava na hora. A janela de administrador abre com
/// <see cref="IsReadOnly"/>: lê as mesmas preferências, mas só a janela principal grava.
/// </summary>
public sealed class SettingsStore(string path)
{
    private readonly JsonFile<AppSettings> _file = new(path, ForgeJson.Indented);

    public AppSettings Current { get; private set; } = new();

    public string? LoadProblem => _file.Problem;

    public bool IsReadOnly { get; init; }

    public event EventHandler? Changed;

    public void Load()
    {
        Current = _file.Load();
        Migrate(Current);
    }

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        if (!IsReadOnly)
            _file.Save(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Converte campos de versões anteriores. Nada a migrar ainda: é o lugar certo quando houver.</summary>
    internal static void Migrate(AppSettings settings)
    {
        if (settings.HistoryLimit <= 0)
            settings.HistoryLimit = 50_000;
        settings.Version = 1;
    }
}
