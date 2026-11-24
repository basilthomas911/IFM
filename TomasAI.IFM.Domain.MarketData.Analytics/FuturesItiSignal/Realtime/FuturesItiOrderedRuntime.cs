using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Bounded lossless local intake. Journal commits precede decision batches; failures stop intake for recovery.</summary>
public sealed class FuturesItiOrderedRuntime : IAsyncDisposable
{
    readonly IFuturesItiSignalRealtimeContext context;
    readonly Channel<Queued> channel = Channel.CreateBounded<Queued>(
        new BoundedChannelOptions(4096) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
    readonly FuturesAtrHotCacheReader atr = new();
    readonly CancellationTokenSource lifetime = new();
    readonly SemaphoreSlim captureGate = new(1, 1);
    readonly Func<FuturesMarketPriceUpdatedRealtimeEvent, CancellationToken, ValueTask> callback;
    readonly Func<FuturesTradeReplayBatchRealtimeEvent, CancellationToken, ValueTask> replayCallback;
    readonly Dictionary<string, (Guid Recovery, long Batch, Guid Epoch)> replay = new(StringComparer.Ordinal);
    readonly HashSet<Guid> verifiedEpochs = [];
    readonly HashSet<Guid> recent = [];
    readonly Queue<Guid> recentOrder = new();
    TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly object pendingGate = new();
    readonly Task worker;
    readonly Func<ValueTask> stopCallback;
    readonly object stopGate = new();
    Task? stopping;
    int stoppingFlag, captures;
    readonly FuturesItiInputWal wal;
    long accepted, processed, batches, lastMarketTicks;
    int pending, failed;
    string failure = string.Empty;

    FuturesItiOrderedRuntime(IFuturesItiSignalRealtimeContext context, FuturesItiInputWal wal)
    {
        this.context = context;
        this.wal = wal;
        callback = EnqueueAsync;
        replayCallback = ObserveReplayAsync;
        stopCallback = DisposeAsync;
        idle.TrySetResult();
        context.Telemetry.RecordOrderedProgress(0, 0, 0, 0, Guid.Empty, 0);
        worker = RunAsync();
    }

    public static Task<FuturesItiOrderedRuntime> StartAsync(IFuturesItiSignalRealtimeContext context, string directory)
    {
        var owner = FuturesItiLocalCommandEndpoint.Current ?? throw new InvalidOperationException("ITI owner must start before ordered intake.");
        var runtime = new FuturesItiOrderedRuntime(context, owner.InputWal ?? throw new InvalidOperationException("ITI WAL was not restored."));
        FuturesItiOrderedIngress.Register(runtime.callback);
        FuturesItiOrderedIngress.RegisterReplay(runtime.replayCallback);
        FuturesItiLocalCommandEndpoint.RegisterIngress(runtime.stopCallback);
        return Task.FromResult(runtime);
    }

    public object Snapshot => new { Accepted = Interlocked.Read(ref accepted), Processed = Interlocked.Read(ref processed),
        Batches = Interlocked.Read(ref batches), Pending = Volatile.Read(ref pending), Capacity = 4096,
        Failed = Volatile.Read(ref failed) != 0, Failure = Volatile.Read(ref failure),
        LastMarketUtc = Interlocked.Read(ref lastMarketTicks) is var ticks && ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null };

    async ValueTask EnqueueAsync(FuturesMarketPriceUpdatedRealtimeEvent source, CancellationToken token)
    {
        Interlocked.Increment(ref captures);
        try
        {
            await captureGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref stoppingFlag) != 0) throw new InvalidOperationException("ITI intake window is closed.");
                await CaptureAsync(source, token).ConfigureAwait(false);
            }
            finally { captureGate.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) { Fail(error); throw; }
        finally { Interlocked.Decrement(ref captures); }
    }

    async ValueTask CaptureAsync(FuturesMarketPriceUpdatedRealtimeEvent source, CancellationToken token)
    {
        if (source.VwapCheckpoint is { IsReplayComplete: true, LastTradeOrdinal: >= 0 and <= 1 } checkpoint)
        {
            var hasContract = context.MarketDataApi.TryGetOnTheRunFuturesContract("ES", out var replayContract);
            if (checkpoint.StreamEpochId != Guid.Empty
                && (hasContract ? replayContract.ContractId == source.EntityId.ContractId
                    : source.EntityId.ContractId.StartsWith("ES", StringComparison.Ordinal)))
                await VerifyEpochStartAsync(checkpoint.StreamEpochId, token).ConfigureAwait(false);
            return;
        }
        if (source.VwapCheckpoint is not null) return;
        // Unrelated quotes/contracts never occupy the ITI mailbox or its admission budget.
        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Trade || source.Price.Trade is not { } trade
            || !source.Price.ContractId.StartsWith("ES", StringComparison.Ordinal)) return;
        var contractReady = context.MarketDataApi.TryGetOnTheRunFuturesContract("ES", out var contract);
        if (contractReady && !string.Equals(contract.ContractId, source.Price.ContractId, StringComparison.Ordinal)) return;
        var usable = FuturesMarketPriceUpdated.IsUsableTradeEvent(source, out _, out var unavailable);
        if (source.EntityId.ContractId != source.Price.ContractId || source.EntityId.ValueDate != source.Price.ValueDate
            || trade.StreamEpochId == Guid.Empty || trade.TradeOrdinal <= 0 || string.IsNullOrWhiteSpace(source.SourceDataset))
            throw new InvalidOperationException("ITI ordered source identity is absent or inconsistent.");
        var vxPrice = 0d;
        if (!context.MarketDataApi.TryGetOnTheRunFuturesContract("VX", out var vxContract)
            || !context.MarketDataApi.TryGetLastTickPrice(vxContract.ContractId, out var vx)
            || vx.Trade is not { LastPrice: > 0 } vxTrade)
            unavailable = "ITI causal VX input is unavailable.";
        else vxPrice = (double)vxTrade.LastPrice;
        DailyItiAtrThreshold? threshold = null;
        if (atr.TryGetLatest(source.Price.ContractId, TimeFrameType.FiveMinutes, out var current)
            && current is { IsWarm: true, PeriodLength: 14, AtrValue: > 0, Metadata.IsValid: true }
            && double.IsFinite(current.AtrValue))
        {
            var value = new DailyItiAtrThreshold(source.Price.ContractId, current.AtrValue,
                current.Metadata.MarketDataAsOfUtc.UtcDateTime, current.Metadata.CalculatedAtUtc.UtcDateTime, current.Metadata.SourceSequence);
            if (value.IsUsableAt(source.Price.ContractId, trade.EventTimestamp.UtcDateTime)) threshold = value;
        }
        if (threshold is null) unavailable = "ITI completed five-minute ATR(14) input is unavailable or stale.";
        if (!contractReady) unavailable = "ITI current ES contract metadata is not ready.";
        var identity = $"ITI|{source.Price.ContractId}|{source.Price.ValueDate:yyyyMMdd}|{source.SourceDataset}|{source.Price.PublisherId}|{source.Price.InstrumentId}|{trade.StreamEpochId:N}|{trade.TradeOrdinal}";
        var command = new GenerateFuturesItiSignalCommand(source.Price.ContractId, source.Price.ValueDate, TimeFrameType.Daily,
            trade.EventTimestamp.UtcDateTime, (double)trade.LastPrice, vxPrice);
        command = command with
        {
            CommandId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0,16)),
            Subject = new(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, command.EntityId.Format()),
            DailyAtr = threshold, RequireDailyAtr = true, InputsFrozen = true,
            InputUnavailableReason = usable && contractReady && vxPrice > 0 && threshold is not null ? null : unavailable,
            VerifiedEpochStart = verifiedEpochs.Contains(trade.StreamEpochId),
            SourceCursor = new(source.SourceDataset, source.Price.PublisherId, source.Price.InstrumentId,
                trade.StreamEpochId, trade.TradeOrdinal, trade.SourceSequence, trade.EventTimestamp.UtcDateTime, trade.ReceiveTimestamp.UtcDateTime)
        };
        if (recent.Contains(command.CommandId)
            || FuturesItiLocalCommandEndpoint.Current?.IsProcessedSource(command.StreamId, command.SourceCursor!) == true) return;
        MessageOrigins.ForPublication(command, TimeProvider.System, causationId: source.Id == Guid.Empty ? null : source.Id);
        var position = await wal.AppendAsync(FuturesItiJournalEnvelope.Encode(command), token).ConfigureAwait(false);
        recent.Add(command.CommandId);
        recentOrder.Enqueue(command.CommandId);
        if (recentOrder.Count > 8192) recent.Remove(recentOrder.Dequeue());
        Interlocked.Increment(ref accepted);
        if (Volatile.Read(ref failed) != 0) return; // Continue authentic WAL capture while decisions remain blocked.
        AddPending();
        try { await channel.Writer.WriteAsync(new(command, position), token).ConfigureAwait(false); }
        catch { CompletePending(1); throw; }
        context.Telemetry.RecordEligibleEsTrade(command.Timestamp);
        context.Telemetry.RecordMarketPriceReceived(command.Timestamp);
        context.Telemetry.RecordCommandRequested();
    }

    async Task RunAsync()
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(lifetime.Token).ConfigureAwait(false))
            {
                var records = new List<Queued>(64);
                if (!channel.Reader.TryRead(out var first)) continue;
                records.Add(first);
                // Preserve every trade and its individual evaluation position, including unchanged prices.
                while (records.Count < 64 && channel.Reader.TryPeek(out var next) && next.Command.StreamId == first.Command.StreamId
                    && channel.Reader.TryRead(out next)) records.Add(next);
                var inputs = records.Select(record => record.Command).ToArray();
                var owner = FuturesItiLocalCommandEndpoint.Current
                    ?? throw new InvalidOperationException("ITI fenced command owner is not available.");
                var prepared = await owner.PrepareInputsAsync(inputs, lifetime.Token).ConfigureAwait(false);
                await wal.AcknowledgeAsync(records[^1].Position).ConfigureAwait(false);
                await owner.ProcessPreparedAsync(prepared, lifetime.Token).ConfigureAwait(false);
                CompletePending(inputs.Length);
                Interlocked.Add(ref processed, inputs.Length);
                for (var index = 0; index < inputs.Length; index++) context.Telemetry.RecordCommandAccepted();
                Interlocked.Increment(ref batches);
                context.Telemetry.RecordOrderedProgress(Volatile.Read(ref pending), Interlocked.Read(ref accepted),
                    Interlocked.Read(ref processed), Interlocked.Read(ref batches), inputs[^1].SourceCursor!.Epoch, inputs[^1].SourceCursor!.Ordinal);
                Interlocked.Exchange(ref lastMarketTicks, inputs[^1].Timestamp.Ticks);
                var lag = DateTime.UtcNow - inputs[^1].Timestamp;
                var ready = lag <= TimeSpan.FromSeconds(2) && string.IsNullOrEmpty(inputs[^1].InputUnavailableReason);
                FuturesItiOrderedIngress.SetHealth(ready, inputs[^1].InputUnavailableReason ?? $"Market lag {lag.TotalMilliseconds:F1} ms.");
                context.HealthEvidence.Record("ITI", "ES", ready ? "Healthy" : "Degraded",
                    $"Ordered journal committed; batch={inputs.Length}; pending={Volatile.Read(ref pending)}; marketLagMs={lag.TotalMilliseconds:F1}.", inputs[^1].Timestamp);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    void Fail(Exception error)
    {
        Volatile.Write(ref failure, error.Message);
        Interlocked.Exchange(ref failed, 1);
        channel.Writer.TryComplete(error);
        idle.TrySetException(error);
        FuturesItiOrderedIngress.SetHealth(false, error.Message);
        context.HealthEvidence.Record("ITI", "ES", "Unhealthy", "Ordered ITI intake stopped for source/commit recovery: " + error.Message);
        context.Telemetry.RecordFailure(error.Message);
    }

    void AddPending()
    {
        lock (pendingGate)
        {
            if (pending++ == 0) idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
    void CompletePending(int count)
    {
        lock (pendingGate)
        {
            pending -= count;
            if (pending == 0) idle.TrySetResult();
        }
    }

    async ValueTask ObserveReplayAsync(FuturesTradeReplayBatchRealtimeEvent source, CancellationToken token)
    {
        if (!source.EntityId.ContractId.StartsWith("ES", StringComparison.Ordinal)) return;
        try
        {
            if (source.IsFirstBatch)
            {
                if (Volatile.Read(ref failed) != 0) throw new InvalidOperationException("Unresolved ITI source failure cannot be cleared by replay restart.");
                replay[source.EntityId.ContractId] = (source.RecoveryGenerationId, source.BatchOrdinal, source.LiveStreamEpochId);
            }
            else if (!replay.TryGetValue(source.EntityId.ContractId, out var prior)
                || prior.Recovery != source.RecoveryGenerationId || source.BatchOrdinal != prior.Batch + 1 || prior.Epoch != source.LiveStreamEpochId)
                throw new InvalidOperationException("ITI provider replay sequence has a gap.");
            else replay[source.EntityId.ContractId] = (source.RecoveryGenerationId, source.BatchOrdinal, source.LiveStreamEpochId);
            if (source.IsFinalBatch && source.LiveStreamEpochId != Guid.Empty)
                await VerifyEpochStartAsync(source.LiveStreamEpochId, token).ConfigureAwait(false);
        }
        catch (Exception error) { Fail(error); throw; }
    }

    async ValueTask VerifyEpochStartAsync(Guid epoch, CancellationToken token)
    {
        if (verifiedEpochs.Contains(epoch)) return;
        if (Volatile.Read(ref failed) != 0) throw new InvalidOperationException("An unresolved ITI source failure cannot be cleared by a new epoch.");
        Task drain;
        lock (pendingGate) drain = pending == 0 ? Task.CompletedTask : idle.Task;
        await drain.WaitAsync(TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
        await FuturesItiLocalCommandEndpoint.Current!.SealProcessedEpochsAsync(token).ConfigureAwait(false);
        verifiedEpochs.Add(epoch);
    }

    public ValueTask DisposeAsync()
    {
        lock (stopGate) return new(stopping ??= StopAsync());
    }

    async Task StopAsync()
    {
        Interlocked.Exchange(ref stoppingFlag, 1);
        FuturesItiOrderedIngress.Unregister(callback);
        FuturesItiOrderedIngress.UnregisterReplay(replayCallback);
        FuturesItiLocalCommandEndpoint.UnregisterIngress(stopCallback);
        FuturesItiOrderedIngress.SetHealth(false, "ITI ordered intake is stopping.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (Volatile.Read(ref captures) != 0) await Task.Delay(1, deadline.Token).ConfigureAwait(false);
        channel.Writer.TryComplete();
        await worker.ConfigureAwait(false);
        if (Volatile.Read(ref failed) == 0 && FuturesItiLocalCommandEndpoint.Current is { } owner)
            await owner.SealProcessedEpochsAsync().ConfigureAwait(false);
        lifetime.Dispose();
    }
    sealed record Queued(GenerateFuturesItiSignalCommand Command, FuturesItiInputWal.Position Position);
}
