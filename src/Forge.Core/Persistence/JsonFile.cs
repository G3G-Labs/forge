using System.Text;
using System.Text.Json;

namespace Forge.Core.Persistence;

/// <summary>
/// Um arquivo JSON gravado de forma atômica: escreve num .tmp, força o disco e troca pelo original guardando o
/// anterior em .bak. Desligar o PC no meio de uma gravação deixa o arquivo antigo ou o novo, nunca um pedaço.
/// </summary>
public sealed class JsonFile<T>(string filePath, JsonSerializerOptions options, TimeSpan? retryDelay = null)
    where T : class, new()
{
    private const int ReadAttempts = 5;

    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(200);

    // O arquivo existe mas estava bloqueado na abertura: antes de gravar por cima, guarda o original ao lado.
    private bool _unreadable;

    public string FilePath { get; } = filePath;

    public string BackupPath => FilePath + ".bak";

    /// <summary>Aviso para o usuário quando a última leitura não usou o arquivo como estava (ou null).</summary>
    public string? Problem { get; private set; }

    public T Load()
    {
        Problem = null;
        _unreadable = false;
        if (!File.Exists(FilePath))
            return TryLoad(BackupPath) ?? new T();

        if (!TryRead(FilePath, out var json))
        {
            // Bloqueado por outro programa (antivírus, sincronização…): o arquivo está bom e não pode ser perdido.
            _unreadable = true;
            Problem = $"Não foi possível ler {Path.GetFileName(FilePath)} agora. O app abriu sem ele e não vai apagá-lo.";
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, options) ?? new T();
        }
        catch (JsonException)
        {
            // Corrompido: guarda uma cópia para análise e volta para a última versão boa, se houver.
            CopyQuietly(FilePath, FilePath + ".corrompido");
            var backup = TryLoad(BackupPath);
            Problem = backup is null
                ? $"{Path.GetFileName(FilePath)} estava corrompido e foi recriado. O original ficou como .corrompido."
                : $"{Path.GetFileName(FilePath)} estava corrompido. O app voltou para a última versão boa.";
            return backup ?? new T();
        }
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        if (_unreadable)
        {
            if (File.Exists(FilePath))
                File.Copy(FilePath, FilePath + ".ilegivel", overwrite: true);
            _unreadable = false;
        }

        var tmp = FilePath + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(JsonSerializer.Serialize(value, options));
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(FilePath))
            File.Replace(tmp, FilePath, BackupPath, ignoreMetadataErrors: true);
        else
            File.Move(tmp, FilePath);
    }

    private T? TryLoad(string path)
    {
        if (!File.Exists(path) || !TryRead(path, out var json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Lê o arquivo, tentando de novo por cerca de um segundo se ele estiver bloqueado.</summary>
    private bool TryRead(string path, out string json)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                json = File.ReadAllText(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == ReadAttempts)
                {
                    json = "";
                    return false;
                }
                Thread.Sleep(_retryDelay);
            }
        }
    }

    private static void CopyQuietly(string from, string to)
    {
        try
        {
            File.Copy(from, to, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem cópia: o aviso ao usuário continua valendo.
        }
    }
}
