using System.Diagnostics;
using System.Text;
using Forge.Core.Util;

namespace Forge.Core.Processes;

public sealed record ProcessResult(int ExitCode, string Output, string Error);

/// <summary>Processos de apoio (git, wsl.exe -l, VS Code). Os shells das abas não passam por aqui: vão pelo ConPTY.</summary>
public interface IProcessRunner
{
    /// <summary>Roda e espera, capturando a saída. Cancelar encerra o processo.</summary>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> args, Encoding? encoding = null, CancellationToken ct = default);

    /// <summary>Inicia e não espera (VS Code, Explorer). <paramref name="hidden"/> evita piscar uma janela de console.</summary>
    void Launch(string fileName, IReadOnlyList<string> args, bool hidden = false);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> args, Encoding? encoding = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding ?? Encoding.UTF8,
            StandardErrorEncoding = encoding ?? Encoding.UTF8,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Não foi possível iniciar {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { /* já terminou */ }
            throw;
        }
        return new ProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    public void Launch(string fileName, IReadOnlyList<string> args, bool hidden = false)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = !hidden,
            CreateNoWindow = hidden,
            Arguments = string.Join(' ', args.Select(CommandLine.Quote)),
        };
        Process.Start(psi)?.Dispose();
    }
}
