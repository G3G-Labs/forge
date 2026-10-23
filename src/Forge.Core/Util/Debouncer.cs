namespace Forge.Core.Util;

/// <summary>
/// Executa a ação uma vez, <paramref name="delay"/> depois do último <see cref="Trigger"/>. Junta mudanças em
/// sequência (renomear letra por letra vira uma gravação só). A ação roda numa thread do pool.
/// </summary>
public sealed class Debouncer(TimeSpan delay, Action action, TimeProvider? time = null) : IDisposable
{
    private readonly ITimer _timer = (time ?? TimeProvider.System)
        .CreateTimer(_ => action(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public void Trigger() => _timer.Change(delay, Timeout.InfiniteTimeSpan);

    public void Cancel() => _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public void Dispose() => _timer.Dispose();
}
