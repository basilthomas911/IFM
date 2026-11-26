using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
public sealed class IronCondorTradePositionRealtimeContext : EventActorContext, IIronCondorTradePositionRealtimeContext,IIronCondorMonitoringReplayEvidence
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public IronCondorTradePositionRealtimeContext(IActorSupervisor supervisor, ILogger<IronCondorTradePositionRealtimeActor> logger, IDbContextFactory? databases = null, IFinancialQueryStore? financialQueries = null, StrategyRiskParameterSetResolver? riskParameterSets = null, TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.TradeRealtimeCacheRegistry? realtimeCache = null, TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? marketData = null, TimeProvider? timeProvider = null, TomasAI.IFM.Domain.Trade.Persistence.IronCondorAutomaticExitDispatcher? exits = null, IronCondorPositionRequests? requests = null,IRealtimeSessionRecorder? sessionRecorder=null) : base(supervisor, new(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName))
    {
        Requests = requests ?? throw new InvalidOperationException("Durable Iron Condor position request capture is required.");
        TimeProvider = timeProvider ?? TimeProvider.System;
        Logger = logger;
        this.realtimeCache = realtimeCache;
        this.databases = databases;
        this.exits = exits;
        this.sessionRecorder=sessionRecorder;
        inputReader = databases is null ? null : new(databases, logger, financialQueries, stoppingToken: RealtimeGeneration.Token, publishInputChange: async (position, date, inputs, now) =>
        {
            await SendAsync<TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent, StrategyPositionId>(new() { Subject = new(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName, TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent.Verb, position.Id.Format()), Id = Guid.NewGuid(), EntityId = position.Id, PositionSnapshot = position, ValueDate = date, IronCondorTradePlanInputs = inputs, ReceivedOn = now, MonitoringGenerationId = RealtimeGeneration.Id }).ConfigureAwait(false);
        }, riskParameterSets: riskParameterSets, marketData: marketData, timeProvider: TimeProvider);
    }

    readonly IronCondorMonitoringInputReader? inputReader;
    readonly IRealtimeSessionRecorder? sessionRecorder;
    readonly TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.TradeRealtimeCacheRegistry? realtimeCache;
    readonly IDbContextFactory? databases;
    readonly TomasAI.IFM.Domain.Trade.Persistence.IronCondorAutomaticExitDispatcher? exits;
    readonly System.Collections.Concurrent.ConcurrentDictionary<StrategyPositionId, bool> closing = new();
    readonly System.Collections.Concurrent.ConcurrentDictionary<StrategyPositionId, DateTimeOffset> closeChecks = new();
    public async ValueTask<bool> IsClosingAsync(StrategyPositionId position)
    {
        var now=TimeProvider.GetUtcNow();
        if (closing.TryGetValue(position, out var result)
            && (result || closeChecks.TryGetValue(position,out var checkedAt) && now-checkedAt<TimeSpan.FromSeconds(5))) return result;
        result = exits is not null && await exits.HasIntentAsync(position, RealtimeGeneration.Token).ConfigureAwait(false);
        closeChecks[position]=now;
        return closing.AddOrUpdate(position,result,(_,prior)=>prior||result);
    }
    readonly System.Collections.Concurrent.ConcurrentDictionary<TradeEntityId, SemaphoreSlim> publicationGates = new();

    /// <summary>Drains exact retained notifications before the actor accepts fresh messages.</summary>
    public async ValueTask RecoverPublicationsAsync()
    {
        foreach (var trade in Requests.Publications.Pending().Select(receipt => receipt.Event.EntityId).Distinct())
        {
            var gate = publicationGates.GetOrAdd(trade, static _ => new(1, 1));
            await gate.WaitAsync(RealtimeGeneration.Token).ConfigureAwait(false);
            try { await FlushPublicationsAsync(trade).ConfigureAwait(false); }
            finally { gate.Release(); }
        }
    }

    async ValueTask FlushPublicationsAsync(TradeEntityId trade)
    {
        foreach (var pending in Requests.Publications.Pending(trade))
            await SendCapturedPublicationAsync(pending).ConfigureAwait(false);
    }

    async ValueTask SendCapturedPublicationAsync(IronCondorPlanPublication publication)
    {
        RealtimeGeneration.Token.ThrowIfCancellationRequested();
        if (!publication.Sent)
        {
            await SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(publication.Event).ConfigureAwait(false);
            RealtimeGeneration.Token.ThrowIfCancellationRequested();
            Requests.Publications.MarkSent(publication.Key);
        }
        else Requests.Publications.MarkSent(publication.Key); // Finish an interrupted archival rename.
        if (publication.Event.Snapshot.Plan?.RequiresExit == true)
            closing[publication.Event.Snapshot.Position!.Id] = true;
        realtimeCache?.RecordPublication(publication.Event.Snapshot);
    }

    public async ValueTask PublishRealtimeAsync(StrategyPositionSnapshot position, StrategyTradePlanSnapshot? plan = null)
    {
        RealtimeGeneration.Token.ThrowIfCancellationRequested();
        var gate = publicationGates.GetOrAdd(position.Id.Trade, static _ => new(1, 1));
        await gate.WaitAsync(RealtimeGeneration.Token).ConfigureAwait(false);
        try
        {
            // Recover older sends independently of whether this observation changes the cache.
            await FlushPublicationsAsync(position.Id.Trade).ConfigureAwait(false);
            if (realtimeCache is null)
                throw new InvalidOperationException("Iron Condor publication requires the realtime cache.");
            var cache = realtimeCache.Find(position.Id.Trade);
            if (!position.IsOpen && cache is null) return;
            if (cache is null || cache.Read().ValueDate != position.ValueDate
                || !cache.Read().Legs.Select(leg => (leg.Definition.TradeLegId, leg.Definition.ContractId)).OrderBy(leg => leg.TradeLegId)
                    .SequenceEqual(position.Legs.Select(leg => (leg.TradeLegId, leg.ContractId)).OrderBy(leg => leg.TradeLegId)))
            {
                var trade = databases is null ? null : await databases.TradeDb.GetEstablishedTradeAsync(position.Id.Trade).ConfigureAwait(false);
                if (trade is null) throw new InvalidOperationException("Iron Condor publication has no established trade.");
                cache = realtimeCache.Register(trade, position.ValueDate);
            }
            var current = cache.Read();
            if (current.Position is { } latest && (latest.RouteGeneration > position.RouteGeneration
                || latest.RouteGeneration == position.RouteGeneration && latest.PositionSequence > position.PositionSequence)) return;
            if (plan is not null && current.Plan is { } latestPlan && latestPlan.Position.RouteGeneration == position.RouteGeneration
                && latestPlan.PlanRevision > plan.PlanRevision) return;
            cache.Apply(position);
            if (plan is not null) cache.Apply(plan);
            var key = TradePlanContractIdentity.DeterministicId(FormattableString.Invariant(
                $"IronCondorNotification/v1|{position.Id.Format()}|{position.ValueDate:yyyy-MM-dd}|{position.RouteGeneration}|{position.PositionSequence}|{(plan is null ? "position" : "plan")}|{plan?.PlanRevision ?? 0}"));
            var hash = Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(new object?[] { position, plan })));
            var captured = Requests.Publications.Prepare(key, hash,
                () => MessageOrigins.Create(TimeProvider, () => TradeRealtimeSnapshotChangedEvent.From(cache.ReadForPublication())));
            await SendCapturedPublicationAsync(captured).ConfigureAwait(false);
            if (!position.IsOpen) realtimeCache.Remove(position.Id.Trade);
        }
        finally { gate.Release(); }
    }
    readonly System.Collections.Concurrent.ConcurrentDictionary<StrategyPositionId, StrategyPositionSnapshot> positions = new();
    /// <summary>Captures current monitoring inputs without waiting for a storage refresh.</summary>
    public IronCondorTradePlanInputs? CaptureMonitoringInputs(StrategyPositionSnapshot position, DateOnly valueDate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(position);
        RealtimeGeneration.Token.ThrowIfCancellationRequested();
        positions.AddOrUpdate(position.Id, position, (_, current) => position.RouteGeneration > current.RouteGeneration || position.RouteGeneration == current.RouteGeneration && position.PositionSequence >= current.PositionSequence ? position : current);
        return inputReader?.Capture(position, valueDate, nowUtc);
    }

    /// <summary>Fences delayed input notifications from a retired lifetime or preceding position revision.</summary>
    public bool IsCurrentMonitoringPosition(StrategyPositionSnapshot position, Guid generationId)
    {
        ArgumentNullException.ThrowIfNull(position);
        return !RealtimeGeneration.Token.IsCancellationRequested && generationId == RealtimeGeneration.Id && positions.TryGetValue(position.Id, out var current) && position.RouteGeneration == current.RouteGeneration && position.PositionSequence == current.PositionSequence;
    }
    public bool IsCurrentRecordedMonitoringInput(TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent input,string payloadSha256)
    {
        if(RealtimeGeneration.Token.IsCancellationRequested || TimeProvider is not TomasAI.IFM.Shared.Time.IRecordedRealtimeSourceReplay replay || !replay.UsesRecordedSource(input.Subject.Name,input.Subject.Verb) || TimeProvider is not TomasAI.IFM.Shared.Time.IRecordedRealtimeGenerationAuthority authority)return false;
        var proof=GenerationAcceptance(input,payloadSha256);
        if(!authority.Allows(proof,RealtimeGeneration.Id))return false;
        // This exact originally accepted observation may hydrate only a missing cache entry, never supersede one.
        positions.TryAdd(input.PositionSnapshot.Id,input.PositionSnapshot);
        return !RealtimeGeneration.Token.IsCancellationRequested&&positions.TryGetValue(input.PositionSnapshot.Id,out var current)&&current.RouteGeneration==input.PositionSnapshot.RouteGeneration&&current.PositionSequence==input.PositionSnapshot.PositionSequence;
    }
    public async ValueTask RecordMonitoringGenerationAcceptanceAsync(TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent input,string payloadSha256)
    {
        if(sessionRecorder is not{Enabled:true} || TimeProvider is TomasAI.IFM.Shared.Time.IRecordedRealtimeSourceReplay)return;
        if(!IsCurrentMonitoringPosition(input.PositionSnapshot,input.MonitoringGenerationId)||input.Id==Guid.Empty||input.EntityId!=input.PositionSnapshot.Id||input.ReceivedOn.Kind!=DateTimeKind.Utc||input.Subject.EntityId!=input.EntityId.Format()||!input.Subject.Is(ActorType.Realtime,IronCondorTradePositionRealtimeActor.ActorName,TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent.Verb))
            throw new InvalidDataException("Only an original normally accepted monitoring generation can create authority evidence.");
        var proof=GenerationAcceptance(input,payloadSha256);
        var wire=MessagePackSerializer.Serialize(proof);
        var receipt=await sessionRecorder.RecordAsync(new(){Kind="MonitoringGenerationAccepted",LogicalSourceKey=$"{input.Subject}|{input.Id:N}|{payloadSha256}",Subject=input.Subject.ToString(),OriginalWire=wire,
            Origin=MessageOrigins.Get(input),EventId=input.Id,GenerationId=input.MonitoringGenerationId,Sequence=input.PositionSnapshot.PositionSequence},RealtimeGeneration.Token).ConfigureAwait(false);
        if(!string.Equals(receipt.OriginalWireSha256,Convert.ToHexString(SHA256.HashData(wire)),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Monitoring generation archive did not prove the exact original permission receipt.");
    }
    static TomasAI.IFM.Shared.Time.RecordedRealtimeGenerationAcceptance GenerationAcceptance(TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent input,string payloadSha256)
        =>new(){Subject=input.Subject,EventId=input.Id,OriginalGenerationId=input.MonitoringGenerationId,PayloadSha256=payloadSha256,PositionSequence=input.PositionSnapshot.PositionSequence,RouteGeneration=input.PositionSnapshot.RouteGeneration};

    public IronCondorPositionRequests Requests { get; }
    public TimeProvider TimeProvider { get; }
    public TradePlanParameters Parameters { get; } = new();
    public ILogger<IronCondorTradePositionRealtimeActor> Logger { get; }
}
