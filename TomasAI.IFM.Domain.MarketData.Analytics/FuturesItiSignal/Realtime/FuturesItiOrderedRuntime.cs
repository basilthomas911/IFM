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
    readonly Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask<bool>> recoveryProofCallback;
    readonly Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask<bool>> recoveryReplayProofCallback;
    readonly Func<FuturesTradeReplayBatchRealtimeEvent, CancellationToken, ValueTask> replayCallback;
    readonly Dictionary<string, (Guid Recovery, long Batch, Guid Epoch)> replay = new(StringComparer.Ordinal);
    readonly HashSet<Guid> verifiedEpochs = [];
    readonly HashSet<Guid> recent = [];
    readonly Queue<Guid> recentOrder = new();
    TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly object pendingGate = new();
    readonly Task worker;
    readonly Func<ValueTask> stopCallback;
    readonly Func<FuturesItiOrderedDrainStatus> drainInspector;
    readonly object stopGate = new();
    Task? stopping;
    int stoppingFlag, captures;
    readonly FuturesItiInputWal wal;
    readonly FuturesItiRecoveryCoverage coverage;
    readonly FuturesItiRecoveryCoverage preparations;
    readonly FuturesItiRecoveryCoverage historicalInputGaps;
    readonly FuturesItiReplayAuthority replayAuthority;
    readonly Dictionary<string,FuturesItiReplayProgress> replaySources=new(StringComparer.Ordinal);
    readonly Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> prepareReplayCallback;
    readonly Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> prepareCallback;
    readonly Func<IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent>,CancellationToken,ValueTask> prepareBatchCallback;
    readonly Func<IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent>,CancellationToken,ValueTask> batchCallback;
    readonly System.Collections.Concurrent.ConcurrentDictionary<Guid,Queued> preparedSources=new();
    readonly Dictionary<Guid,(GenerateFuturesItiSignalCommand Command,byte[] Payload)> unflushedSources=[];
    readonly HashSet<Guid> ignoredSources=[];readonly Queue<Guid> ignoredOrder=new();
    readonly Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> validateRecoveryCallback;
    readonly Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> validateReplayRecoveryCallback;
    readonly Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> recoveryCallback;
    readonly Func<FuturesTradeReplayBatchRealtimeEvent,CancellationToken,ValueTask> recoveryReplayCallback;
    long accepted, processed, batches, lastMarketTicks;
    int pending, failed;
    string failure = string.Empty;

    FuturesItiOrderedRuntime(IFuturesItiSignalRealtimeContext context, FuturesItiInputWal wal,string directory)
    {
        this.context = context;
        this.wal = wal;
        coverage=new(Path.Combine(directory,"original-source-coverage"),wal.RootId);
        preparations=new(Path.Combine(directory,"prepared-source-coverage"),wal.RootId);
        historicalInputGaps=new(Path.Combine(directory,"historical-causal-gaps"),wal.RootId);
        if(historicalInputGaps.Count>0)FuturesItiOrderedIngress.SetHealth(false,"Authentic historical ITI sources lack original causal ATR/VX; state reconstruction is required.");
        replayAuthority=new(Path.Combine(directory,"replay-authority"),wal.RootId);
        var authority=replayAuthority.Restore();
        foreach(var original in authority.Progress){replay[original.Contract]=(original.Recovery,original.Batch,original.Epoch);replaySources[original.Contract]=original;}
        foreach(var epoch in authority.VerifiedEpochs)verifiedEpochs.Add(epoch);
        prepareReplayCallback=PrepareOriginalReplayAsync;
        prepareCallback=PrepareOriginalTradeAsync;
        prepareBatchCallback=PrepareBatchAsync;batchCallback=DeliverBatchAsync;
        validateRecoveryCallback=ValidateOriginalTradeAsync;validateReplayRecoveryCallback=ValidateOriginalReplayAsync;
        recoveryCallback=RecoverOriginalTradeAsync;
        recoveryProofCallback=ProveOriginalTradeAsync;recoveryReplayProofCallback=ProveOriginalReplayAsync;
        recoveryReplayCallback=RecoverOriginalReplayAsync;
        callback = EnqueueAsync;
        replayCallback = ObserveReplayAsync;
        stopCallback = DisposeAsync;
        drainInspector=()=>DrainStatus;
        idle.TrySetResult();
        context.Telemetry.RecordOrderedProgress(0, 0, 0, 0, Guid.Empty, 0);
        worker = RunAsync();
    }

    public static async Task<FuturesItiOrderedRuntime> StartAsync(IFuturesItiSignalRealtimeContext context, string directory)
    {
        var owner = FuturesItiLocalCommandEndpoint.Current ?? throw new InvalidOperationException("ITI owner must start before ordered intake.");
        var runtime = new FuturesItiOrderedRuntime(context, owner.InputWal ?? throw new InvalidOperationException("ITI WAL was not restored."),directory);
        await foreach(var row in runtime.wal.ReplayAsync().ConfigureAwait(false))
        {
            var frozen=FuturesItiJournalEnvelope.Decode(row.Payload,0);
            if(frozen.SourceCaptureRequired&&!runtime.wal.IsSourceReleased(frozen))
            {
                if(runtime.preparedSources.Count>=4096)throw new IOException("Unreleased ITI source capacity exceeded; recovery is required.");
                runtime.preparedSources[frozen.SourceEventId]=new(frozen,row.Position);
            }
        }
        FuturesItiOrderedIngress.Register(runtime.callback);
        FuturesItiOrderedIngress.RegisterReplay(runtime.replayCallback);
        FuturesItiOrderedIngress.RegisterRecovery(runtime.recoveryCallback,runtime.recoveryReplayCallback);
        FuturesItiOrderedIngress.RegisterRecoveryProof(runtime.recoveryProofCallback,runtime.recoveryReplayProofCallback);
        FuturesItiOrderedIngress.RegisterRecoveryValidation(runtime.validateRecoveryCallback,runtime.validateReplayRecoveryCallback);
        FuturesItiOrderedIngress.RegisterPreparation(runtime.prepareCallback);
        FuturesItiOrderedIngress.RegisterBatchPreparation(runtime.prepareBatchCallback);
        FuturesItiOrderedIngress.RegisterBatch(runtime.batchCallback);
        FuturesItiOrderedIngress.RegisterReplayPreparation(runtime.prepareReplayCallback);
        FuturesItiLocalCommandEndpoint.RegisterIngress(runtime.stopCallback);
        FuturesItiOrderedIngress.RegisterDrainInspector(runtime.drainInspector);
        return runtime;
    }

    public FuturesItiOrderedDrainStatus DrainStatus=>new(Volatile.Read(ref pending),Volatile.Read(ref captures),preparedSources.Count,unflushedSources.Count,
        Volatile.Read(ref failed)!=0||historicalInputGaps.Count>0,Volatile.Read(ref failure));
    public object Snapshot => new { Accepted = Interlocked.Read(ref accepted), Processed = Interlocked.Read(ref processed),
        Batches = Interlocked.Read(ref batches), Pending = Volatile.Read(ref pending), Capacity = 4096,
        Failed = Volatile.Read(ref failed) != 0, Failure = Volatile.Read(ref failure), HistoricalCausalGaps=historicalInputGaps.Count,
        PreparedSources=preparedSources.Count,UnflushedSources=unflushedSources.Count,
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

    async ValueTask PrepareOriginalTradeAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token)
    {
        if(source.VwapCheckpoint is null){await PrepareBatchAsync([source],token).ConfigureAwait(false);return;}
        Interlocked.Increment(ref captures);
        try
        {
            await captureGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if(Volatile.Read(ref stoppingFlag)!=0)throw new InvalidOperationException("ITI intake window is closed.");
                if(source.VwapCheckpoint is {IsReplayComplete:true,LastTradeOrdinal:>=0 and <=1})
                {preparations.Record(source.Id,MessagePack.MessagePackSerializer.Serialize(source));return;}
                await CaptureAsync(source,token,stageOnly:true).ConfigureAwait(false);
            }
            finally{captureGate.Release();}
        }
        finally{Interlocked.Decrement(ref captures);}
    }

    static bool IsOrdinaryTrade(FuturesMarketPriceUpdatedRealtimeEvent source)=>source.VwapCheckpoint is null
        &&source.UpdateSource==FuturesMarketPriceUpdateSource.Trade&&source.Price.Trade is not null
        &&source.Price.ContractId.StartsWith("ES",StringComparison.Ordinal);

    static Guid OriginalCommandId(FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        var trade=source.Price.Trade??throw new InvalidDataException("ITI source has no original trade.");
        var identity=$"ITI|{source.Price.ContractId}|{source.Price.ValueDate:yyyyMMdd}|{source.SourceDataset}|{source.Price.PublisherId}|{source.Price.InstrumentId}|{trade.StreamEpochId:N}|{trade.TradeOrdinal}";
        return new(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0,16));
    }
    static void ValidateOriginalSource(GenerateFuturesItiSignalCommand command,FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        var trade=source.Price.Trade??throw new InvalidDataException("Prepared ITI source lost its trade.");
        var cursor=new FuturesItiSourceCursor(source.SourceDataset,source.Price.PublisherId,source.Price.InstrumentId,trade.StreamEpochId,trade.TradeOrdinal,
            trade.SourceSequence,trade.EventTimestamp.UtcDateTime,trade.ReceiveTimestamp.UtcDateTime);
        if(command.SourceEventId!=source.Id||command.CommandId!=OriginalCommandId(source)||command.SourceCursor!=cursor||command.Timestamp!=trade.EventTimestamp.UtcDateTime
            ||command.FuturesPrice!=(double)trade.LastPrice)throw new InvalidDataException("Original ITI batch source differs from its frozen causal command.");
    }
    public async ValueTask PrepareBatchAsync(IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent> sources,CancellationToken token)
    {
        if(sources.Count>4096)throw new InvalidDataException("ITI source batch exceeds bounded staging capacity.");
        Interlocked.Increment(ref captures);
        try
        {
            await captureGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if(Volatile.Read(ref stoppingFlag)!=0||Volatile.Read(ref failed)!=0)throw new InvalidOperationException("ITI intake is not available.");
                var fresh=new List<(Guid Source,GenerateFuturesItiSignalCommand Command,byte[] Payload)>();var seen=new Dictionary<Guid,GenerateFuturesItiSignalCommand>();
                // No await until every causal input in this source batch has been captured.
                foreach(var source in sources)
                {
                    if(source.VwapCheckpoint is not null)throw new InvalidDataException("ITI checkpoint controls require their ordered single-item barrier.");
                    if(!IsOrdinaryTrade(source))continue;
                    if(source.Id==Guid.Empty)throw new InvalidDataException("ITI batch source identity is absent.");
                    if(ignoredSources.Contains(source.Id))continue;
                    if(preparedSources.TryGetValue(source.Id,out var prior)){ValidateOriginalSource(prior.Command,source);continue;}
                    if(seen.TryGetValue(source.Id,out var duplicate)){ValidateOriginalSource(duplicate,source);continue;}
                    if(unflushedSources.TryGetValue(source.Id,out var frozen))
                    {ValidateOriginalSource(frozen.Command,source);seen.Add(source.Id,frozen.Command);fresh.Add((source.Id,frozen.Command,frozen.Payload));continue;}
                    var command=FreezeTrade(source,true);
                    if(command is null){ignoredSources.Add(source.Id);ignoredOrder.Enqueue(source.Id);if(ignoredOrder.Count>8192)ignoredSources.Remove(ignoredOrder.Dequeue());continue;}
                    var payload=FuturesItiJournalEnvelope.Encode(command);command=command with{JournalPayload=payload};
                    seen.Add(source.Id,command);fresh.Add((source.Id,command,payload));
                }
                if(preparedSources.Count+unflushedSources.Count+fresh.Count(row=>!unflushedSources.ContainsKey(row.Source))>4096)throw new IOException("Prepared ITI batch capacity exhausted; native sources remain retained.");
                foreach(var row in fresh)unflushedSources.TryAdd(row.Source,(row.Command,row.Payload));
                for(var start=0;start<fresh.Count;start+=64)
                {
                    var group=fresh.Skip(start).Take(64).ToArray();var positions=await wal.AppendBatchAsync(group.Select(row=>row.Payload).ToArray(),token).ConfigureAwait(false);
                    for(var index=0;index<group.Length;index++)
                    {
                        var row=group[index];preparedSources[row.Source]=new(row.Command,positions[index]);
                        unflushedSources.Remove(row.Source);
                        recent.Add(row.Command.CommandId);recentOrder.Enqueue(row.Command.CommandId);if(recentOrder.Count>8192)recent.Remove(recentOrder.Dequeue());
                        Interlocked.Increment(ref accepted);
                    }
                }
            }
            finally{captureGate.Release();}
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception error){Fail(error);throw;}
        finally{Interlocked.Decrement(ref captures);}
    }
    public async ValueTask DeliverBatchAsync(IReadOnlyList<FuturesMarketPriceUpdatedRealtimeEvent> sources,CancellationToken token)
    {
        if(sources.Count>4096)throw new InvalidDataException("ITI source batch exceeds bounded staging capacity.");
        Interlocked.Increment(ref captures);
        try
        {
            await captureGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if(Volatile.Read(ref stoppingFlag)!=0||Volatile.Read(ref failed)!=0)throw new InvalidOperationException("ITI intake is not available.");
                var release=new List<(Guid Source,Queued Queued)>();var seen=new HashSet<Guid>();
                foreach(var source in sources)
                {
                    if(source.VwapCheckpoint is not null)throw new InvalidDataException("ITI checkpoint controls require their ordered single-item barrier.");
                    if(!IsOrdinaryTrade(source)||!seen.Add(source.Id)||ignoredSources.Contains(source.Id))continue;
                    if(preparedSources.TryGetValue(source.Id,out var staged)){ValidateOriginalSource(staged.Command,source);release.Add((source.Id,staged));continue;}
                    var id=OriginalCommandId(source);
                    var descriptor=new GenerateFuturesItiSignalCommand(source.Price.ContractId,source.Price.ValueDate,TimeFrameType.Daily,default,0,0);
                    descriptor=descriptor with{Subject=new(ActorType.Command,GenerateFuturesItiSignalCommand.Actor,GenerateFuturesItiSignalCommand.Verb,descriptor.EntityId.Format())};
                    var trade=source.Price.Trade??throw new InvalidDataException("ITI source has no original trade.");var cursor=new FuturesItiSourceCursor(source.SourceDataset,source.Price.PublisherId,source.Price.InstrumentId,trade.StreamEpochId,trade.TradeOrdinal,trade.SourceSequence,trade.EventTimestamp.UtcDateTime,trade.ReceiveTimestamp.UtcDateTime);
                    if(recent.Contains(id)||FuturesItiLocalCommandEndpoint.Current?.IsProcessedSource(descriptor.StreamId,cursor)==true)continue;
                    throw new InvalidDataException("Confirmed ITI batch has no original frozen preparation; current causal inputs must not be sampled on retry.");
                }
                // Confirm the entire original-source group before allowing its first decision to execute.
                if(release.Count>0)
                {
                    static bool Earlier(FuturesItiInputWal.Position a,FuturesItiInputWal.Position b)=>a.Segment<b.Segment||a.Segment==b.Segment&&a.EndOffset<b.EndOffset;
                    for(var index=1;index<release.Count;index++)if(!Earlier(release[index-1].Queued.Position,release[index].Queued.Position))throw new InvalidDataException("ITI confirmed batch changed original WAL order.");
                    var members=release.Select(row=>row.Source).ToHashSet();
                    if(preparedSources.Any(row=>!members.Contains(row.Key)&&Earlier(row.Value.Position,release[0].Queued.Position)))
                        throw new IOException("An earlier original ITI source is not released; successor must remain retained.");
                }
                wal.ReleaseSourcesBatch(release.Select(row=>row.Queued.Command).ToArray());
                foreach(var row in release)
                {await EnqueueFrozenAsync(row.Queued,token).ConfigureAwait(false);preparedSources.TryRemove(row.Source,out _);}
            }
            finally{captureGate.Release();}
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception error){Fail(error);throw;}
        finally{Interlocked.Decrement(ref captures);}
    }

    async ValueTask CaptureAsync(FuturesMarketPriceUpdatedRealtimeEvent source, CancellationToken token,bool stageOnly=false)
    {
        if (source.VwapCheckpoint is { IsReplayComplete: true, LastTradeOrdinal: >= 0 and <= 1 } checkpoint)
        {
            var hasContract = context.MarketDataApi.TryGetOnTheRunFuturesContract("ES", out var replayContract);
            if (checkpoint.StreamEpochId != Guid.Empty
                && (hasContract ? replayContract.ContractId == source.EntityId.ContractId
                    : source.EntityId.ContractId.StartsWith("ES", StringComparison.Ordinal)))
                await VerifyEpochStartAsync(checkpoint.StreamEpochId, token).ConfigureAwait(false);
            coverage.Record(source.Id,MessagePack.MessagePackSerializer.Serialize(source));
            return;
        }
        if (source.VwapCheckpoint is not null) return;
        // Unrelated quotes/contracts never occupy the ITI mailbox or its admission budget.
        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Trade || source.Price.Trade is not { } trade
            || !source.Price.ContractId.StartsWith("ES", StringComparison.Ordinal)) return;
        if(preparedSources.TryGetValue(source.Id,out var staged))
        {
            ValidateOriginalSource(staged.Command,source);
            if(!stageOnly)await ReleasePreparedAsync(source.Id,staged,token).ConfigureAwait(false);
            return;
        }
        if(unflushedSources.ContainsKey(source.Id))throw new IOException("Original ITI input awaits its original prepared WAL commit; no current inputs may be sampled.");
        var command=FreezeTrade(source,stageOnly);
        if(command is null)return;
        var payload=FuturesItiJournalEnvelope.Encode(command);
        var position = await wal.AppendAsync(payload, token).ConfigureAwait(false);
        command=command with{JournalPayload=payload};
        recent.Add(command.CommandId);
        recentOrder.Enqueue(command.CommandId);
        if (recentOrder.Count > 8192) recent.Remove(recentOrder.Dequeue());
        Interlocked.Increment(ref accepted);
        var queued=new Queued(command,position);
        if(stageOnly)
        {
            if(preparedSources.Count>=4096)throw new IOException("Prepared ITI source capacity exhausted; native source remains retained.");
            if(!preparedSources.TryAdd(source.Id,queued))throw new InvalidDataException("Original ITI preparation identity is already pending.");return;
        }
        await EnqueueFrozenAsync(queued,token).ConfigureAwait(false);
    }

    GenerateFuturesItiSignalCommand? FreezeTrade(FuturesMarketPriceUpdatedRealtimeEvent source,bool stageOnly)
    {
        var trade=source.Price.Trade??throw new InvalidDataException("ITI source has no original trade.");
        var contractReady = context.MarketDataApi.TryGetOnTheRunFuturesContract("ES", out var contract);
        if (contractReady && !string.Equals(contract.ContractId, source.Price.ContractId, StringComparison.Ordinal)) return null;
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
            SourceCaptureRequired=stageOnly,SourceEventId=stageOnly?source.Id:Guid.Empty,
            InputUnavailableReason = usable && contractReady && vxPrice > 0 && threshold is not null ? null : unavailable,
            VerifiedEpochStart = verifiedEpochs.Contains(trade.StreamEpochId),
            SourceCursor = new(source.SourceDataset, source.Price.PublisherId, source.Price.InstrumentId,
                trade.StreamEpochId, trade.TradeOrdinal, trade.SourceSequence, trade.EventTimestamp.UtcDateTime, trade.ReceiveTimestamp.UtcDateTime)
        };
        if (recent.Contains(command.CommandId)
            || FuturesItiLocalCommandEndpoint.Current?.IsProcessedSource(command.StreamId, command.SourceCursor!) == true) return null;
        MessageOrigins.ForPublication(command, TimeProvider.System, causationId: source.Id == Guid.Empty ? null : source.Id);
        return command;
    }

    async ValueTask ReleasePreparedAsync(Guid sourceId,Queued staged,CancellationToken token)
    {
        wal.ReleaseSource(staged.Command); // Exact source/recipient capture already confirmed at caller.
        await EnqueueFrozenAsync(staged,token).ConfigureAwait(false);
        preparedSources.TryRemove(sourceId,out _);
    }
    async ValueTask EnqueueFrozenAsync(Queued staged,CancellationToken token)
    {
        if (Volatile.Read(ref failed) != 0) return; // WAL remains owned for restart recovery.
        AddPending();
        try { await channel.Writer.WriteAsync(staged, token).ConfigureAwait(false); }
        catch { CompletePending(1); throw; }
        context.Telemetry.RecordEligibleEsTrade(staged.Command.Timestamp);
        context.Telemetry.RecordMarketPriceReceived(staged.Command.Timestamp);
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
                var end=records[^1].Position;
                if(preparedSources.Values.All(row=>row.Position.Segment>end.Segment||row.Position.Segment==end.Segment&&row.Position.EndOffset>end.EndOffset))
                    await wal.AcknowledgeAsync(end).ConfigureAwait(false);
                await owner.ProcessPreparedAsync(prepared, lifetime.Token).ConfigureAwait(false);
                CompletePending(inputs.Length);
                Interlocked.Add(ref processed, inputs.Length);
                for (var index = 0; index < inputs.Length; index++) context.Telemetry.RecordCommandAccepted();
                Interlocked.Increment(ref batches);
                context.Telemetry.RecordOrderedProgress(Volatile.Read(ref pending), Interlocked.Read(ref accepted),
                    Interlocked.Read(ref processed), Interlocked.Read(ref batches), inputs[^1].SourceCursor!.Epoch, inputs[^1].SourceCursor!.Ordinal);
                Interlocked.Exchange(ref lastMarketTicks, inputs[^1].Timestamp.Ticks);
                var lag = DateTime.UtcNow - inputs[^1].Timestamp;
                var ready = historicalInputGaps.Count==0&&lag <= TimeSpan.FromSeconds(2) && string.IsNullOrEmpty(inputs[^1].InputUnavailableReason);
                var readinessReason=historicalInputGaps.Count>0?"Authentic historical ITI sources lack original causal ATR/VX; state reconstruction is required.":inputs[^1].InputUnavailableReason??$"Market lag {lag.TotalMilliseconds:F1} ms.";
                FuturesItiOrderedIngress.SetHealth(ready,readinessReason);
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

    ValueTask RecoverOriginalTradeAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token)=>RecoverOriginalTradeCoreAsync(source,token,release:true);
    ValueTask<bool> ProveOriginalTradeAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!source.EntityId.ContractId.StartsWith("ES",StringComparison.Ordinal))return ValueTask.FromResult(true);
        if(source.VwapCheckpoint is{} checkpoint)
            return ValueTask.FromResult(checkpoint is not{IsReplayComplete:true,LastTradeOrdinal:>=0 and <=1}
                ||coverage.Contains(source.Id,MessagePack.MessagePackSerializer.Serialize(source)));
        if(source.UpdateSource!=FuturesMarketPriceUpdateSource.Trade)return ValueTask.FromResult(true);
        return FuturesItiLocalCommandEndpoint.Current is{} owner?owner.ProveProcessedOriginalAsync(source,token):ValueTask.FromResult(false);
    }
    ValueTask<bool> ProveOriginalReplayAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(!source.EntityId.ContractId.StartsWith("ES",StringComparison.Ordinal)
            ||coverage.Contains(source.Id,MessagePack.MessagePackSerializer.Serialize(source)));
    }
    ValueTask ValidateOriginalTradeAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token)=>RecoverOriginalTradeCoreAsync(source,token,release:false);
    async ValueTask RecoverOriginalTradeCoreAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token,bool release)
    {
        // Non-trade checkpoint/other-contract facts do not feed ITI decisions.
        if(!source.EntityId.ContractId.StartsWith("ES",StringComparison.Ordinal))return;
        if(source.VwapCheckpoint is {IsReplayComplete:true,LastTradeOrdinal:>=0 and <=1})
        {
            var payload=MessagePack.MessagePackSerializer.Serialize(source);
            if(coverage.Contains(source.Id,payload))return;
            if(!preparations.Contains(source.Id,payload))preparations.Record(source.Id,payload); // Original immutable checkpoint itself supplies its historical epoch proof.
            if(source.VwapCheckpoint.StreamEpochId==Guid.Empty)throw new InvalidDataException("Original checkpoint has no epoch authority.");
            if(!release)return;
            await VerifyEpochStartAsync(source.VwapCheckpoint.StreamEpochId,token).ConfigureAwait(false);
            SaveReplayAuthority();coverage.Record(source.Id,payload);return;
        }
        if(source.VwapCheckpoint is not null)return;
        if(source.UpdateSource!=FuturesMarketPriceUpdateSource.Trade||source.Price.Trade is not{} trade||!source.Price.ContractId.StartsWith("ES",StringComparison.Ordinal))return;
        if(source.EntityId.ContractId!=source.Price.ContractId||source.EntityId.ValueDate!=source.Price.ValueDate||trade.StreamEpochId==Guid.Empty||trade.TradeOrdinal<1||string.IsNullOrWhiteSpace(source.SourceDataset))
            throw new InvalidDataException("Original ordered ITI source cursor is absent or inconsistent.");
        var cursor=new FuturesItiSourceCursor(source.SourceDataset,source.Price.PublisherId,source.Price.InstrumentId,trade.StreamEpochId,trade.TradeOrdinal,trade.SourceSequence,trade.EventTimestamp.UtcDateTime,trade.ReceiveTimestamp.UtcDateTime);
        var descriptor=new GenerateFuturesItiSignalCommand(source.Price.ContractId,source.Price.ValueDate,TimeFrameType.Daily,trade.EventTimestamp.UtcDateTime,(double)trade.LastPrice,0);
        descriptor=descriptor with{Subject=new(ActorType.Command,GenerateFuturesItiSignalCommand.Actor,GenerateFuturesItiSignalCommand.Verb,descriptor.EntityId.Format())};
        await captureGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if(FuturesItiLocalCommandEndpoint.Current?.IsProcessedSource(descriptor.StreamId,cursor)==true)return;
            await foreach(var row in wal.ReplayAsync(token).ConfigureAwait(false))
            {
                var frozen=FuturesItiJournalEnvelope.Decode(row.Payload,0);
                if(frozen.StreamId==descriptor.StreamId&&frozen.SourceCursor==cursor)
                {
                    if(!frozen.InputsFrozen||frozen.FuturesPrice!=(double)trade.LastPrice||frozen.Timestamp!=trade.EventTimestamp.UtcDateTime)
                        throw new InvalidDataException("Original ITI WAL source differs from retained native frame.");
                    if(release&&!wal.IsSourceReleased(frozen))
                    {
                        var queued=new Queued(frozen,row.Position);
                        await ReleasePreparedAsync(source.Id,queued,token).ConfigureAwait(false);
                    }
                    return; // Exact frozen command is released only after confirmed source capture.
                }
            }
            // The authentic source exists, but no decision ever captured its causal inputs. Preserve it as an explicit
            // unavailable-context input, not a fabricated reconstructed signal. A sticky durable gap blocks entries.
            var identity=$"ITI|{source.Price.ContractId}|{source.Price.ValueDate:yyyyMMdd}|{source.SourceDataset}|{source.Price.PublisherId}|{source.Price.InstrumentId}|{trade.StreamEpochId:N}|{trade.TradeOrdinal}";
            var unavailable=descriptor with
            {
                CommandId=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0,16)),
                InputsFrozen=true,RequireDailyAtr=true,DailyAtr=null,SourceCursor=cursor,SourceCaptureRequired=true,SourceEventId=source.Id,
                InputUnavailableReason="Historical source recovered without original causal ATR/VX; no ITI signal may be synthesized.",
                VerifiedEpochStart=verifiedEpochs.Contains(cursor.Epoch)||cursor.Ordinal==1
            };
            MessageOrigins.Copy(source,unavailable);
            historicalInputGaps.Record(source.Id,MessagePack.MessagePackSerializer.Serialize(source));
            FuturesItiOrderedIngress.SetHealth(false,"Authentic historical ITI sources lack original causal ATR/VX; state reconstruction is required.");
            var payload=FuturesItiJournalEnvelope.Encode(unavailable);var position=await wal.AppendAsync(payload,token).ConfigureAwait(false);
            unavailable=unavailable with{JournalPayload=payload};var staged=new Queued(unavailable,position);preparedSources[source.Id]=staged;
            if(release)await ReleasePreparedAsync(source.Id,staged,token).ConfigureAwait(false);
        }
        finally{captureGate.Release();}
    }
    ValueTask PrepareOriginalReplayAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(source.EntityId.ContractId.StartsWith("ES",StringComparison.Ordinal))preparations.Record(source.Id,MessagePack.MessagePackSerializer.Serialize(source));
        return ValueTask.CompletedTask;
    }
    ValueTask ValidateOriginalReplayAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(!source.EntityId.ContractId.StartsWith("ES",StringComparison.Ordinal))return ValueTask.CompletedTask;
        var payload=MessagePack.MessagePackSerializer.Serialize(source);
        if(!coverage.Contains(source.Id,payload)&&!preparations.Contains(source.Id,payload))preparations.Record(source.Id,payload);
        return ValueTask.CompletedTask;
    }
    async ValueTask RecoverOriginalReplayAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!source.EntityId.ContractId.StartsWith("ES",StringComparison.Ordinal))return;
        var payload=MessagePack.MessagePackSerializer.Serialize(source);
        if(coverage.Contains(source.Id,payload))return;
        if(!preparations.Contains(source.Id,payload))throw new InvalidOperationException("Original ITI replay batch has no frozen original preparation.");
        await ObserveReplayAsync(source,token).ConfigureAwait(false);
    }
    void SaveReplayAuthority()=>replayAuthority.Save(new(replaySources.Values.ToArray(),verifiedEpochs.ToArray()));

    async ValueTask ObserveReplayAsync(FuturesTradeReplayBatchRealtimeEvent source, CancellationToken token)
    {
        if (!source.EntityId.ContractId.StartsWith("ES", StringComparison.Ordinal)) return;
        var payload=MessagePack.MessagePackSerializer.Serialize(source);
        if(coverage.Contains(source.Id,payload))return;
        if(replaySources.TryGetValue(source.EntityId.ContractId,out var original)&&original.SourceId==source.Id)
        {
            if(!original.SourceSha256.AsSpan().SequenceEqual(SHA256.HashData(payload)))throw new InvalidDataException("Original replay authority source changed.");
            coverage.Record(source.Id,payload);return;
        }
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
            replaySources[source.EntityId.ContractId]=new(source.EntityId.ContractId,source.RecoveryGenerationId,source.BatchOrdinal,source.LiveStreamEpochId,source.Id,SHA256.HashData(payload));
            SaveReplayAuthority();coverage.Record(source.Id,payload);
        }
        catch (Exception error) { Fail(error); throw; }
    }

    async ValueTask VerifyEpochStartAsync(Guid epoch, CancellationToken token)
    {
        if (verifiedEpochs.Contains(epoch)) return;
        if (Volatile.Read(ref failed) != 0) throw new InvalidOperationException("An unresolved ITI source failure cannot be cleared by a new epoch.");
        if(preparedSources.Count!=0||unflushedSources.Count!=0)throw new IOException("Original ITI sources must finish their confirmed capture/release before an epoch barrier.");
        Task drain;
        lock (pendingGate) drain = pending == 0 ? Task.CompletedTask : idle.Task;
        await drain.WaitAsync(TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
        await FuturesItiLocalCommandEndpoint.Current!.SealProcessedEpochsAsync(token).ConfigureAwait(false);
        verifiedEpochs.Add(epoch);
        SaveReplayAuthority();
    }

    public ValueTask DisposeAsync()
    {
        lock (stopGate) return new(stopping ??= StopAsync());
    }

    async Task StopAsync()
    {
        Interlocked.Exchange(ref stoppingFlag, 1);
        FuturesItiOrderedIngress.Unregister(callback);
        FuturesItiOrderedIngress.UnregisterDrainInspector(drainInspector);
        FuturesItiOrderedIngress.UnregisterReplay(replayCallback);
        FuturesItiOrderedIngress.UnregisterRecovery(recoveryCallback,recoveryReplayCallback);
        FuturesItiOrderedIngress.UnregisterRecoveryProof(recoveryProofCallback,recoveryReplayProofCallback);
        FuturesItiOrderedIngress.UnregisterRecoveryValidation(validateRecoveryCallback,validateReplayRecoveryCallback);
        FuturesItiOrderedIngress.UnregisterPreparation(prepareCallback);
        FuturesItiOrderedIngress.UnregisterBatchPreparation(prepareBatchCallback);
        FuturesItiOrderedIngress.UnregisterBatch(batchCallback);
        FuturesItiOrderedIngress.UnregisterReplayPreparation(prepareReplayCallback);
        FuturesItiLocalCommandEndpoint.UnregisterIngress(stopCallback);
        FuturesItiOrderedIngress.SetHealth(false, "ITI ordered intake is stopping.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (Volatile.Read(ref captures) != 0) await Task.Delay(1, deadline.Token).ConfigureAwait(false);
        channel.Writer.TryComplete();
        await worker.ConfigureAwait(false);
        if (Volatile.Read(ref failed) == 0 && preparedSources.Count==0 && unflushedSources.Count==0 && FuturesItiLocalCommandEndpoint.Current is { } owner)
            await owner.SealProcessedEpochsAsync().ConfigureAwait(false);
        coverage.Dispose();
        preparations.Dispose();
        historicalInputGaps.Dispose();
        lifetime.Dispose();
    }
    sealed record Queued(GenerateFuturesItiSignalCommand Command, FuturesItiInputWal.Position Position);
}
