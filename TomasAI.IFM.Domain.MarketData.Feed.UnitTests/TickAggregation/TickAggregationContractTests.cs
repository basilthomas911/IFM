using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.TickAggregation;

public sealed class TickAggregationContractTests
{
    [Fact]
    public void Quote_segment_serializes_only_active_prefix()
    {
        var buffer = new FuturesTickQuoteData[64];
        buffer[0] = new FuturesTickQuoteData(1, 2, 3, 4, 5, 0.000000005m, 6, 7, 8, 0.000000008m, 9, 10);
        buffer[1] = buffer[0] with { SourceSequence = 2 };
        buffer[2] = buffer[0] with { SourceSequence = 999 };

        var bytes = MessagePackSerializer.Serialize(new FuturesTickQuoteDataSegment(buffer, 2));
        var roundTrip = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(bytes);

        Assert.Equal((ushort)2, roundTrip.Count);
        Assert.Equal(2, roundTrip.Buffer.Length);
        Assert.Equal((uint)2, roundTrip.Buffer[1].SourceSequence);
    }

    [Fact]
    public void Inserted_event_creates_exact_concrete_completion_type()
    {
        var entity = new TickDataEntityId("ESU6", new DateOnly(2026, 8, 7), AssetTypeId.Futures);
        var inserted = new FuturesTickTradeDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickTradeDataInsertedEvent.Actor, FuturesTickTradeDataInsertedEvent.Verb, entity.Format()),
            EntityId = entity, Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
            TickDataId = new TickDataId("ESU6", entity.ValueDate, 1, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)),
            AssetTypeId = AssetTypeId.Futures
        };

        var complete = inserted.ToCompleteEvent<FuturesTickTradeDataInsertedCompleteEvent, TickDataEntityId>();

        Assert.IsType<FuturesTickTradeDataInsertedCompleteEvent>(complete);
        Assert.Equal((ushort)1, ((FuturesTickTradeDataInsertedCompleteEvent)complete).PersistedRecordCount);
    }

    [Fact]
    public void Concrete_completion_and_failure_events_round_trip()
    {
        var entity = new TickDataEntityId("ESU6", new DateOnly(2026, 8, 7), AssetTypeId.Futures);
        var tickId = new TickDataId("ESU6", entity.ValueDate, 7, new DateTime(2026, 8, 7, 20, 0, 0, DateTimeKind.Utc));
        var complete = new FuturesTickTradeDataInsertedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, TickAggregationCompleteEvent.Actor,
                FuturesTickTradeDataInsertedCompleteEvent.Verb, entity.Format()),
            EntityId = entity, TickDataId = tickId, AssetTypeId = AssetTypeId.Futures,
            PersistedRecordCount = 1
        };
        var failed = new FuturesTickQuoteDataInsertedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, TickAggregationFailEvent.Actor,
                FuturesTickQuoteDataInsertedFailEvent.Verb, entity.Format()),
            EntityId = entity, TickDataId = tickId, AssetTypeId = AssetTypeId.Futures,
            AttemptedRecordCount = 8, ErrorMessage = "failed"
        };

        var completeResult = MessagePackSerializer.Deserialize<FuturesTickTradeDataInsertedCompleteEvent>(
            MessagePackSerializer.Serialize(complete));
        var failedResult = MessagePackSerializer.Deserialize<FuturesTickQuoteDataInsertedFailEvent>(
            MessagePackSerializer.Serialize(failed));

        Assert.Equal(tickId, completeResult.TickDataId);
        Assert.Equal((ushort)8, failedResult.AttemptedRecordCount);
    }
}
