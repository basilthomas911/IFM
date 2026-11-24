using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

/// <summary>One local ordered ITI consumer, before optional actor fan-out. Registered only by the fenced runtime.</summary>
public static class FuturesItiOrderedIngress
{
    static Func<FuturesMarketPriceUpdatedRealtimeEvent, CancellationToken, ValueTask>? consumer;
    static Func<FuturesTradeReplayBatchRealtimeEvent, CancellationToken, ValueTask>? replayConsumer;
    static int enabled, healthy = 1;
    static string reason = string.Empty;
    public static bool AllowsEntries => Volatile.Read(ref enabled) == 0 || Volatile.Read(ref healthy) != 0;
    public static string StatusReason => Volatile.Read(ref reason);
    public static void SetHealth(bool value, string detail)
    { Volatile.Write(ref reason, detail); Interlocked.Exchange(ref healthy, value ? 1 : 0); }
    public static void Register(Func<FuturesMarketPriceUpdatedRealtimeEvent, CancellationToken, ValueTask> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Interlocked.CompareExchange(ref consumer, value, null) is not null)
            throw new InvalidOperationException("ITI ordered consumer already registered.");
        Interlocked.Exchange(ref enabled, 1);
        SetHealth(false, "Ordered ITI source is awaiting its first current committed input.");
    }
    public static void Unregister(Func<FuturesMarketPriceUpdatedRealtimeEvent, CancellationToken, ValueTask> value)
        => Interlocked.CompareExchange(ref consumer, null, value);
    public static ValueTask DeliverAsync(FuturesMarketPriceUpdatedRealtimeEvent source, CancellationToken token)
        => Volatile.Read(ref consumer) is { } value ? value(source, token) : ValueTask.CompletedTask;
    public static void RegisterReplay(Func<FuturesTradeReplayBatchRealtimeEvent, CancellationToken, ValueTask> value)
    {
        if (Interlocked.CompareExchange(ref replayConsumer, value, null) is not null)
            throw new InvalidOperationException("ITI replay observer already registered.");
    }
    public static void UnregisterReplay(Func<FuturesTradeReplayBatchRealtimeEvent, CancellationToken, ValueTask> value)
        => Interlocked.CompareExchange(ref replayConsumer, null, value);
    public static ValueTask DeliverReplayAsync(FuturesTradeReplayBatchRealtimeEvent source, CancellationToken token)
        => Volatile.Read(ref replayConsumer) is { } value ? value(source, token) : ValueTask.CompletedTask;
}
