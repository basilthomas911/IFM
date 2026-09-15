using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the application-owned native CQL quote encoding before binding.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class TickQuoteConversionBenchmarks
{
    private FuturesTickQuoteDataSegment _segment;

    [Params(1, 32, 64)]
    public int QuoteCount { get; set; }

    [GlobalSetup]
    /// <summary>Creates a bounded quote segment for the selected batch size.</summary>
    public void Setup()
    {
        var quotes = new FuturesTickQuoteData[QuoteCount];
        for (var index = 0; index < quotes.Length; index++)
            quotes[index] = new FuturesTickQuoteData(
                (uint)index, 1_000_000_000L + index, 2_000_000_000L + index,
                (byte)(index % 8), 50_000_000_000L + index,
                index % 2 == 0 ? 5.25m + index / 100m : null,
                (uint)(100 + index), (uint)(10 + index), 51_000_000_000L + index,
                index % 2 == 0 ? null : 5.35m + index / 100m,
                (uint)(200 + index), (uint)(20 + index));
        _segment = new FuturesTickQuoteDataSegment(quotes, (ushort)quotes.Length);
    }

    [Benchmark]
    /// <summary>Encodes the native nested UDT list into one byte array.</summary>
    public byte[] OneBufferCqlEncoding()
        => TickQuoteCqlEncoder.Encode(_segment);
}
