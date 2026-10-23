using System.Text;
using System.Text.Json;
using Forge.Core.Models;

namespace Forge.Core.Persistence;

/// <summary>
/// O histórico unificado (Ctrl+R): history.jsonl, um comando por linha com data, aba e pasta.
/// As duas janelas (comum e administrador) acrescentam linhas; só a comum apara o arquivo ao abrir.
/// </summary>
public sealed class HistoryStore(string path, int limit)
{
    private readonly Lock _gate = new();

    // Carregado na primeira busca e mantido em dia pelos Append seguintes.
    private List<HistoryEntry>? _cache;

    public string FilePath { get; } = path;

    public int Limit { get; set; } = limit;

    public void Append(HistoryEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, ForgeJson.Line) + "\n";
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            // Append com compartilhamento: a outra janela pode estar escrevendo ao mesmo tempo, uma linha por vez.
            using (var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                stream.Write(Encoding.UTF8.GetBytes(line));
            _cache?.Add(entry);
        }
    }

    /// <summary>
    /// Comandos que contêm o texto (sem diferenciar maiúsculas), do mais recente para o mais antigo, sem repetir.
    /// Com <paramref name="cwd"/>, só os rodados naquela pasta.
    /// </summary>
    public IReadOnlyList<HistoryEntry> Search(string query, string? cwd = null, int max = 50)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<HistoryEntry>();
        lock (_gate)
        {
            var all = _cache ??= ReadAll();
            for (var i = all.Count - 1; i >= 0 && results.Count < max; i--)
            {
                var e = all[i];
                if (query.Length > 0 && !e.Command.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (cwd is not null && !string.Equals(e.Cwd, cwd, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (seen.Add(e.Command))
                    results.Add(e);
            }
        }
        return results;
    }

    /// <summary>Mantém só as <see cref="Limit"/> linhas mais recentes. Grava de forma atômica.</summary>
    public void Trim()
    {
        lock (_gate)
        {
            var all = ReadAll();
            if (all.Count <= Limit)
            {
                _cache = all;
                return;
            }
            var keep = all.GetRange(all.Count - Limit, Limit);
            var tmp = FilePath + ".tmp";
            File.WriteAllLines(tmp, keep.Select(e => JsonSerializer.Serialize(e, ForgeJson.Line)));
            File.Move(tmp, FilePath, overwrite: true);
            _cache = keep;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            _cache = [];
        }
    }

    /// <summary>Lê tudo, pulando linhas inválidas (a última pode ter ficado pela metade se o PC desligou).</summary>
    private List<HistoryEntry> ReadAll()
    {
        var list = new List<HistoryEntry>();
        if (!File.Exists(FilePath))
            return list;
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
                continue;
            try
            {
                if (JsonSerializer.Deserialize<HistoryEntry>(line, ForgeJson.Line) is { } entry)
                    list.Add(entry);
            }
            catch (JsonException)
            {
                // Linha truncada ou editada à mão: fica de fora da busca.
            }
        }
        return list;
    }
}
