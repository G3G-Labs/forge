using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;

namespace Forge.App.Services;

/// <summary>
/// Uma janela comum e uma de administrador por usuário. Abrir de novo só traz a existente para a frente e repassa
/// os argumentos (ex.: "--add-tab" vindo de "Abrir como administrador") por um pipe nomeado.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();

    public SingleInstance(string role, Action<string[]> onForwarded)
    {
        var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var id = $"Forge-{user}-{role}";
        _pipeName = id;
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + id, out var created);
        IsFirst = created;
        if (created)
            _ = ListenAsync(onForwarded, _cts.Token);
    }

    public bool IsFirst { get; }

    public void Forward(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(2000);
            using var writer = new StreamWriter(client);
            writer.Write(JsonSerializer.Serialize(args));
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // A outra instância não respondeu: esta só fecha.
        }
    }

    private async Task ListenAsync(Action<string[]> onForwarded, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(ct);
                using var reader = new StreamReader(server);
                var text = await reader.ReadToEndAsync(ct);
                onForwarded(JsonSerializer.Deserialize<string[]>(text) ?? []);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Mensagem quebrada: espera a próxima.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsFirst)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
        _cts.Dispose();
    }
}
