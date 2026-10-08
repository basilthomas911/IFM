using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorLegacyRuleTests
{
    static IronCondorTradePlanSnapshot Snapshot => new()
    { MaxProfit = 1000, MaxLoss = -500, DailyProfitTarget = 100, StopLossLimit = 0, ForwardLossRatio = 0.1 };
    static IronCondorTradePlanInputs Inputs => new()
    { TradeType = TradeType.ShortIronCondor, AverageTradePnl = 50, FundBalance = 10000,
        ForwardLossLimit = ForwardLossLimitType.LimitWarning, TradeSignal = new FuturesTradeSignalV2ReadModel { MDI = 50 } };

    [Fact]
    public void Raise_precedes_daily_profit_and_does_not_mutate_input()
    {
        var original = Snapshot;
        var result = IronCondorLegacyRuleCompute.Evaluate(original, Inputs with { AverageTradePnl = 201 });
        result.ActionSubType.Should().Be(nameof(ActionSubType.RaiseTrailingStopLimit));
        result.StopLossLimit.Should().Be(0.20);
        original.StopLossLimit.Should().Be(0);
    }
    [Fact]
    public void Trailing_threshold_is_strict_and_clears_on_exit_recommendation()
    {
        var original = Snapshot with { StopLossLimit = 0.25 };
        IronCondorLegacyRuleCompute.Evaluate(original, Inputs with { AverageTradePnl = 199 }).ActionSubType.Should().Be(nameof(ActionSubType.TrailingStopLimitReached));
        IronCondorLegacyRuleCompute.Evaluate(original, Inputs with { AverageTradePnl = 200 }).ActionSubType.Should().Be(nameof(ActionSubType.InTrailingStop));
    }
    [Fact]
    public void Loss_clears_trailing_stop_before_max_loss_as_in_legacy_handler()
        => IronCondorLegacyRuleCompute.Evaluate(Snapshot with { StopLossLimit = 0.2 }, Inputs with { AverageTradePnl = -600 })
            .ActionSubType.Should().Be(nameof(ActionSubType.ClearStopLossLimit));
    [Fact]
    public void Maximum_loss_precedes_forward_loss()
        => IronCondorLegacyRuleCompute.Evaluate(Snapshot with { ForwardLossRatio = 0.9 }, Inputs with { AverageTradePnl = -600,
            ForwardLossLimit = ForwardLossLimitType.LimitReached }).ActionSubType.Should().Be(nameof(ActionSubType.MaxLossLimitReached));
    [Theory]
    [InlineData(TradeType.ShortIronCondor, 0.9)]
    [InlineData(TradeType.LongIronCondor, 0.1)]
    public void Mdi_direction_changes_for_long_and_short_condors(TradeType strategy, double ratio)
        => IronCondorLegacyRuleCompute.Evaluate(Snapshot with { ForwardLossRatio = ratio }, Inputs with
            { TradeType = strategy, AverageTradePnl = -10, ForwardLossLimit = ForwardLossLimitType.LimitReached })
            .ActionSubType.Should().Be(nameof(ActionSubType.ForwardLossRiskLimitReached));
    [Fact]
    public void Missing_business_inputs_do_not_generate_normal_recommendations()
        => IronCondorLegacyRuleCompute.Evaluate(Snapshot, Inputs with { FundBalance = null }).ActionType.Should().BeNull();
}
