using Forge.Core.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace Forge.Core.Tests;

public class AuditLogTests
{
    private static FakeTimeProvider Clock(int day = 23) =>
        new(new DateTimeOffset(2026, 10, day, 14, 39, 12, 345, TimeSpan.Zero));

    [Fact]
    public void Grava_uma_linha_por_entrada_no_arquivo_do_dia_com_o_detalhe_recuado()
    {
        using var temp = new TempFolder();
        var log = new AuditLog(temp.Path, "main", Clock());

        log.Info("comando", "npm run dev");
        log.Warning("comando", "npm test", "pasta: C:\\dev\\api\r\ncódigo de saída: 1");

        Assert.Equal(temp.File("forge-2026-10-23.log"), log.CurrentFile);
        var lines = File.ReadAllLines(log.CurrentFile);
        Assert.Equal(
            [
                "2026-10-23 14:39:12.345  INFO   main   comando   npm run dev",
                "2026-10-23 14:39:12.345  AVISO  main   comando   npm test",
                "    pasta: C:\\dev\\api",
                "    código de saída: 1",
            ],
            lines);
    }

    [Fact]
    public void Erro_leva_a_pilha_e_a_mensagem_fica_numa_linha_so()
    {
        using var temp = new TempFolder();
        var log = new AuditLog(temp.Path, "admin", Clock());
        Exception error;
        try
        {
            throw new IOException("disco cheio");
        }
        catch (IOException ex)
        {
            error = ex;
        }

        log.Error("erro", "Não foi possível\nsalvar", error);

        var lines = File.ReadAllLines(log.CurrentFile);
        Assert.Equal("2026-10-23 14:39:12.345  ERRO   admin  erro      Não foi possível salvar", lines[0]);
        Assert.Equal("    System.IO.IOException: disco cheio", lines[1]);
        Assert.Contains(lines, l => l.StartsWith("       at ", StringComparison.Ordinal));
    }

    [Fact]
    public void Desligado_nao_grava_nada()
    {
        using var temp = new TempFolder();
        var log = new AuditLog(temp.Path, time: Clock()) { Enabled = false };

        log.Error("erro", "falhou");

        Assert.False(File.Exists(log.CurrentFile));
    }

    [Fact]
    public void Muda_de_arquivo_quando_vira_o_dia()
    {
        using var temp = new TempFolder();
        var clock = Clock();
        var log = new AuditLog(temp.Path, time: clock);

        log.Info("app", "um");
        clock.Advance(TimeSpan.FromHours(10));
        log.Info("app", "dois");

        Assert.Single(File.ReadAllLines(temp.File("forge-2026-10-23.log")));
        Assert.Single(File.ReadAllLines(temp.File("forge-2026-10-24.log")));
    }

    [Fact]
    public void Prune_apaga_so_os_dias_antigos_do_log()
    {
        using var temp = new TempFolder();
        foreach (var name in new[] { "forge-2026-10-09.log", "forge-2026-10-10.log", "forge-2026-10-23.log", "outro.log", "forge-x.log" })
            File.WriteAllText(temp.File(name), "");
        var log = new AuditLog(temp.Path, time: Clock());

        var removed = log.Prune(keepDays: 14);

        Assert.Equal(1, removed);
        Assert.Equal(
            ["forge-2026-10-10.log", "forge-2026-10-23.log", "forge-x.log", "outro.log"],
            Directory.GetFiles(temp.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }
}
