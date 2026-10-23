namespace Forge.Core.Models;

/// <summary>Uma linha do history.jsonl: o comando, onde e quando rodou.</summary>
public sealed record HistoryEntry(
    DateTimeOffset At,
    string Command,
    string TabId,
    string TabName,
    string Group,
    string? Cwd,
    int? ExitCode);

/// <summary>Branch e alterações de uma pasta, lidas de <c>git status --porcelain=v2 --branch</c>.</summary>
public sealed record GitStatus(
    string Branch,
    string? Upstream,
    int Ahead,
    int Behind,
    int Changes,
    bool Detached);
