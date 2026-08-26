using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

/// <summary>
/// Exposes the readonly Market Outlook Snapshot context and publishes component
/// changes from closed-generic source actor contexts.
/// </summary>
public static class MarketOutlookSnapshotRealtimeContextExtensions
{
    extension(IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IMarketOutlookSnapshotRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IMarketOutlookSnapshotRealtimeContext, nameof(context))!;

        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;

        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<MarketOutlookSnapshotRealtimeActor> Logger => context.DomainContext.Logger;
    }

    /// <summary>Publishes an RSI component change from its closed-generic source actor context.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesRsiSignalGeneratedCompleteEvent source)
        where TActor : IActor =>
        PublishMarketOutlookComponentAsync(
            context,
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            rsi: source.FuturesRsiSignal);

    /// <summary>Publishes a TDI component change from its closed-generic source actor context.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesTdiSignalGeneratedCompleteEvent source)
        where TActor : IActor =>
        PublishMarketOutlookComponentAsync(
            context,
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            tdi: source.FuturesTdiSignal);

    /// <summary>Publishes an ITI component change from its closed-generic source actor context.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesItiSignalGeneratedCompleteEvent source)
        where TActor : IActor =>
        PublishMarketOutlookComponentAsync(
            context,
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            iti: source.FuturesItiSignal,
            vixFuturesPrice: Convert.ToDecimal(source.VixFuturesPrice));

    static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        IEventActorContext<TActor> context,
        string contractId,
        DateOnly valueDate,
        Guid commandId,
        string? aggregateId,
        string eventSource,
        FuturesRsiSignalReadModel? rsi = null,
        FuturesTdiSignalReadModel? tdi = null,
        FuturesItiSignalV2ReadModel? iti = null,
        decimal vixFuturesPrice = 0)
        where TActor : IActor
    {
        ArgumentNullException.ThrowIfNull(context);

        var entityId = new MarketOutlookEntityId(contractId, valueDate);
        var changed = new MarketOutlookComponentChangedRealtimeEvent
        {
            Subject = new ActorSubject(
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
            VixFuturesPrice = vixFuturesPrice
        };
        return context.SendAsync<MarketOutlookComponentChangedRealtimeEvent, MarketOutlookEntityId>(changed);
    }
}
