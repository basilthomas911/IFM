using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

internal static class TickAggregationEventFactory
{
    public static TickAggregationCompleteEvent Complete(IEvent<TickDataEntityId> source, ushort count, string verb)
    {
        var tick = source switch
        {
            FuturesTickTradeDataInsertedEvent trade => (trade.SchemaVersion, trade.TickDataId, trade.AssetTypeId),
            FuturesTickQuoteDataInsertedEvent quote => (quote.SchemaVersion, quote.TickDataId, quote.AssetTypeId),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        TickAggregationCompleteEvent result = string.Equals(verb,FuturesTickTradeDataInsertedCompleteEvent.Verb,StringComparison.Ordinal)
            ? new FuturesTickTradeDataInsertedCompleteEvent()
            : new FuturesTickQuoteDataInsertedCompleteEvent();
        return result with
        {
            Subject = new ActorSubject(ActorType.Realtime, TickAggregationCompleteEvent.Actor, verb, source.EntityId.Format()),
            EntityId = source.EntityId,
            Id = source.Id,
            EventId = source.EventId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = source.EventSource,
            ReceivedOn = source.ReceivedOn,
            SchemaVersion = tick.SchemaVersion,
            TickDataId = tick.TickDataId,
            AssetTypeId = tick.AssetTypeId,
            PersistedRecordCount = count
        };
    }

    public static TickAggregationFailEvent Fail(IEvent<TickDataEntityId> source, Exception ex, ushort count, string verb, int errorCode)
    {
        var tick = source switch
        {
            FuturesTickTradeDataInsertedEvent trade => (trade.SchemaVersion, trade.TickDataId, trade.AssetTypeId),
            FuturesTickQuoteDataInsertedEvent quote => (quote.SchemaVersion, quote.TickDataId, quote.AssetTypeId),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        TickAggregationFailEvent result = string.Equals(verb,FuturesTickTradeDataInsertedFailEvent.Verb,StringComparison.Ordinal)
            ? new FuturesTickTradeDataInsertedFailEvent()
            : new FuturesTickQuoteDataInsertedFailEvent();
        return result with
        {
            Subject = new ActorSubject(ActorType.Realtime, TickAggregationFailEvent.Actor, verb, source.EntityId.Format()),
            EntityId = source.EntityId,
            Id = source.Id,
            ErrorDate = DateTime.UtcNow,
            EventId = source.EventId,
            CommandId = source.CommandId,
            EventSource = source.EventSource,
            ErrorMessage = ex.Message,
            ErrorCode = errorCode,
            ErrorType = ErrorType.EventService,
            ErrorData = ex.GetType().Name,
            ReceivedOn = source.ReceivedOn,
            AggregateId = source.AggregateId,
            SchemaVersion = tick.SchemaVersion,
            TickDataId = tick.TickDataId,
            AssetTypeId = tick.AssetTypeId,
            AttemptedRecordCount = count
        };
    }
}
