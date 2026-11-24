namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;

/// <summary>Process-local binding to the single database-fenced ITI owner; no transport fallback masquerades as fast intake.</summary>
public static class FuturesItiLocalCommandEndpoint
{
    static FuturesItiSignalCommandActor? current;
    static Func<ValueTask>? stopIngress;
    public static FuturesItiSignalCommandActor? Current => Volatile.Read(ref current);
    internal static void Register(FuturesItiSignalCommandActor actor)
    {
        if (Interlocked.CompareExchange(ref current, actor, null) is not null)
            throw new InvalidOperationException("An ITI local command owner is already registered.");
    }
    internal static void Unregister(FuturesItiSignalCommandActor actor) => Interlocked.CompareExchange(ref current, null, actor);
    internal static void RegisterIngress(Func<ValueTask> stop) => Interlocked.Exchange(ref stopIngress, stop);
    internal static void UnregisterIngress(Func<ValueTask> stop) => Interlocked.CompareExchange(ref stopIngress, null, stop);
    internal static ValueTask StopIngressAsync(FuturesItiSignalCommandActor actor)
        => ReferenceEquals(Current, actor) && Volatile.Read(ref stopIngress) is { } stop ? stop() : ValueTask.CompletedTask;
}
