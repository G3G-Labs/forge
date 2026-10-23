using System.Globalization;
using System.Text;

namespace Forge.Core.Diagnostics;

public enum AuditLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Log de auditoria para investigar problemas: %APPDATA%\Forge\logs\forge-AAAA-MM-DD.log, um arquivo por dia, em
/// texto legível. Registra o que o app abriu e executou (shells, comandos, processos de apoio) e todo erro, com a
/// pilha. As duas janelas (comum e administrador) escrevem no mesmo arquivo; a coluna de origem separa as linhas.
/// Escrever no log nunca derruba o app: se o disco falhar, a linha se perde e pronto.
/// </summary>
public sealed class AuditLog(string folder, string source = "main", TimeProvider? time = null)
{
    public const int DefaultKeepDays = 14;

    private const string Prefix = "forge-";
    private const string Extension = ".log";

    private readonly Lock _gate = new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public string Folder { get; } = folder;

    public bool Enabled { get; set; } = true;

    public string CurrentFile => FileFor(_time.GetLocalNow());

    public void Info(string category, string message, string? detail = null) => Write(AuditLevel.Info, category, message, detail);

    public void Warning(string category, string message, string? detail = null) => Write(AuditLevel.Warning, category, message, detail);

    public void Error(string category, string message, string? detail = null) => Write(AuditLevel.Error, category, message, detail);

    public void Error(string category, string message, Exception ex) => Write(AuditLevel.Error, category, message, ex.ToString());

    /// <summary>
    /// Uma entrada: "2026-10-23 14:39:12.345  ERRO   main   git     mensagem", com o detalhe (comando, pilha)
    /// nas linhas de baixo, recuado.
    /// </summary>
    public void Write(AuditLevel level, string category, string message, string? detail = null)
    {
        if (!Enabled)
            return;
        var now = _time.GetLocalNow();
        var text = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append("  ")
            .Append(Label(level).PadRight(5)).Append("  ")
            .Append(source.PadRight(5)).Append("  ")
            .Append(category.PadRight(8)).Append("  ")
            .Append(OneLine(message))
            .Append('\n');
        if (!string.IsNullOrWhiteSpace(detail))
            foreach (var line in detail.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'))
                text.Append("    ").Append(line).Append('\n');

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Folder);
                // Append com compartilhamento: a outra janela pode estar escrevendo ao mesmo tempo.
                using var stream = new FileStream(FileFor(now), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.Write(Encoding.UTF8.GetBytes(text.ToString()));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem log desta vez: o app segue.
        }
    }

    /// <summary>Apaga os arquivos com mais de <paramref name="keepDays"/> dias. Devolve quantos apagou.</summary>
    public int Prune(int keepDays = DefaultKeepDays)
    {
        if (!Directory.Exists(Folder))
            return 0;
        var oldest = DateOnly.FromDateTime(_time.GetLocalNow().Date).AddDays(-keepDays + 1);
        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(Folder, Prefix + "*" + Extension))
        {
            var name = Path.GetFileNameWithoutExtension(file)[Prefix.Length..];
            if (!DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) || day >= oldest)
                continue;
            try
            {
                File.Delete(file);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Em uso: fica para a próxima abertura.
            }
        }
        return removed;
    }

    private string FileFor(DateTimeOffset day) =>
        Path.Combine(Folder, Prefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + Extension);

    private static string Label(AuditLevel level) => level switch
    {
        AuditLevel.Warning => "AVISO",
        AuditLevel.Error => "ERRO",
        _ => "INFO",
    };

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();
}
