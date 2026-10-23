using System.Diagnostics;
using System.Text;
using Forge.Core.Diagnostics;
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

/// <summary>Com <paramref name="audit"/>, registra o que foi aberto e as falhas ao iniciar (programa não encontrado).</summary>
public sealed class ProcessRunner(AuditLog? audit = null) : IProcessRunner
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

        using var process = Start(psi, fileName, args);
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
        audit?.Info("processo", "Aberto: " + Display(fileName, args));
        Start(psi, fileName, args).Dispose();
    }

    private Process Start(ProcessStartInfo psi, string fileName, IReadOnlyList<string> args)
    {
        try
        {
            return Process.Start(psi) ?? throw new InvalidOperationException($"Não foi possível iniciar {fileName}.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            audit?.Warning("processo", "Não foi possível iniciar: " + Display(fileName, args), ex.Message);
            throw;
        }
    }

    public static string Display(string fileName, IReadOnlyList<string> args) =>
        string.Join(' ', args.Prepend(fileName).Select(CommandLine.Quote));
}
