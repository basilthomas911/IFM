using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Command;

public sealed class ManualTradeLifecycleScenarios
{
    [Theory]
    [InlineData("Monthly", "ShortIronCondor", "LongIronCondor", "Sell", "Buy")]
    [InlineData("Weekly", "CallDebitSpread", "CallCreditSpread", "Buy", "Sell")]
    [InlineData("Daily", "FuturesOutright", "FuturesOutright", "Buy", "Sell")]
    public void Given_a_manual_draft_when_opened_then_a_compatible_close_can_settle_once(
        string horizon, string openingType, string closingType, string openingAction, string closingAction)
    {
        var now = new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc);
        var valueDate = DateOnly.FromDateTime(now);
        var maturity = horizon == "Daily" ? (DateOnly?)null : valueDate.AddDays(horizon == "Monthly" ? 30 : 7);
        var aggregate = new PortfolioFundCompositionAggregate();
        var draft = aggregate.CreateManualDraft(new() { PortfolioId = 1, FundId = 2, PortfolioVersion = 1,
            FundMandateVersion = 1, IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1), Reference = horizon }, 10, now, "operator");
        draft.Trades.Should().BeEmpty();
        AddManualFundOrderTradeRequest Request(int id, bool primary, string type, string action, long version) => new()
        {
            PortfolioId = 1, FundId = 2, OrderId = 10, TradeId = id, PrimaryTrade = primary,
            TradeType = type, TradeAction = action, TradeState = "NewTrade", ExpectedOrderVersion = version,
            BaseContractId = "ESZ6", BaseContractSymbol = "ES", EffectiveDate = valueDate,
            RequestedAtUtc = now, MaturityDate = maturity, Reference = $"{action} {horizon}"
        };
        var added = aggregate.AddManualTrade(Request(20, true, openingType, openingAction, draft.AggregateVersion), "operator");
        added.Trades.Should().ContainSingle();
        var duplicate = () => aggregate.AddManualTrade(Request(21, true, openingType, openingAction, added.AggregateVersion), "operator");
        duplicate.Should().Throw<InvalidOperationException>();
        var prematureClose = () => aggregate.AddManualTrade(Request(21, false, closingType, closingAction, added.AggregateVersion), "operator");
        prematureClose.Should().Throw<InvalidOperationException>();
        var opening = new FundTradeExecutionEvidence { PortfolioId = 1, FundId = 2,
            SetupTrade = new() { OrderId = 10, TradeId = 20 }, ExecutionOrderId = 30,
            ExecutionTradeId = 40, ExecutionAttemptId = Guid.NewGuid(), OccurredAtUtc = now,
            TradeDate = valueDate, MaturityDate = maturity };
        aggregate.RecordFundTradeSubmission(opening).Order.Status.Should().Be("Opening");
        var opened = aggregate.RecordFundTradeOpening(opening);
        opened.Order.Status.Should().Be("Opened");
        var close = aggregate.AddManualTrade(Request(21, false, closingType, closingAction, opened.AggregateVersion), "operator");
        close.Trades.Should().HaveCount(2);
        var evidence = opening with { SetupTrade = new() { OrderId = 10, TradeId = 21 },
            ExecutionOrderId = 31, ExecutionAttemptId = Guid.NewGuid(), ClosingExecution = true,
            OpeningExecutionTradeId = 40, FullyClosed = true, ExecutionStatus = "Filled" };
        aggregate.RecordFundTradeSubmission(evidence).Order.Status.Should().Be("Closing");
        var closed = aggregate.RecordFundTradeClosing(evidence);
        closed.Order.Status.Should().Be("Closed");
        closed.Trades.Should().OnlyContain(trade => trade.TradeState == "Closed");
        aggregate.RecordFundTradeClosing(evidence).AggregateVersion.Should().Be(closed.AggregateVersion);
        var replay = new PortfolioFundCompositionAggregate();
        replay.ApplyReservation(closed);
        replay.Orders.Should().ContainSingle().Which.Status.Should().Be("Closed");
        replay.ReservationForOrder(10).Trades.Should().HaveCount(2);
    }
}
