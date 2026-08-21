using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot;

internal static class MarketOutlookComponentPublisher
{
    internal static ValueTask PublishAsync(
        this FuturesRsiSignalGeneratedCompleteEvent source,
        IEventActorContext context)
        => PublishAsync(source.EntityId.ContractId, source.EntityId.ValueDate, source.CommandId,
            source.AggregateId, source.EventName, context, rsi: source.FuturesRsiSignal);

    internal static ValueTask PublishAsync(
        this FuturesTdiSignalGeneratedCompleteEvent source,
        IEventActorContext context)
        => PublishAsync(source.EntityId.ContractId, source.EntityId.ValueDate, source.CommandId,
            source.AggregateId, source.EventName, context, tdi: source.FuturesTdiSignal);

    internal static ValueTask PublishAsync(
        this FuturesItiSignalGeneratedCompleteEvent source,
        IEventActorContext context)
        => PublishAsync(source.EntityId.ContractId, source.EntityId.ValueDate, source.CommandId,
            source.AggregateId, source.EventName, context, iti: source.FuturesItiSignal,
            vixFuturesPrice: Convert.ToDecimal(source.VixFuturesPrice));

    static ValueTask PublishAsync(
        string contractId,
        DateOnly valueDate,
        Guid commandId,
        string? aggregateId,
        string eventSource,
        IEventActorContext context,
        FuturesRsiSignalReadModel? rsi = null,
        FuturesTdiSignalReadModel? tdi = null,
        FuturesItiSignalV2ReadModel? iti = null,
        decimal vixFuturesPrice = 0)
    {
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
