using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

/// <summary>Publishes eligible source-operator results to the versionless Market Outlook cache owner.</summary>
public static class MarketOutlookSnapshotRealtimeContextExtensions
{
    extension(IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        public IMarketOutlookSnapshotRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IMarketOutlookSnapshotRealtimeContext, nameof(context))!;
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        public ILogger<MarketOutlookSnapshotRealtimeActor> Logger => context.DomainContext.Logger;
    }

    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesRsiSignalGeneratedCompleteEvent source)
        where TActor : IActor => Publish(
            context, source.EntityId.ContractId, source.EntityId.ValueDate, source.CommandId,
            source.AggregateId, source.EventName, rsi: source.FuturesRsiSignal);

    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesTdiSignalGeneratedCompleteEvent source)
        where TActor : IActor => Publish(
            context, source.EntityId.ContractId, source.EntityId.ValueDate, source.CommandId,
            source.AggregateId, source.EventName, tdi: source.FuturesTdiSignal);

    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesItiSignalGeneratedCompleteEvent source)
        where TActor : IActor => Publish(
            context, source.EntityId.ContractId, source.EntityId.ValueDate, source.CommandId,
            source.AggregateId, source.EventName, iti: source.FuturesItiSignal,
            vixFuturesPrice: Convert.ToDecimal(source.VixFuturesPrice));

    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesEmaSignalGeneratedCompleteEvent source)
        where TActor : IActor => IsEsSeries(source.Signal.Metadata.SignalKey.MarketSeriesIdentity)
            ? Publish(context, source.Signal.Metadata.ContractId, source.Signal.Metadata.ValueDate,
                source.CommandId, source.AggregateId, source.EventName, ema: source.Signal)
            : ValueTask.CompletedTask;

    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesBbSignalGeneratedCompleteEvent source)
        where TActor : IActor => IsEsSeries(source.Signal.Metadata.SignalKey.MarketSeriesIdentity)
            ? Publish(context, source.Signal.Metadata.ContractId, source.Signal.Metadata.ValueDate,
                source.CommandId, source.AggregateId, source.EventName, bb: source.Signal)
            : ValueTask.CompletedTask;

    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesTradeSignalUpdatedCompleteEvent source)
        where TActor : IActor => source.FuturesTradeSignal is { } signal
            ? Publish(context, source.EntityId.ContractId, source.EntityId.ValueDate,
                source.CommandId, source.AggregateId, source.EventName, tradeSignal: signal)
            : ValueTask.CompletedTask;

    static ValueTask Publish<TActor>(
        IEventActorContext<TActor> context,
        string contractId,
        DateOnly valueDate,
        Guid commandId,
        string? aggregateId,
        string eventSource,
        FuturesRsiSignalReadModel? rsi = null,
        FuturesTdiSignalReadModel? tdi = null,
        FuturesItiSignalV2ReadModel? iti = null,
        decimal vixFuturesPrice = 0,
        FuturesEmaSignalReadModel? ema = null,
        FuturesBbSignalReadModel? bb = null,
        FuturesTradeSignalV2ReadModel? tradeSignal = null)
        where TActor : IActor
    {
        ArgumentNullException.ThrowIfNull(context);
        var entityId = new MarketOutlookEntityId(contractId, valueDate);
        var changed = new MarketOutlookComponentChangedRealtimeEvent
        {
            Subject = new(
                ActorType.Realtime,
                MarketOutlookComponentChangedRealtimeEvent.Actor,
                MarketOutlookComponentChangedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = commandId == Guid.Empty ? Guid.NewGuid() : commandId,
            AggregateId = aggregateId ?? string.Empty,
            EventSource = eventSource,
            ReceivedOn = DateTime.UtcNow,
            FuturesRsiSignal = rsi,
            FuturesTdiSignal = tdi,
            FuturesItiSignal = iti,
            VixFuturesPrice = vixFuturesPrice,
            FuturesEmaSignal = ema,
            FuturesBbSignal = bb,
            FuturesTradeSignal = tradeSignal
        };
        var eligible = MarketOutlookComponentEligibility.SelectEligible(changed, out _);
        return MarketOutlookComponentEligibility.IsEligible(eligible, out _)
            ? context.SendAsync<MarketOutlookComponentChangedRealtimeEvent, MarketOutlookEntityId>(eligible)
            : ValueTask.CompletedTask;
    }

    static bool IsEsSeries(MarketSeriesIdentity series) =>
        series.FuturesSeriesId is { } continuation
            ? string.Equals(continuation.RootSymbol, "ES", StringComparison.OrdinalIgnoreCase)
            : series.Kind == MarketSeriesIdentityKind.Contract
              && series.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase);
}
