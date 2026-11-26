using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

public sealed partial class FuturesTradeSessionBarAccumulator
{
    internal List<FuturesTradeSessionBarReadModel> PendingPublications { get; } = [];
    internal bool HasRecoveryWork => buckets.Count != 0 || PendingPublications.Count != 0;

    // This is a private, versioned recovery format, independent of the market-event wire format.
    internal void WriteCheckpoint(BinaryWriter writer)
    {
        if (contracts.Count > 16384 || finalizedThrough.Count > 16384 || streams.Count > 16384
            || buckets.Count > 16384 || PendingPublications.Count > 16384
            || streams.Values.Any(stream => stream.Pending.Count > 16384 || stream.RetiredEpochs.Count > 16384))
            throw new InvalidDataException("Bar checkpoint collection exceeds its recovery bound.");
        writer.Write(1);
        writer.Write(contracts.Count);
        foreach (var (series, contract) in contracts) { writer.Write(series.Format()); writer.Write(contract); }
        writer.Write(finalizedThrough.Count);
        foreach (var (key, end) in finalizedThrough)
        { writer.Write(key.SeriesIdentity.Format()); writer.Write((int)key.TimeFrame); WriteTime(writer, end); }
        writer.Write(streams.Count);
        foreach (var (contract, stream) in streams)
        {
            writer.Write(contract); writer.Write(stream.Series.Format()); WriteGuid(writer, stream.EpochId);
            writer.Write(stream.Ordinal); WriteTime(writer, stream.LatestTradeUtc); WriteTime(writer, stream.IncompleteThrough);
            writer.Write(stream.GapDeadline.HasValue);
            if (stream.GapDeadline is { } deadline) WriteTime(writer, deadline);
            writer.Write(stream.RetiredEpochs.Count);
            foreach (var epoch in stream.RetiredEpochs) WriteGuid(writer, epoch);
            writer.Write(stream.Pending.Count);
            foreach (var trade in stream.Pending.Values) WriteTrade(writer, trade);
        }
        writer.Write(buckets.Count);
        foreach (var (key, bucket) in buckets)
        { writer.Write(key.SeriesIdentity.Format()); writer.Write((int)key.TimeFrame); bucket.Write(writer); }
        writer.Write(PendingPublications.Count);
        foreach (var bar in PendingPublications)
        {
            // Closed observations use their existing generated MessagePack contract, including original timestamps.
            var bytes = MessagePackSerializer.Serialize(bar);
            if (bytes.Length > 65536) throw new InvalidDataException("Pending bar exceeds its recovery frame bound.");
            writer.Write(bytes.Length); writer.Write(bytes);
        }
    }

    internal void ReadCheckpoint(BinaryReader reader)
    {
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported bar checkpoint version.");
        for (int i = 0, count = ReadCount(reader); i < count; i++)
            contracts.Add(MarketSeriesIdentity.Parse(reader.ReadString()), reader.ReadString());
        for (int i = 0, count = ReadCount(reader); i < count; i++)
            finalizedThrough.Add(new(MarketSeriesIdentity.Parse(reader.ReadString()), (TimeFrameType)reader.ReadInt32()), ReadTime(reader));
        for (int i = 0, count = ReadCount(reader); i < count; i++)
        {
            var contract = reader.ReadString();
            var stream = new TradeStream(MarketSeriesIdentity.Parse(reader.ReadString()))
            {
                EpochId = ReadGuid(reader), Ordinal = reader.ReadInt64(), LatestTradeUtc = ReadTime(reader),
                IncompleteThrough = ReadTime(reader), GapDeadline = reader.ReadBoolean() ? ReadTime(reader) : null
            };
            for (int j = 0, epochs = ReadCount(reader); j < epochs; j++) stream.RetiredEpochs.Add(ReadGuid(reader));
            for (int j = 0, pending = ReadCount(reader); j < pending; j++)
            { var trade = ReadTrade(reader); stream.Pending.Add(trade.TradeOrdinal, trade); }
            streams.Add(contract, stream);
        }
        for (int i = 0, count = ReadCount(reader); i < count; i++)
        {
            var key = new BucketKey(MarketSeriesIdentity.Parse(reader.ReadString()), (TimeFrameType)reader.ReadInt32());
            buckets.Add(key, ObservationBucket.Read(reader, key));
        }
        for (int i = 0, count = ReadCount(reader); i < count; i++)
        {
            var length = reader.ReadInt32();
            if (length is < 1 or > 65536) throw new InvalidDataException("Invalid pending-bar frame size.");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            PendingPublications.Add(MessagePackSerializer.Deserialize<FuturesTradeSessionBarReadModel>(bytes));
        }
    }

    static int ReadCount(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        return count is >= 0 and <= 16384 ? count : throw new InvalidDataException("Invalid bar checkpoint collection size.");
    }
    static void WriteTime(BinaryWriter writer, DateTimeOffset time)
    { writer.Write(time.Ticks); writer.Write((short)time.Offset.TotalMinutes); }
    static DateTimeOffset ReadTime(BinaryReader reader) => new(reader.ReadInt64(), TimeSpan.FromMinutes(reader.ReadInt16()));
    static void WriteGuid(BinaryWriter writer, Guid value)
    { Span<byte> bytes = stackalloc byte[16]; value.TryWriteBytes(bytes); writer.Write(bytes); }
    static Guid ReadGuid(BinaryReader reader)
    { Span<byte> bytes = stackalloc byte[16]; reader.BaseStream.ReadExactly(bytes); return new(bytes); }
    static void WriteTrade(BinaryWriter writer, FuturesMarketTradeSnapshot trade)
    {
        writer.Write(trade.LastPrice); writer.Write(trade.LastSize); writer.Write(trade.SourceSequence);
        WriteTime(writer, trade.EventTimestamp); WriteTime(writer, trade.ReceiveTimestamp);
        writer.Write((int)trade.NormalizedTradeAction); writer.Write((int)trade.NormalizedTradeSide);
        writer.Write((ushort)trade.NormalizedTradeConditionFlags); WriteGuid(writer, trade.StreamEpochId); writer.Write(trade.TradeOrdinal);
    }
    static FuturesMarketTradeSnapshot ReadTrade(BinaryReader reader) => new(
        reader.ReadDecimal(), reader.ReadUInt32(), reader.ReadInt64(), ReadTime(reader), ReadTime(reader),
        (NormalizedTradeAction)reader.ReadInt32(), (NormalizedTradeSide)reader.ReadInt32(),
        (NormalizedTradeConditionFlags)reader.ReadUInt16(), ReadGuid(reader), reader.ReadInt64());

    sealed partial class ObservationBucket
    {
        internal void Write(BinaryWriter writer)
        {
            writer.Write(contractId); writer.Write(valueDate.DayNumber); WriteTime(writer, startUtc); WriteTime(writer, endUtc); WriteGuid(writer, streamEpochId);
            writer.Write(open); writer.Write(high); writer.Write(low); writer.Write(close); writer.Write(volume); writer.Write(tradeCount);
            writer.Write(priceVolumeSum); writer.Write(firstSequence); writer.Write(lastSequence); WriteTime(writer, firstEvent); WriteTime(writer, lastEvent);
        }
        internal static ObservationBucket Read(BinaryReader reader, BucketKey key) => new(
            key.SeriesIdentity, reader.ReadString(), DateOnly.FromDayNumber(reader.ReadInt32()), key.TimeFrame,
            ReadTime(reader), ReadTime(reader), ReadGuid(reader))
        {
            open = reader.ReadDecimal(), high = reader.ReadDecimal(), low = reader.ReadDecimal(), close = reader.ReadDecimal(),
            volume = reader.ReadDecimal(), tradeCount = reader.ReadInt64(), priceVolumeSum = reader.ReadDecimal(),
            firstSequence = reader.ReadInt64(), lastSequence = reader.ReadInt64(), firstEvent = ReadTime(reader), lastEvent = ReadTime(reader)
        };
    }
}
