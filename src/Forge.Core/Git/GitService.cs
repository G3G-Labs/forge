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
            var result = await RunGitAsync(folder, shell, ["status", "--porcelain=v2", "--branch"], ct).ConfigureAwait(false);
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

    /// <summary>As branches locais da pasta, em ordem alfabética. Vazio se não for um repositório.</summary>
    public async Task<IReadOnlyList<string>> BranchesAsync(string folder, ShellProfile shell, CancellationToken ct = default)
    {
        try
        {
            var result = await RunGitAsync(folder, shell, ["for-each-ref", "--format=%(refname:short)", "refs/heads"], ct).ConfigureAwait(false);
            if (result.ExitCode != 0)
                return [];
            return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return [];
        }
    }

    /// <summary>Troca de branch (git switch). Devolve null se deu certo, ou a mensagem do git.</summary>
    public async Task<string?> SwitchAsync(string folder, ShellProfile shell, string branch, CancellationToken ct = default)
    {
        try
        {
            var result = await RunGitAsync(folder, shell, ["switch", branch], ct).ConfigureAwait(false);
            Invalidate(folder);
            return result.ExitCode == 0 ? null : FirstLine(result.Error) ?? FirstLine(result.Output) ?? $"git switch saiu com código {result.ExitCode}.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "O git não foi encontrado.";
        }
    }

    // Em abas do ambiente WSL (shell do Linux ou terminal do Windows na distro), o git roda dentro da distro.
    private Task<ProcessResult> RunGitAsync(string folder, ShellProfile shell, IReadOnlyList<string> args, CancellationToken ct) =>
        shell.InWsl
            ? runner.RunAsync(shell.WslExecutable ?? shell.Executable, ["-d", shell.Distro!, "--cd", PathMapper.ToWsl(folder), "--", "git", .. args], ct: ct)
            : runner.RunAsync("git", ["-C", ExternalApps.WindowsPath(folder, shell), .. args], ct: ct);

    private static string? FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

    /// <summary>Esquece o cache de uma pasta (ex.: o usuário acabou de rodar um comando nela).</summary>
    public void Invalidate(string folder)
    {
        // A chave é "distro|pasta"; a distro não importa aqui.
        foreach (var key in _cache.Keys)
            if (key.EndsWith("|" + folder, StringComparison.Ordinal))
                _cache.TryRemove(key, out _);
    }
}
