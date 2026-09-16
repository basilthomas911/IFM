using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.TickAggregation;

public sealed class TickAggregationContractTests
{
    [Fact]
    public void Tick_data_entity_identity_round_trips_formatted_contract_and_value_date()
    {
        var expected = new TickDataEntityId(
            "ES:U6/TEST",
            new DateOnly(2026, 9, 2),
            AssetTypeId.Futures);

        var result = TickDataEntityId.Parse(expected.Format());

        Assert.Equal(expected, result);
        Assert.False(TickDataEntityId.TryParse("1:20260230:ESU6", out _));
        Assert.False(TickDataEntityId.TryParse("0:20260902:ESU6", out _));
    }

    [Fact]
    public void Quote_segment_serializes_only_active_prefix()
    {
        var buffer = new FuturesTickQuoteData[64];
        buffer[0] = new FuturesTickQuoteData(1, 2, 3, 4, 5, 0.000000005m, 6, 7, 8, 0.000000008m, 9, 10);
        buffer[1] = buffer[0] with { SourceSequence = 2 };
        buffer[2] = buffer[0] with { SourceSequence = 999 };

        var bytes = MessagePackSerializer.Serialize(new FuturesTickQuoteDataSegment(buffer, 2));
        var roundTrip = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(bytes);

        try
        {
            Assert.Equal((ushort)2, roundTrip.Count);
            Assert.True(roundTrip.Buffer.Length >= roundTrip.Count);
            Assert.Equal((uint)2, roundTrip.Buffer[1].SourceSequence);
        }
        finally { roundTrip.Dispose(); }
    }

    [Fact]
    public void Maximum_quote_segment_round_trips_without_losing_order_or_count()
    {
        var buffer = new FuturesTickQuoteData[FuturesTickQuoteDataSegment.MaximumCount];
        for (var index = 0; index < buffer.Length; index++)
            buffer[index] = new FuturesTickQuoteData(
                (uint)(index + 1), index + 1, index + 2, 0,
                5_000_000_000L + index, index % 2 == 0 ? null : 5m,
                (uint)(index + 10), 1, 5_100_000_000L + index,
                index % 2 == 0 ? 5.1m : null, (uint)(index + 11), 1);

        var payload = MessagePackSerializer.Serialize(
            new FuturesTickQuoteDataSegment(buffer, FuturesTickQuoteDataSegment.MaximumCount));
        var decoded = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(payload);

        try
        {
            Assert.Equal(FuturesTickQuoteDataSegment.MaximumCount, decoded.Count);
            Assert.Equal(buffer.Length, decoded.Buffer.Length);
            Assert.Equal(buffer[0], decoded.Buffer[0]);
            Assert.Equal(buffer[^1], decoded.Buffer[^1]);
        }
        finally { decoded.Dispose(); }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FuturesTickQuoteDataSegment(
                new FuturesTickQuoteData[FuturesTickQuoteDataSegment.MaximumCount + 1],
                (ushort)(FuturesTickQuoteDataSegment.MaximumCount + 1)));
    }

    [Fact]
    public void Decoded_quote_slot_is_reused_after_the_consumer_disposes_it()
    {
        PooledQuoteSegmentBuffers.Warmup();
        var quote = new FuturesTickQuoteData(
            1, 2, 3, 4, 5_000_000_000, 5m, 6, 7,
            5_100_000_000, 5.1m, 8, 9);
        var payload = MessagePackSerializer.Serialize(
            new FuturesTickQuoteDataSegment([quote], 1));

        var first = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(payload);
        var buffer = first.Buffer;
        Assert.Equal(512, buffer.Length);
        first.Dispose();
        first.Dispose();

        var second = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(payload);
        try
        {
            Assert.Same(buffer, second.Buffer);
            Assert.Equal(quote, second.Buffer[0]);
        }
        finally { second.Dispose(); }
    }

    [Fact]
    public void Malformed_quote_segment_returns_its_decoder_slot()
    {
        PooledQuoteSegmentBuffers.Warmup();
        var quote = new FuturesTickQuoteData(
            1, 2, 3, 4, 5_000_000_000, 5m, 6, 7,
            5_100_000_000, 5.1m, 8, 9);
        var payload = MessagePackSerializer.Serialize(
            new FuturesTickQuoteDataSegment([quote, quote], 2));

        var first = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(payload);
        var buffer = first.Buffer;
        first.Dispose();

        Assert.Throws<MessagePackSerializationException>(() =>
            MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(payload[..^1]));

        var next = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(payload);
        try { Assert.Same(buffer, next.Buffer); }
        finally { next.Dispose(); }
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
