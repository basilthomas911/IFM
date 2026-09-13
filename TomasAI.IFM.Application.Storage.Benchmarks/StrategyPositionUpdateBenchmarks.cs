using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the actor-local calculation performed before the asynchronous durability stage.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class StrategyPositionUpdateBenchmarks
{
    readonly StrategyPositionActorStateMachine state = new();
    Guid[] legIds = [];
    long sequence;

    [GlobalSetup]
    public void Setup()
    {
        legIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var componentId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var legs = legIds.Select((legId, index) => new TradeLegDefinition
        {
            TradeLegId = legId,
            AssetFamily = TradeAssetFamily.FuturesOption,
            SignedQuantity = index is 0 or 3 ? 1 : -1,
            ContractKey = $"ES-OPTION-{index}",
            ContractId = $"ES-OPTION-{index}"
        }).ToArray();
        var fills = legs.Select((leg, index) => new ExecutionFillEvidence
        {
            ExecutionFillId = Guid.NewGuid(),
            ExecutionAttemptId = attemptId,
            ComponentId = componentId,
            TradeLegId = leg.TradeLegId,
            ContractId = leg.ContractId,
            SignedQuantity = leg.SignedQuantity,
            Price = 10m + index,
            FilledAtUtc = DateTime.UnixEpoch,
            ExternalExecutionId = $"BENCH-{index}"
        }).ToArray();
        var trade = new EstablishedTradeDefinition
        {
            Id = new TradeEntityId(1, 1, 1, 1),
            AssetFamily = TradeAssetFamily.FuturesOption,
            StrategyKind = TradeStrategyKind.IronCondor,
            SourceComponentId = componentId,
            ExecutionAttemptId = attemptId,
            Status = EstablishedTradeStatus.Open,
            Legs = legs,
            OriginalFills = fills,
            EstablishedAtUtc = DateTime.UnixEpoch,
            EvidenceRevision = 1
        };
        var opened = state.Open(trade, Guid.NewGuid(), DateTime.UnixEpoch);
        if (!opened.Accepted) throw new InvalidOperationException(opened.Detail);
    }

    [Benchmark]
    public StrategyPositionSnapshot UpdateOneOfFourLegs()
    {
        var next = Interlocked.Increment(ref sequence);
        var result = state.UpdateLeg(
            legIds[(int)(next & 3)],
            10m + (next % 1000) / 100m,
            next,
            DateTime.UnixEpoch.AddTicks(next),
            1);
        return result.Value ?? throw new InvalidOperationException(result.Detail);
    }
}
