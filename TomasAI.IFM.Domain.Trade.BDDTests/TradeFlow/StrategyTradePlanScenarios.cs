using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.BDDTests.TradeFlow;

public sealed class StrategyTradePlanScenarios
{
    static readonly DateTime Now = new(2026, 9, 14, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Given_a_current_open_position_when_loss_is_inside_limits_then_monitoring_continues()
    {
        var position = Position(unrealizedPnl: -100m, asOfUtc: Now);

        var result = new IronCondorTradePlanAlgorithm().Calculate(
            position, Parameters(), null, DateOnly.FromDateTime(Now), Now).Snapshot;

        result.State.Should().Be(TradePlanState.Normal);
        result.Action.Should().Be(TradePlanAction.Monitor);
        result.RequiresExit.Should().BeFalse();
    }

    [Fact]
    public void Given_a_current_open_position_when_maximum_loss_is_reached_then_market_exit_is_requested()
    {
        var position = Position(unrealizedPnl: -1_000m, asOfUtc: Now);

        var result = new IronCondorTradePlanAlgorithm().Calculate(
            position, Parameters(), null, DateOnly.FromDateTime(Now), Now).Snapshot;

        result.State.Should().Be(TradePlanState.ExitRequired);
        result.Action.Should().Be(TradePlanAction.ExitAtMarket);
        result.ReasonCode.Should().Be("PLAN.MAX_LOSS");
    }

    [Fact]
    public void Given_stale_prices_when_loss_is_breached_then_the_plan_holds_for_fresh_evidence()
    {
        var position = Position(unrealizedPnl: -1_000m, asOfUtc: Now.AddMinutes(-2));

        var result = new IronCondorTradePlanAlgorithm().Calculate(
            position, Parameters(), null, DateOnly.FromDateTime(Now), Now).Snapshot;

        result.State.Should().Be(TradePlanState.Hold);
        result.Action.Should().Be(TradePlanAction.Hold);
        result.RequiresExit.Should().BeFalse();
        result.ReasonCode.Should().Be("PLAN.DATA.STALE");
    }

    static TradePlanParameters Parameters() => new()
    {
        MaximumLoss = 1_000m,
        WarningLoss = 750m,
        ProfitTarget = 500m,
        MaterialPnlChange = 10m,
        MaterialPriceChange = 0.25m,
        MaximumDataAgeSeconds = 30
    };

    static StrategyPositionSnapshot Position(decimal unrealizedPnl, DateTime asOfUtc)
    {
        var legs = Enumerable.Range(1, 4).Select(index => new StrategyPositionLeg
        {
            TradeLegId = Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
            ContractId = $"ES-OPTION-{index}",
            AssetFamily = TradeAssetFamily.FuturesOption,
            SignedQuantity = index % 2 == 0 ? -1 : 1,
            OpeningPrice = 10m + index,
            CurrentPrice = 10.1m + index,
            LastSourceSequence = 1,
            LastPriceAtUtc = asOfUtc
        }).ToArray();
        return new StrategyPositionSnapshot
        {
            Id = new(new TradeEntityId(1, 1, 1, 1),
                Guid.Parse("22222222-2222-2222-2222-222222222222")),
            StrategyKind = TradeStrategyKind.IronCondor,
            Phase = StrategyPositionPhase.MarkToMarket,
            PositionSequence = 1,
            RouteGeneration = 1,
            Legs = legs,
            UnrealizedPnl = unrealizedPnl,
            AsOfUtc = asOfUtc,
            IsOpen = true
        };
    }
}
