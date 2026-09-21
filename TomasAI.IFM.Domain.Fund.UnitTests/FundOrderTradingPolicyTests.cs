using FluentAssertions;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class FundOrderTradingPolicyTests
{
    [Fact]
    public void Empty_open_order_accepts_one_primary_opening_trade()
    {
        var order = Order();
        var opening = Trade(1, TradeType.ShortIronCondor, TradeState.NewTrade, true);

        FundOrderTradingPolicy.CanAddTrade(order).Should().BeTrue();
        FundOrderTradingPolicy.IsCompatibleAddition(order, opening).Should().BeTrue();
        FundOrderTradingPolicy.CanDeleteOrder(order).Should().BeTrue();
    }

    [Fact]
    public void Opening_position_allows_only_derived_non_primary_closer()
    {
        var order = Order();
        order.Add(Trade(1, TradeType.ShortIronCondor, TradeState.TradeToOpen, true));

        FundOrderTradingPolicy.CanAddTrade(order).Should().BeTrue();
        FundOrderTradingPolicy.IsCompatibleAddition(
            order, Trade(2, TradeType.LongIronCondor, TradeState.NewTrade, false)).Should().BeTrue();
        FundOrderTradingPolicy.IsCompatibleAddition(
            order, Trade(2, TradeType.ShortIronCondor, TradeState.NewTrade, false)).Should().BeFalse();
        FundOrderTradingPolicy.CanRemoveTrade(order, order.Trades[0]).Should().BeFalse();
        FundOrderTradingPolicy.CanDeleteOrder(order).Should().BeFalse();
        FundOrderTradingPolicy.CanCloseOrder(order).Should().BeFalse();
    }

    [Fact]
    public void Single_trade_that_is_not_an_open_position_rejects_another_trade()
    {
        var order = Order();
        order.Add(Trade(1, TradeType.ShortIronCondor, TradeState.NewTrade, true));

        FundOrderTradingPolicy.CanAddTrade(order).Should().BeFalse();
    }

    [Fact]
    public void Closed_order_rejects_a_trade_even_when_it_has_no_trades()
    {
        var order = Order(TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Closed);

        FundOrderTradingPolicy.CanAddTrade(order).Should().BeFalse();
    }

    [Fact]
    public void Two_trades_reach_temporary_cap_and_close_evidence_locks_both()
    {
        var order = Order();
        order.Add(Trade(1, TradeType.ShortIronCondor, TradeState.TradeToOpen, true));
        order.Add(Trade(2, TradeType.LongIronCondor, TradeState.OrderCompleted, false));

        FundOrderTradingPolicy.CanAddTrade(order).Should().BeFalse();
        order.Trades.Should().OnlyContain(trade =>
            !FundOrderTradingPolicy.CanRemoveTrade(order, trade));
        FundOrderTradingPolicy.CanDeleteOrder(order).Should().BeFalse();
        FundOrderTradingPolicy.CanCloseOrder(order).Should().BeTrue();
    }

    [Fact]
    public void Two_trades_disable_removal_even_when_closer_is_still_new()
    {
        var order = Order();
        order.Add(Trade(1, TradeType.ShortIronCondor, TradeState.TradeToOpen, true));
        order.Add(Trade(2, TradeType.LongIronCondor, TradeState.NewTrade, false));

        FundOrderTradingPolicy.CanAddTrade(order).Should().BeFalse();
        order.Trades.Should().OnlyContain(trade =>
            !FundOrderTradingPolicy.CanRemoveTrade(order, trade));
    }

    [Theory]
    [InlineData(TradeState.NewTrade, true)]
    [InlineData(TradeState.OrderCancelled, true)]
    [InlineData(TradeState.OrderSubmitted, false)]
    [InlineData(TradeState.OrderPartiallyFilled, false)]
    [InlineData(TradeState.OrderFilled, false)]
    [InlineData(TradeState.TradeToOpen, false)]
    [InlineData(TradeState.OrderCompleted, false)]
    public void Removal_requires_pristine_or_cancelled_zero_fill_trade(
        TradeState state,
        bool expected)
    {
        var order = Order();
        var trade = Trade(1, TradeType.ShortIronCondor, state, true);
        order.Add(trade);

        FundOrderTradingPolicy.CanRemoveTrade(order, trade).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Cancelled_trade_requires_confirmed_zero_fill_evidence(
        bool? hasFillEvidence,
        bool expected)
    {
        var order = Order();
        var trade = Trade(
            1, TradeType.ShortIronCondor, TradeState.OrderCancelled, true, hasFillEvidence);
        order.Add(trade);

        FundOrderTradingPolicy.CanRemoveTrade(order, trade).Should().Be(expected);
    }

    [Fact]
    public void Cancellation_retains_prior_fill_evidence()
    {
        var aggregateTrade = new TomasAI.IFM.Domain.Fund.Command.Model.FundOrderTrade(
            Trade(1, TradeType.ShortIronCondor, TradeState.NewTrade, true));
        aggregateTrade.SetTradeState(TradeState.OrderPartiallyFilled);
        aggregateTrade.SetTradeState(TradeState.OrderCancelled);
        var cancelledTrade = aggregateTrade.ToViewModel();
        var order = Order();
        order.Add(cancelledTrade);

        cancelledTrade.HasFillEvidence.Should().BeTrue();
        FundOrderTradingPolicy.CanRemoveTrade(order, cancelledTrade).Should().BeFalse();
    }

    static FundOrderReadModel Order(
        TomasAI.IFM.Domain.Fund.Shared.OrderStatus status = TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open) => new(
        17, 101, DateTime.UtcNow, status, "ES",
        new DateOnly(2026, 9, 18), new DateOnly(2026, 10, 16),
        "ES strategy", DateTime.UtcNow, "test", null, string.Empty);

    static FundOrderTradeReadModel Trade(
        int tradeId,
        TradeType type,
        TradeState state,
        bool primary,
        bool? hasFillEvidence = false) => new(
        17, 101, tradeId, type,
        new DateOnly(2026, 9, 18), new DateOnly(2026, 10, 16),
        state,
        type is TradeType.ShortIronCondor or TradeType.PutCreditSpread or TradeType.CallCreditSpread
            ? TradeAction.Sell
            : TradeAction.Buy,
        "ES strategy", primary, "ES", DateTime.UtcNow, "test", null, string.Empty,
        hasFillEvidence);
}
