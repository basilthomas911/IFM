using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class TradeHistoryOrderingBenchmarks
{
    HistoryRow[] _trades = [];

    [Params(32, 512)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var start = new DateOnly(2026, 1, 1);
        _trades = new HistoryRow[Count];
        for (var index = 0; index < Count; index++)
            _trades[index] = new(start.AddDays(index % 64), (TradeStatus)(index % 4));
    }

    [Benchmark(Baseline = true)]
    public List<HistoryRow> BeforeRepeatedDateScans()
    {
        List<HistoryRow> sorted = [.. _trades.OrderBy(trade => trade.ValueDate)];
        var result = new List<HistoryRow>();
        foreach (var valueDate in sorted.Select(trade => trade.ValueDate).Distinct())
        {
            result.AddRange(sorted.Where(trade => trade.Status == TradeStatus.Open && trade.ValueDate == valueDate));
            result.AddRange(sorted.Where(trade => trade.Status == TradeStatus.IntraDay && trade.ValueDate == valueDate));
            result.AddRange(sorted.Where(trade => trade.Status == TradeStatus.EndOfDay && trade.ValueDate == valueDate));
            result.AddRange(sorted.Where(trade => trade.Status == TradeStatus.Close && trade.ValueDate == valueDate));
        }
        return result;
    }

    [Benchmark]
    public HistoryRow[] AfterSingleOrderingPipeline()
        => [.. _trades.OrderBy(trade => trade.ValueDate).ThenBy(trade => StatusOrder(trade.Status))];

    static int StatusOrder(TradeStatus status)
        => status switch
        {
            TradeStatus.Open => 0,
            TradeStatus.IntraDay => 1,
            TradeStatus.EndOfDay => 2,
            TradeStatus.Close => 3,
            _ => -1
        };

    public readonly record struct HistoryRow(DateOnly ValueDate, TradeStatus Status);
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class SnapshotLegJoinBenchmarks
{
    string[] _legIds = [];
    string[] _positionLegIds = [];

    [Params(32, 512)]
    public int PositionLegCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _legIds = Enumerable.Range(0, 16).Select(index => $"LEG-{index}").ToArray();
        _positionLegIds = Enumerable.Range(0, PositionLegCount).Select(index => _legIds[index % _legIds.Length]).ToArray();
    }

    [Benchmark(Baseline = true)]
    public int BeforeRepeatedSingle()
    {
        var checksum = 0;
        foreach (var positionLegId in _positionLegIds)
            checksum += _legIds.Where(legId => legId == positionLegId).Single().Length;
        return checksum;
    }

    [Benchmark]
    public int AfterIndexedJoin()
    {
        var legById = _legIds.ToDictionary(legId => legId, StringComparer.Ordinal);
        var checksum = 0;
        foreach (var positionLegId in _positionLegIds)
            checksum += legById[positionLegId].Length;
        return checksum;
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class GraphHydrationFanOutBenchmarks
{
    [Params(4, 8)]
    public int OperationCount { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<int> BeforeSequential()
    {
        var checksum = 0;
        for (var index = 0; index < OperationCount; index++)
            checksum += await SimulatedStorageReadAsync(index);
        return checksum;
    }

    [Benchmark]
    public async Task<int> AfterConcurrent()
    {
        var operations = new Task<int>[OperationCount];
        for (var index = 0; index < OperationCount; index++)
            operations[index] = SimulatedStorageReadAsync(index);
        var results = await Task.WhenAll(operations);
        return results.Sum();
    }

    static async Task<int> SimulatedStorageReadAsync(int value)
    {
        await Task.Delay(1).ConfigureAwait(false);
        return value;
    }
}
