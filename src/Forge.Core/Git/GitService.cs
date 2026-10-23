using System.Collections.Concurrent;
using Forge.Core.Actions;
using Forge.Core.Models;
using Forge.Core.Processes;
using Forge.Core.Shells;

namespace Forge.Core.Git;

/// <summary>
/// Branch, alterações e commits à frente/atrás da pasta de uma aba. Sem biblioteca Git: roda o git instalado.
/// Em abas WSL, roda dentro da distro (ler \\wsl.localhost pelo Windows é lento). Resultados ficam em cache por
/// alguns segundos, porque a barra lateral pergunta por várias abas da mesma pasta.
/// </summary>
public sealed class GitService(IProcessRunner runner, TimeProvider? time = null)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(3);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, GitStatus? Status)> _cache = new();

    /// <summary>Status da pasta, ou null se não for um repositório (ou o git não existir).</summary>
    public async Task<GitStatus?> QueryAsync(string folder, ShellProfile shell, bool fresh = false, CancellationToken ct = default)
    {
        var key = (shell.Distro ?? "") + "|" + folder;
        if (!fresh && _cache.TryGetValue(key, out var hit) && _time.GetUtcNow() - hit.At < CacheFor)
            return hit.Status;

        GitStatus? status;
        try
        {
            var result = shell.IsWsl
                ? await runner.RunAsync(shell.Executable, ["-d", shell.Distro!, "--cd", PathMapper.ToWsl(folder), "--", "git", "status", "--porcelain=v2", "--branch"], ct: ct).ConfigureAwait(false)
                : await runner.RunAsync("git", ["-C", ExternalApps.WindowsPath(folder, shell), "status", "--porcelain=v2", "--branch"], ct: ct).ConfigureAwait(false);
            status = result.ExitCode == 0 ? GitStatusParser.Parse(result.Output) : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // git não instalado.
            status = null;
        }

        _cache[key] = (_time.GetUtcNow(), status);
        return status;
    }

    /// <summary>Esquece o cache de uma pasta (ex.: o usuário acabou de rodar um comando nela).</summary>
    public void Invalidate(string folder)
    {
        // A chave é "distro|pasta"; a distro não importa aqui.
        foreach (var key in _cache.Keys)
            if (key.EndsWith("|" + folder, StringComparison.Ordinal))
                _cache.TryRemove(key, out _);
    }
}
