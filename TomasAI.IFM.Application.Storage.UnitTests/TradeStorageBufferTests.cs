using System.Buffers.Binary;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using Xunit;

namespace TomasAI.IFM.Application.Storage.UnitTests;

public sealed class TradeStorageBufferTests
{
    static TradeStorageRecord Record(int index, decimal price) => new(
        new((uint)index, 1_795_467_599_000_000_000L + index, 1_795_467_599_000_001_000L + index,
            7, long.MaxValue - index, price, uint.MaxValue, 255, 254, 253), index, DateTime.MaxValue.Ticks,
        Guid.NewGuid(), Guid.NewGuid(), long.MaxValue, "ES-\u00e9", "native-\u03bb", DateTime.MinValue.Ticks, index);

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void Binary_and_CQL_round_trip_every_original_field_with_bounded_arrays(int count)
    {
        var prices = new[] { 0m, 6000.25m, -1.25m, decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m };
        var records = Enumerable.Range(0, count).Select(index => Record(index, prices[index % prices.Length])).ToArray();
        var batch = new TradeStorageBatch(1, Guid.NewGuid(), AssetTypeId.Futures, "ES20261218", new(2026, 11, 23),
            "GLBX.MDP3", new(2026, 11, 23), ushort.MaxValue, uint.MaxValue, Guid.NewGuid(), new(records, count));
        using var buffer = new TradeBatchBinaryBuffer();
        Assert.True(24 + System.Runtime.CompilerServices.Unsafe.SizeOf<TradeStorageRecord>() * TradeStorageSegment.MaximumCount < 85_000);
        MessagePackSerializer.Serialize(buffer, batch);
        Assert.True(buffer.WrittenMemory.Length < 85_000);
        using var decoded = MessagePackSerializer.Deserialize<TradeStorageBatch>(buffer.WrittenMemory);
        Assert.Equal(batch.BatchId, decoded.BatchId);
        Assert.Equal(batch.SourceEpoch, decoded.SourceEpoch);
        Assert.Equal(records, decoded.Records.Items.ToArray());
        using var cql = TickTradeBufferEncoder.EncodePooled(decoded.Records);
        Assert.InRange(cql.Buffer.Length, 1, TickTradeBufferEncoder.MaximumEncodedBytes);
        using var restored = TickTradeBufferEncoder.DecodeList(cql.Buffer, count);
        Assert.Equal(records, restored.Items.ToArray());
        decoded.Dispose(); // Copies and repeated disposal must return a lease once.
    }

    [Fact]
    public void Decoder_rejects_truncated_oversized_and_corrupt_lists()
    {
        var record = Record(1, 6000.25m);
        using var encoded = TickTradeBufferEncoder.EncodePooled(new([record], 1));
        Assert.Throws<InvalidDataException>(() => TickTradeBufferEncoder.DecodeList(encoded.Buffer.AsSpan(0, 8), 1));
        Assert.Throws<InvalidDataException>(() => TickTradeBufferEncoder.DecodeList(encoded.Buffer, 2));
        Assert.Throws<InvalidDataException>(() => TickTradeBufferEncoder.DecodeList(new byte[85_000], 1));
        BinaryPrimitives.WriteInt32BigEndian(encoded.Buffer.AsSpan(4), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => TickTradeBufferEncoder.DecodeList(encoded.Buffer, 1));
    }

    [Fact]
    public void Byte_bounds_are_enforced_before_storage_and_slices_preserve_records()
    {
        var record = Record(1, 1m) with { AggregateId = new string('x', 64_000) };
        Assert.Throws<ArgumentOutOfRangeException>(() => TickTradeBufferEncoder.EncodePooled(new([record], 1)));
        var records = new[] { Record(1, 1m), Record(2, 2m), Record(3, 3m) };
        var segment = new TradeStorageSegment(records, 3);
        Assert.Equal(records[1], Assert.Single(segment.Slice(1, 1).Items.ToArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() => segment.Slice(2, 2));
        using var bytes = new TradeBatchBinaryBuffer();
        Assert.Throws<InvalidDataException>(() => bytes.GetMemory(85_000));
        Assert.Throws<InvalidDataException>(() => bytes.Advance(85_000));
    }
}
