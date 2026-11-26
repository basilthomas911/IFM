using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

/// <summary>One local ordered ITI consumer, before optional actor fan-out. Registered only by the fenced runtime.</summary>
public static class FuturesItiOrderedIngress
{
    static Func<FuturesMarketPriceUpdatedRealtimeEvent, CancellationToken, ValueTask>? consumer;
    static Func<FuturesTradeReplayBatchRealtimeEvent, CancellationToken, ValueTask>? replayConsumer;
    static Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask>? recoveryConsumer;
    static Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask>? recoveryReplayConsumer;
    static Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask>? prepareConsumer;
    static Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask>? prepareReplayConsumer;
    static Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask>? validateRecoveryConsumer;
    static Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask>? validateRecoveryReplayConsumer;
    static Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask<bool>>? recoveryProofConsumer;
    static Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask<bool>>? recoveryReplayProofConsumer;
    static Func<IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent>,CancellationToken,ValueTask>? prepareBatchConsumer,batchConsumer;
    static int enabled, healthy = 1;
    static Func<FuturesItiOrderedDrainStatus>? drainInspector;
    static string reason = string.Empty;
    public static bool HasTradeConsumer => Volatile.Read(ref consumer) is not null;
    public static bool HasReplayConsumer => Volatile.Read(ref replayConsumer) is not null;
    public static bool AllowsEntries => Volatile.Read(ref enabled) == 0 || Volatile.Read(ref healthy) != 0;
    public static string StatusReason => Volatile.Read(ref reason);
    /// <summary>Actual resident owner work, independent of market freshness or entry permission.</summary>
    public static FuturesItiOrderedDrainStatus? InspectDrain() => Volatile.Read(ref drainInspector)?.Invoke();
    public static void RegisterDrainInspector(Func<FuturesItiOrderedDrainStatus> value)
    {if(Interlocked.CompareExchange(ref drainInspector,value,null) is not null)throw new InvalidOperationException("ITI drain inspector already registered.");}
    public static void UnregisterDrainInspector(Func<FuturesItiOrderedDrainStatus> value)=>Interlocked.CompareExchange(ref drainInspector,null,value);
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
    /// <summary>Recovery accepts only existing original ordered-WAL/processed coverage; it never samples today's causal inputs.</summary>
    public static void RegisterRecovery(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> trade,Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> replay)
    {
        if(Interlocked.CompareExchange(ref recoveryConsumer,trade,null) is not null)throw new InvalidOperationException("ITI recovery consumer already registered.");
        if(Interlocked.CompareExchange(ref recoveryReplayConsumer,replay,null) is not null){Interlocked.CompareExchange(ref recoveryConsumer,null,trade);throw new InvalidOperationException("ITI replay recovery consumer already registered.");}
    }
    public static void UnregisterRecovery(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> trade,Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> replay)
    {Interlocked.CompareExchange(ref recoveryConsumer,null,trade);Interlocked.CompareExchange(ref recoveryReplayConsumer,null,replay);}
    public static ValueTask DeliverRecoveredAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token)
        =>Volatile.Read(ref recoveryConsumer) is {} value?value(source,token):ValueTask.FromException(new InvalidOperationException("Original ordered ITI recovery authority is unavailable."));
    public static ValueTask DeliverRecoveredReplayAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token)
        =>Volatile.Read(ref recoveryReplayConsumer) is {} value?value(source,token):ValueTask.FromException(new InvalidOperationException("Original ordered ITI replay authority is unavailable."));
    public static void RegisterPreparation(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> value)
    {if(Interlocked.CompareExchange(ref prepareConsumer,value,null) is not null)throw new InvalidOperationException("ITI source preparer already registered.");}
    public static void UnregisterPreparation(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> value)=>Interlocked.CompareExchange(ref prepareConsumer,null,value);
    public static ValueTask PrepareSourceAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token)
        =>Volatile.Read(ref prepareConsumer) is{} value?value(source,token):ValueTask.FromException(new InvalidOperationException("Original ITI causal input preparation is unavailable."));
    public static void RegisterReplayPreparation(Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> value)
    {if(Interlocked.CompareExchange(ref prepareReplayConsumer,value,null) is not null)throw new InvalidOperationException("ITI replay preparer already registered.");}
    public static void UnregisterReplayPreparation(Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> value)=>Interlocked.CompareExchange(ref prepareReplayConsumer,null,value);
    public static ValueTask PrepareSourceAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token)
        =>Volatile.Read(ref prepareReplayConsumer) is{} value?value(source,token):ValueTask.FromException(new InvalidOperationException("Original ITI replay preparation is unavailable."));
    public static void RegisterRecoveryValidation(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> trade,Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> replay)
    {if(Interlocked.CompareExchange(ref validateRecoveryConsumer,trade,null) is not null)throw new InvalidOperationException("ITI recovery validator already registered.");if(Interlocked.CompareExchange(ref validateRecoveryReplayConsumer,replay,null) is not null)throw new InvalidOperationException("ITI replay recovery validator already registered.");}
    public static void UnregisterRecoveryValidation(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> trade,Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> replay)
    {Interlocked.CompareExchange(ref validateRecoveryConsumer,null,trade);Interlocked.CompareExchange(ref validateRecoveryReplayConsumer,null,replay);}
    public static ValueTask ValidateRecoveredAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token)
        =>Volatile.Read(ref validateRecoveryConsumer) is{} value?value(source,token):ValueTask.FromException(new InvalidOperationException("Original ITI source recovery validator is unavailable."));
    public static ValueTask ValidateRecoveredAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token)
        =>Volatile.Read(ref validateRecoveryReplayConsumer) is{} value?value(source,token):ValueTask.FromException(new InvalidOperationException("Original ITI replay recovery validator is unavailable."));
    public static void RegisterRecoveryProof(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask<bool>> trade,
        Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask<bool>> replay)
    {
        if(Interlocked.CompareExchange(ref recoveryProofConsumer,trade,null) is not null)throw new InvalidOperationException("ITI original recovery proof is already registered.");
        if(Interlocked.CompareExchange(ref recoveryReplayProofConsumer,replay,null) is not null)
        {Interlocked.CompareExchange(ref recoveryProofConsumer,null,trade);throw new InvalidOperationException("ITI original replay proof is already registered.");}
    }
    public static void UnregisterRecoveryProof(Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask<bool>> trade,
        Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask<bool>> replay)
    {Interlocked.CompareExchange(ref recoveryProofConsumer,null,trade);Interlocked.CompareExchange(ref recoveryReplayProofConsumer,null,replay);}
    public static ValueTask<bool> ProveRecoveredAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token=default)
        =>Volatile.Read(ref recoveryProofConsumer) is{} value?value(source,token):ValueTask.FromResult(false);
    public static ValueTask<bool> ProveRecoveredAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token=default)
        =>Volatile.Read(ref recoveryReplayProofConsumer) is{} value?value(source,token):ValueTask.FromResult(false);
    public static void RegisterBatchPreparation(Func<IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent>,CancellationToken,ValueTask> value)
    {if(Interlocked.CompareExchange(ref prepareBatchConsumer,value,null) is not null)throw new InvalidOperationException("ITI source batch preparer already registered.");}
    public static void RegisterBatch(Func<IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent>,CancellationToken,ValueTask> value)
    {if(Interlocked.CompareExchange(ref batchConsumer,value,null) is not null)throw new InvalidOperationException("ITI source batch consumer already registered.");}
    public static void UnregisterBatchPreparation(Func<IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent>,CancellationToken,ValueTask> value)=>Interlocked.CompareExchange(ref prepareBatchConsumer,null,value);
    public static void UnregisterBatch(Func<IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent>,CancellationToken,ValueTask> value)=>Interlocked.CompareExchange(ref batchConsumer,null,value);
    public static ValueTask PrepareBatchAsync(IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent> sources,CancellationToken token)
        =>sources.Count==0?ValueTask.CompletedTask:Volatile.Read(ref prepareBatchConsumer) is{} value?value(sources,token):ValueTask.FromException(new InvalidOperationException("ITI source batch preparation is unavailable."));
    public static ValueTask DeliverBatchAsync(IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent> sources,CancellationToken token)
        =>sources.Count==0?ValueTask.CompletedTask:Volatile.Read(ref batchConsumer) is{} value?value(sources,token):ValueTask.FromException(new InvalidOperationException("ITI ordered batch release is unavailable."));
}

public sealed record FuturesItiOrderedDrainStatus(int Pending,int InFlightCaptures,int PreparedSources,int UnflushedSources,bool Failed,string Reason)
{
    public bool Drained=>Pending==0&&InFlightCaptures==0&&PreparedSources==0&&UnflushedSources==0&&!Failed;
}
