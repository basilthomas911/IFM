using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class PositionLookupBenchmarks
{
    TradePositionReadModel[] _positions = [];
    readonly DateOnly _targetDate = new(2026, 8, 5);

    [Params(32, 512)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _positions = new TradePositionReadModel[Count];
        for (var index = 0; index < Count; index++)
        {
            _positions[index] = new TradePositionReadModel
            {
                OrderId = 1,
                TradeId = 2,
                TradeType = index % 3 == 0 ? TradeType.PutCreditSpread : TradeType.CallCreditSpread,
                TradeStatus = index % 2 == 0 ? TradeStatus.IntraDay : TradeStatus.EndOfDay,
                ValueDate = _targetDate.AddDays((index % 31) - 15),
                DaysToExpiry = 30
            };
        }
    }

    [Benchmark(Baseline = true)]
    public TradePositionReadModel? BeforeLatest()
        => _positions
            .OrderBy(position => position.EntityId.ValueDate)
            .Where(position => position.EntityId.TradeType == TradeType.PutCreditSpread
                && position.EntityId.TradeStatus == TradeStatus.IntraDay)
            .LastOrDefault();

    [Benchmark]
    public TradePositionReadModel? AfterLatest()
        => _positions.Get(TradeType.PutCreditSpread, TradeStatus.IntraDay);
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class PositionDateLookupBenchmarks
{
    TradePositionReadModel[] _positions = [];
    readonly DateOnly _targetDate = new(2026, 8, 5);

    [Params(32, 512)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _positions = new TradePositionReadModel[Count];
        for (var index = 0; index < Count; index++)
        {
            _positions[index] = new TradePositionReadModel
            {
                OrderId = 1,
                TradeId = 2,
                TradeType = TradeType.PutCreditSpread,
                TradeStatus = TradeStatus.IntraDay,
                ValueDate = index == Count - 1 ? _targetDate : _targetDate.AddDays(-1),
                DaysToExpiry = 30
            };
        }
    }

    [Benchmark(Baseline = true)]
    public TradePositionReadModel? BeforeFormattedDate()
        => _positions
            .Where(position => position.EntityId.TradeType == TradeType.PutCreditSpread
                && position.EntityId.TradeStatus == TradeStatus.IntraDay
                && $"{position.EntityId.ValueDate:yyyyMMdd}" == $"{_targetDate:yyyyMMdd}")
            .LastOrDefault();

    [Benchmark]
    public TradePositionReadModel? AfterDirectDateReverseScan()
        => _positions.Get(TradeType.PutCreditSpread, TradeStatus.IntraDay, _targetDate);
}
