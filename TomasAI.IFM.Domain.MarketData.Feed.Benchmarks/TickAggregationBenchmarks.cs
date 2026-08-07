using BenchmarkDotNet.Attributes;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Benchmarks;

[MemoryDiagnoser]
public class TickAggregationBenchmarks
{
    private FuturesTickQuoteData[] _buffer = default!;

    [Params(8, 64)]
    public int QuoteCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new FuturesTickQuoteData[64];
        for (var index = 0; index < _buffer.Length; index++)
            _buffer[index] = new FuturesTickQuoteData(
                (uint)index, index, index, 0,
                5_000_000_000 + index, 5m + index / 1_000_000_000m, 10, 1,
                5_100_000_000 + index, 5.1m + index / 1_000_000_000m, 11, 1);
    }

    [Benchmark(Baseline = true, Description = "Before: Take/ToArray then serialize")]
    public byte[] BeforeCopyActivePrefix() =>
        MessagePackSerializer.Serialize(_buffer.Take(QuoteCount).ToArray());

    [Benchmark(Description = "After: serialize active pooled segment")]
    public byte[] AfterActiveSegment() =>
        MessagePackSerializer.Serialize(
            new FuturesTickQuoteDataSegment(_buffer, checked((ushort)QuoteCount)));
}
