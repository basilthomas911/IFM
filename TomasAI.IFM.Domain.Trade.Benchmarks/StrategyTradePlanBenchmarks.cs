using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Model;

namespace TomasAI.IFM.Domain.Trade.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class StrategyTradePlanBenchmarks
{
    readonly DateTime now = new(2026, 9, 14, 14, 30, 0, DateTimeKind.Utc);
    readonly TradePlanParameters parameters = new();
    readonly IronCondorTradePlanAlgorithm ironCondor = new();
    readonly VerticalSpreadTradePlanAlgorithm verticalSpread = new();
    readonly FuturesTradePlanAlgorithm futures = new();
    StrategyPositionSnapshot ironCondorPosition = new();
    StrategyPositionSnapshot verticalSpreadPosition = new();
    StrategyPositionSnapshot futuresPosition = new();
    ExitPositionWorkflowStartedEvent exit = new();
    StrategyTradePlanSnapshot serializedPlan = new();

    [GlobalSetup]
    public void Setup()
    {
        ironCondorPosition = Position(TradeStrategyKind.IronCondor, 4, -1_100m);
        verticalSpreadPosition = Position(TradeStrategyKind.VerticalSpread, 2, 25m);
        futuresPosition = Position(TradeStrategyKind.FuturesOutright, 1, 25m);
        serializedPlan = ironCondor.Calculate(ironCondorPosition, parameters, null,
            DateOnly.FromDateTime(now), now).Snapshot;
        exit = new()
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EntityId = new(ironCondorPosition.Id, DateOnly.FromDateTime(now),
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            StrategyKind = TradeStrategyKind.IronCondor,
            SourcePlanEventId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            ExitPlan = serializedPlan
        };
    }

    [Benchmark]
    public StrategyTradePlanSnapshot IronCondorMaximumLoss() => ironCondor.Calculate(
        ironCondorPosition, parameters, null, DateOnly.FromDateTime(now), now).Snapshot;

    [Benchmark]
    public StrategyTradePlanSnapshot VerticalSpreadNormal() => verticalSpread.Calculate(
        verticalSpreadPosition, parameters, null, DateOnly.FromDateTime(now), now).Snapshot;

    [Benchmark]
    public StrategyTradePlanSnapshot FuturesNormal() => futures.Calculate(
        futuresPosition, parameters, null, DateOnly.FromDateTime(now), now).Snapshot;

    [Benchmark]
    public ExitOrderComposition ComposeFourLegExit() =>
        StrategyExitOrderCompositionModel.Compose(exit, now);

    [Benchmark]
    public byte[] SerializePlanSnapshot() => MessagePackSerializer.Serialize(serializedPlan);

    static StrategyPositionSnapshot Position(TradeStrategyKind strategy, int legCount, decimal pnl)
    {
        var at = new DateTime(2026, 9, 14, 14, 30, 0, DateTimeKind.Utc);
        var legs = Enumerable.Range(1, legCount).Select(index => new StrategyPositionLeg
        {
            TradeLegId = Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
            ContractId = $"CONTRACT-{index}",
            ContractKey = $"CONTRACT-{index}",
            AssetFamily = strategy == TradeStrategyKind.FuturesOutright
                ? TradeAssetFamily.Futures : TradeAssetFamily.FuturesOption,
            SignedQuantity = index % 2 == 0 ? -1 : 1,
            OpeningPrice = 10 + index,
            CurrentPrice = 10.1m + index,
            LastSourceSequence = 1,
            LastPriceAtUtc = at
        }).ToArray();
        return new()
        {
            Id = new(new(1, 2, 3, 4), Guid.Parse("11111111-1111-1111-1111-111111111111")),
            StrategyKind = strategy,
            Phase = StrategyPositionPhase.MarkToMarket,
            PositionSequence = 1,
            RouteGeneration = 1,
            Legs = legs,
            MarketValue = legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity),
            UnrealizedPnl = pnl,
            AsOfUtc = at,
            IsOpen = true
        };
    }
}
