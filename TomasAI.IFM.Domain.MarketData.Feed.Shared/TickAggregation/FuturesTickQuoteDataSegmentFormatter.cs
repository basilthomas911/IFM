using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

public sealed class FuturesTickQuoteDataSegmentFormatter
    : IMessagePackFormatter<FuturesTickQuoteDataSegment>
{
    public void Serialize(
        ref MessagePackWriter writer,
        FuturesTickQuoteDataSegment value,
        MessagePackSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        writer.WriteArrayHeader(value.Count);
        var formatter = options.Resolver.GetFormatterWithVerify<FuturesTickQuoteData>();
        foreach (ref readonly var item in value.Items)
            formatter.Serialize(ref writer, item, options);
    }

    public FuturesTickQuoteDataSegment Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var count = reader.ReadArrayHeader();
        if (count is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
            throw new MessagePackSerializationException($"Invalid quote segment length {count}.");
        var owner = PooledQuoteSegmentBuffers.Rent(count);
        var buffer = owner?.Buffer ?? new FuturesTickQuoteData[count];
        var formatter = options.Resolver.GetFormatterWithVerify<FuturesTickQuoteData>();
        try
        {
            for (var index = 0; index < count; index++)
                buffer[index] = formatter.Deserialize(ref reader, options);
            return new FuturesTickQuoteDataSegment(buffer, checked((ushort)count), owner);
        }
        catch
        {
            owner?.Dispose();
            throw;
        }
    }
}
