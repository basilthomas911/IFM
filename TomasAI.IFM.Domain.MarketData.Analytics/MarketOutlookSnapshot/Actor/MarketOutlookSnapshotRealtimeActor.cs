using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

/// <summary>
/// Retains asynchronous Market Outlook inputs and emits one coherent frontend
/// snapshot only when the corresponding EOD projection completes.
/// </summary>
public class MarketOutlookSnapshotRealtimeActor(
    IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> actorContext)
    : BaseEventActor<MarketOutlookSnapshotRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IMarketOutlookSnapshotRealtimeContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IMarketOutlookSnapshotRealtimeContext, nameof(actorContext))!;

    /// <summary>Gets the stable actor mailbox name retained for wire compatibility.</summary>
    public const string ActorName = "MarketOutlook";
    readonly ConcurrentDictionary<MarketOutlookEntityId, MarketOutlookSnapshotState> _states = new();

    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [MarketOutlookComponentChangedRealtimeEvent.Verb] =
            message => message.AsEvent<MarketOutlookComponentChangedRealtimeEvent>()!,
        [MarketOutlookEodUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<MarketOutlookEodUpdatedRealtimeEvent>()!
    };

    protected override IEvent ParseMessage(IEventActorContext<MarketOutlookSnapshotRealtimeActor> context, IActorMessage message)
    {
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !Parsers.TryGetValue(subject.Verb, out var parser))
            return default!;
        var @event = parser(message);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    protected override async ValueTask ReceiveAsync(IEventActorContext<MarketOutlookSnapshotRealtimeActor> context, IEvent @event)
    {
        switch (@event)
        {
            case MarketOutlookComponentChangedRealtimeEvent changed:
                Observe(changed);
                break;
            case MarketOutlookEodUpdatedRealtimeEvent eod:
                await PublishSnapshotAsync(eod, context).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unable to resolve {ActorName} realtime event from {@event.Subject}.");
        }
    }

    void Observe(MarketOutlookComponentChangedRealtimeEvent changed)
    {
        var state = _states.GetOrAdd(changed.EntityId, static _ => new MarketOutlookSnapshotState());
        if (changed.FuturesRsiSignal is { } rsi
            && rsi.TimePeriod == FuturesTradeSignalPrerequisites.SignalTimePeriod
            && rsi.PeriodLength == FuturesIntradaySignalActivationProfile.RsiPeriodLength)
            state.Rsi = rsi;
        if (changed.FuturesTdiSignal is { } tdi
            && tdi.TimePeriod == FuturesTradeSignalPrerequisites.SignalTimePeriod
            && tdi.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId)
            state.Tdi = tdi;
        if (changed.FuturesItiSignal is { TimePeriod: TimeFrameType.Daily } iti)
        {
            state.ObserveIti(iti);
            if (changed.VixFuturesPrice > 0)
                state.VixFuturesPrice = changed.VixFuturesPrice;
        }
    }

    async ValueTask PublishSnapshotAsync(
        MarketOutlookEodUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (!string.Equals(source.FuturesEodData.Symbol, "ES", StringComparison.Ordinal))
            return;

        var state = _states.GetOrAdd(source.EntityId, static _ => new MarketOutlookSnapshotState());
        await HydrateAsync(state, source.EntityId, context).ConfigureAwait(false);

        var enrichedEod = await context.GetFuturesEodDataAsync(
                source.EntityId.ContractId,
                source.EntityId.ValueDate)
            .ConfigureAwait(false) ?? source.FuturesEodData;
        var missing = state.GetMissingInputs();
        FuturesTradeSignalV2ReadModel? tradeSignal = null;
        if (missing.Count == 0)
        {
            var command = new UpdateFuturesTradeSignalCommand(
                enrichedEod,
                state.Rsi,
                state.Tdi,
                state.ToItiData(),
                state.VixFuturesPrice,
                FuturesTradeSignalPrerequisites.SignalTimePeriod);
            _ = command.Compute(out FuturesTradeSignalCompute compute);
            tradeSignal = compute.FuturesTradeSignal;
        }

        state.CurrentSnapshot ??= await actorContext.DbFactory.MarketDataDb
            .GetMarketOutlookSnapshotAsync(source.EntityId.ContractId, source.EntityId.ValueDate)
            .ConfigureAwait(false);
        tradeSignal ??= state.CurrentSnapshot?.FuturesTradeSignal;
        var snapshot = new MarketOutlookSnapshotReadModel(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            checked((state.CurrentSnapshot?.Revision ?? 0) + 1),
            DateTime.UtcNow,
            enrichedEod,
            tradeSignal,
            string.Join(", ", missing));
        await actorContext.DbFactory.MarketDataDb.UpsertMarketOutlookSnapshotAsync(snapshot)
            .ConfigureAwait(false);
        state.CurrentSnapshot = snapshot;

        var notification = new MarketOutlookUpdatedNotifyEvent
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                MarketOutlookUpdatedNotifyEvent.Actor,
                MarketOutlookUpdatedNotifyEvent.Verb,
                source.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = source.EntityId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = nameof(MarketOutlookEodUpdatedRealtimeEvent),
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = snapshot
        };
        await context.SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(notification)
            .ConfigureAwait(false);
    }

    async ValueTask HydrateAsync(
        MarketOutlookSnapshotState state,
        MarketOutlookEntityId id,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        var db = actorContext.DbFactory.MarketDataDb;
        // Re-read every persisted input at the EOD barrier. The coordinator cache keeps
        // asynchronous updates available, while the read closes any cross-mailbox race
        // where an input was committed before EOD but its realtime message arrives later.
        var rsiTask = db.GetLastFuturesRsiSignalAsync(
            id.ContractId, id.ValueDate,
            FuturesTradeSignalPrerequisites.SignalTimePeriod,
            FuturesIntradaySignalActivationProfile.RsiPeriodLength);
        var tdiTask = db.GetLastFuturesTdiSignalAsync(
            id.ContractId, id.ValueDate,
            FuturesTradeSignalPrerequisites.SignalTimePeriod,
            FuturesTdiConfiguration.StandardConfigurationId);
        var directionTask = db.GetLastFuturesItiSignalTrendDirectionChangeAsync(
            id.ContractId, id.ValueDate);
        var extremeTask = db.GetLastFuturesItiSignalTrendExtremeChangeAsync(
            id.ContractId, id.ValueDate);
        var reversalTask = db.GetLastFuturesItiSignalTrendReversalChangeAsync(
            id.ContractId, id.ValueDate);
        var vixTask = context.GetVixFuturesEodDataClosePriceAsync(id.ValueDate).AsTask();

        await Task.WhenAll(rsiTask, tdiTask, directionTask, extremeTask, reversalTask, vixTask)
            .ConfigureAwait(false);
        state.Rsi = await rsiTask.ConfigureAwait(false) ?? state.Rsi;
        state.Tdi = await tdiTask.ConfigureAwait(false) ?? state.Tdi;
        state.TrendDirectionChange = await directionTask.ConfigureAwait(false)
            ?? state.TrendDirectionChange;
        state.TrendExtremeChange = await extremeTask.ConfigureAwait(false)
            ?? state.TrendExtremeChange;
        state.TrendReversalChange = await reversalTask.ConfigureAwait(false)
            ?? state.TrendReversalChange;
        var persistedVixPrice = await vixTask.ConfigureAwait(false);
        if (persistedVixPrice > 0)
            state.VixFuturesPrice = persistedVixPrice;
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        actorContext.Logger.LogErrorEvent(ActorName, exception,
            "Market Outlook coordination failed for {EntityId}", @event.Subject.EntityId);
        await ValueTask.CompletedTask;
    }

    sealed class MarketOutlookSnapshotState
    {
        internal FuturesRsiSignalReadModel? Rsi { get; set; }
        internal FuturesTdiSignalReadModel? Tdi { get; set; }
        internal FuturesItiSignalV2ReadModel? TrendDirectionChange { get; set; }
        internal FuturesItiSignalV2ReadModel? TrendExtremeChange { get; set; }
        internal FuturesItiSignalV2ReadModel? TrendReversalChange { get; set; }
        internal decimal VixFuturesPrice { get; set; }
        internal MarketOutlookSnapshotReadModel? CurrentSnapshot { get; set; }

        internal void ObserveIti(FuturesItiSignalV2ReadModel signal)
        {
            switch (signal.IntrinsicTimeMode)
            {
                case IntrinsicTimeModeType.TrendDirectionChanged:
                    TrendDirectionChange = signal;
                    break;
                case IntrinsicTimeModeType.TrendExtremeChanged:
                    TrendExtremeChange = signal;
                    break;
                case IntrinsicTimeModeType.TrendReversalChanged:
                    TrendReversalChange = signal;
                    break;
            }
        }

        internal FuturesItiSignalDataReadModel ToItiData()
            => new(TrendDirectionChange, TrendExtremeChange, TrendReversalChange);

        internal List<string> GetMissingInputs()
        {
            List<string> missing = [];
            if (Rsi is null) missing.Add("RSI");
            if (Tdi is null) missing.Add("TDI");
            if (TrendDirectionChange is null) missing.Add("ITI direction");
            if (TrendExtremeChange is null) missing.Add("ITI extreme");
            if (TrendReversalChange is null) missing.Add("ITI reversal");
            if (VixFuturesPrice <= 0) missing.Add("VX price");
            return missing;
        }
    }
}
