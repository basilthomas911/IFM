using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

public enum AssetTypeId : byte
{
    Unknown = 0,
    Futures = 1,
    FuturesOption = 2,
    Equity = 3,
    EquityOption = 4
}

public enum QuoteEmissionReason : byte
{
    BufferFull = 1,
    TradeObserved = 2,
    FeedStopped = 3,
    TickerRemoved = 4,
    FeedFaulted = 5,
    ValueDateChanged = 6
}

[MessagePackObject]
public readonly record struct TickDataEntityId(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] AssetTypeId AssetTypeId) : IActorEntityId
{
    public string Format() => string.Create(
        CultureInfo.InvariantCulture,
        $"{(byte)AssetTypeId}:{ValueDate:yyyyMMdd}:{Uri.EscapeDataString(ContractId)}");
}

[MessagePackObject]
public readonly record struct TickDataId(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] long SequenceId,
    [property: Key(3)] DateTime TimestampUtc);

[MessagePackObject]
public readonly record struct FuturesTickQuoteData(
    [property: Key(0)] uint SourceSequence,
    [property: Key(1)] long EventTimestampNanoseconds,
    [property: Key(2)] long ReceiveTimestampNanoseconds,
    [property: Key(3)] byte HeaderFlags,
    [property: Key(4)] long BidPriceRaw,
    [property: Key(5)] decimal? BidPrice,
    [property: Key(6)] uint BidSize,
    [property: Key(7)] uint BidCount,
    [property: Key(8)] long AskPriceRaw,
    [property: Key(9)] decimal? AskPrice,
    [property: Key(10)] uint AskSize,
    [property: Key(11)] uint AskCount);

[MessagePackObject]
public readonly record struct FuturesTickTradeData(
    [property: Key(0)] uint SourceSequence,
    [property: Key(1)] long EventTimestampNanoseconds,
    [property: Key(2)] long ReceiveTimestampNanoseconds,
    [property: Key(3)] byte HeaderFlags,
    [property: Key(4)] long PriceRaw,
    [property: Key(5)] decimal Price,
    [property: Key(6)] uint Size,
    [property: Key(7)] byte Action,
    [property: Key(8)] byte Side,
    [property: Key(9)] byte DbnFlags);

[MessagePackFormatter(typeof(FuturesTickQuoteDataSegmentFormatter))]
public readonly struct FuturesTickQuoteDataSegment
{
    public const ushort MaximumCount = 64;

    public FuturesTickQuoteDataSegment(FuturesTickQuoteData[] buffer, ushort count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count is 0 or > MaximumCount || count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        Buffer = buffer;
        Count = count;
    }

    public FuturesTickQuoteData[] Buffer { get; }
    public ushort Count { get; }
    public ReadOnlySpan<FuturesTickQuoteData> Items => Buffer.AsSpan(0, Count);
}

public sealed class FuturesTickQuoteDataSegmentFormatter
    : IMessagePackFormatter<FuturesTickQuoteDataSegment>
{
    public void Serialize(
        ref MessagePackWriter writer,
        FuturesTickQuoteDataSegment value,
        MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(value.Count);
        var formatter = options.Resolver.GetFormatterWithVerify<FuturesTickQuoteData>();
        foreach (ref readonly var item in value.Items)
            formatter.Serialize(ref writer, item, options);
    }

    public FuturesTickQuoteDataSegment Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
            throw new MessagePackSerializationException($"Invalid quote segment length {count}.");
        var buffer = new FuturesTickQuoteData[count];
        var formatter = options.Resolver.GetFormatterWithVerify<FuturesTickQuoteData>();
        for (var index = 0; index < count; index++)
            buffer[index] = formatter.Deserialize(ref reader, options);
        return new FuturesTickQuoteDataSegment(buffer, checked((ushort)count));
    }
}
