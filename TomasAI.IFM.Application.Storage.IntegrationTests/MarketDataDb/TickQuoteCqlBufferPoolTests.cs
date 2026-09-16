using System;
using System.Linq;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class TickQuoteCqlBufferPoolTests
{
    [Fact]
    public void Encoded_value_uses_a_reusable_exact_length_buffer()
    {
        var quote = new FuturesTickQuoteData(
            1, 2, 3, 4, 5_000_000_000, 5m, 6, 7,
            5_100_000_000, 5.1m, 8, 9);
        var segment = new FuturesTickQuoteDataSegment(
            Enumerable.Repeat(quote, 512).ToArray(), 512);

        var first = TickQuoteCqlEncoder.EncodePooled(segment);
        var encoded = first.Buffer;
        var expected = TickQuoteCqlEncoder.Encode(segment);
        Assert.Equal(expected, encoded);
        first.Dispose();

        using var second = TickQuoteCqlEncoder.EncodePooled(segment);
        Assert.Same(encoded, second.Buffer);
        Assert.Equal(expected, second.Buffer);
    }

    [Fact]
    public void Maximum_512_quote_value_stays_below_the_large_object_heap_threshold()
    {
        var quote = new FuturesTickQuoteData(
            1, 2, 3, 4, 5, decimal.MaxValue, 6, 7,
            8, decimal.MaxValue, 9, 10);
        var segment = new FuturesTickQuoteDataSegment(
            Enumerable.Repeat(quote, 512).ToArray(), 512);

        using var encoded = TickQuoteCqlEncoder.EncodePooled(segment);
        Assert.True(encoded.Buffer.Length + 24 < 85_000,
            $"512 quotes encoded to {encoded.Buffer.Length + 24} bytes including the array header.");
    }
}
