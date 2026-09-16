using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the MessagePack quote-segment ingress allocation left after CQL encoding.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class TickQuoteIngressBenchmarks
{
    private byte[] _payload = null!;

    [Params(64, 512, 4096)]
    public int QuoteCount { get; set; }

    /// <summary>Builds one serialized segment outside the measured deserialization loop.</summary>
    [GlobalSetup]
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
        _payload = MessagePackSerializer.Serialize(
            new FuturesTickQuoteDataSegment(quotes, (ushort)quotes.Length));
    }

    /// <summary>Decodes and returns the quote-segment buffer as NATS ingress does.</summary>
    [Benchmark]
    public ushort SegmentDeserialize()
    {
        var decoded = MessagePackSerializer.Deserialize<FuturesTickQuoteDataSegment>(_payload);
        try { return decoded.Count; }
        finally { decoded.Dispose(); }
    }
}
