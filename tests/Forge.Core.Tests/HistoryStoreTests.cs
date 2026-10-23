using Forge.Core.Models;
using Forge.Core.Persistence;

namespace Forge.Core.Tests;

public class HistoryStoreTests
{
    private static HistoryEntry Entry(string command, string cwd = "/home/dev/api", int minute = 0) =>
        new(new DateTimeOffset(2026, 10, 23, 9, minute, 0, TimeSpan.Zero), command, "t1", "api", "Cliente A", cwd, 0);

    [Fact]
    public void Busca_ignora_maiusculas_do_mais_recente_para_o_mais_antigo_sem_repetir()
    {
        using var temp = new TempFolder();
        var store = new HistoryStore(temp.File("history.jsonl"), 100);
        store.Append(Entry("docker compose up -d", minute: 1));
        store.Append(Entry("npm run dev", minute: 2));
        store.Append(Entry("Docker ps", minute: 3));
        store.Append(Entry("docker compose up -d", minute: 4));

        var results = store.Search("DOCKER");

        Assert.Equal(new[] { "docker compose up -d", "Docker ps" }, results.Select(r => r.Command));
        Assert.Equal(4, results[0].At.Minute);
    }

    [Fact]
    public void Filtra_pela_pasta()
    {
        using var temp = new TempFolder();
        var store = new HistoryStore(temp.File("history.jsonl"), 100);
        store.Append(Entry("ls", cwd: "/home/dev/api"));
        store.Append(Entry("ls -la", cwd: "/home/dev/web"));

        var results = store.Search("ls", cwd: "/home/dev/web");

        Assert.Equal("ls -la", Assert.Single(results).Command);
    }

    [Fact]
    public void Linha_truncada_e_pulada_e_o_resto_continua_valendo()
    {
        using var temp = new TempFolder();
        var path = temp.File("history.jsonl");
        new HistoryStore(path, 100).Append(Entry("git status"));
        File.AppendAllText(path, "{\"at\":\"2026-10-23T09:");

        var results = new HistoryStore(path, 100).Search("");

        Assert.Equal("git status", Assert.Single(results).Command);
    }

    [Fact]
    public void Aparar_mantem_so_as_linhas_mais_recentes()
    {
        using var temp = new TempFolder();
        var path = temp.File("history.jsonl");
        var store = new HistoryStore(path, limit: 3);
        for (var i = 0; i < 5; i++)
            store.Append(Entry($"cmd {i}", minute: i));

        store.Trim();

        Assert.Equal(3, File.ReadAllLines(path).Length);
        Assert.Equal(new[] { "cmd 4", "cmd 3", "cmd 2" }, new HistoryStore(path, 3).Search("").Select(r => r.Command));
    }
}
