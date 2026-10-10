using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Workflow;

public sealed class FundTradeExecutionLifecycleTests
{
    static readonly DateTime Now = new(2026, 10, 9, 14, 0, 0, DateTimeKind.Utc);
    static (PortfolioFundCompositionAggregate Aggregate, FundTradeExecutionEvidence Opening) Fixture(bool includeClosing = true)
    {
        var aggregate = new PortfolioFundCompositionAggregate();
        var draft = aggregate.CreateManualDraft(new() { PortfolioId = 1, PortfolioVersion = 1, FundId = 2,
            FundMandateVersion = 1, IdempotencyKey = Guid.NewGuid(), Reference = "Lifecycle test",
            RequestedAtUtc = Now, ExpiresAtUtc = Now.AddDays(1) }, 10, Now, "test");
        var open = aggregate.AddManualTrade(new() { PortfolioId = 1, FundId = 2, OrderId = 10,
            ExpectedOrderVersion = draft.AggregateVersion, TradeId = 20, TradeType = nameof(TradeType.ShortIronCondor),
            TradeState = nameof(TradeState.TradeToOpen), TradeAction = nameof(TradeAction.Sell), PrimaryTrade = true,
            EffectiveDate = new(2026,10,9), Reference = "Open", BaseContractSymbol = "ES", BaseContractId = "ESZ26", RequestedAtUtc = Now }, "test");
        if (includeClosing) aggregate.AddManualTrade(new() { PortfolioId = 1, FundId = 2, OrderId = 10,
            ExpectedOrderVersion = open.AggregateVersion, TradeId = 21, TradeType = nameof(TradeType.LongIronCondor),
            TradeState = nameof(TradeState.NewTrade), TradeAction = nameof(TradeAction.Buy), PrimaryTrade = false,
            EffectiveDate = new(2026,10,9), Reference = "Close", BaseContractSymbol = "ES", BaseContractId = "ESZ26", RequestedAtUtc = Now }, "test");
        return (aggregate, new() { PortfolioId = 1, FundId = 2, SetupTrade = new() { OrderId = 10, TradeId = 20 },
            ExecutionOrderId = 30, ExecutionTradeId = 40, ExecutionAttemptId = Guid.NewGuid(), OccurredAtUtc = Now,
            TradeDate = new(2026,10,9), MaturityDate = new(2026,11,20) });
    }

    [Fact] public void Opening_completion_before_submission_notification_does_not_regress()
    {
        var (aggregate, evidence) = Fixture();
        var opened = aggregate.RecordFundTradeOpening(evidence);
        var delayed = aggregate.RecordFundTradeSubmission(evidence);
        delayed.Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Open));
        delayed.AggregateVersion.Should().Be(opened.AggregateVersion);
        delayed.Trades.Single(x => x.PrimaryTrade).TradeDate.Should().Be(evidence.TradeDate);
        delayed.Trades.Single(x => x.PrimaryTrade).MaturityDate.Should().Be(evidence.MaturityDate);
    }

    [Fact] public void Rejected_unfilled_submission_releases_binding_for_retry()
    {
        var (aggregate, evidence) = Fixture();
        aggregate.RecordFundTradeSubmission(evidence);
        var released = aggregate.ReleaseFundTradeSubmission(evidence);
        released.Trades.Single(x => x.PrimaryTrade).ExecutionOrderId.Should().Be(0);
        released.Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.NewTrade));
        var retried = aggregate.RecordFundTradeSubmission(evidence with { ExecutionAttemptId = Guid.NewGuid(), ExecutionOrderId = 31 });
        retried.Trades.Single(x => x.PrimaryTrade).ExecutionOrderId.Should().Be(31);
    }

    [Fact] public void Release_cannot_undo_an_established_trade()
    {
        var (aggregate, evidence) = Fixture();
        aggregate.RecordFundTradeOpening(evidence);
        aggregate.ReleaseFundTradeSubmission(evidence).Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Open));
    }

    [Fact] public void Stale_attempt_and_wrong_fund_are_rejected()
    {
        var (aggregate, evidence) = Fixture();
        aggregate.RecordFundTradeSubmission(evidence);
        ((Action)(() => aggregate.RecordFundTradeOpening(evidence with { ExecutionAttemptId = Guid.NewGuid() }))).Should().Throw<InvalidOperationException>();
        ((Action)(() => aggregate.RecordFundTradeOpening(evidence with { FundId = 3 }))).Should().Throw<InvalidOperationException>();
    }

    [Fact] public void Complete_close_finalizes_both_setup_trades_and_is_idempotent()
    {
        var (aggregate, evidence) = Fixture();
        aggregate.RecordFundTradeOpening(evidence);
        var close = evidence with { SetupTrade = new() { OrderId = 10, TradeId = 21 }, ExecutionOrderId = 31,
            ExecutionAttemptId = Guid.NewGuid(), ClosingExecution = true, OpeningExecutionTradeId = 40, FullyClosed = true };
        aggregate.RecordFundTradeSubmission(close);
        var completed = aggregate.RecordFundTradeClosing(close);
        completed.Order.Status.Should().Be(nameof(FundCompositionState.Executed));
        completed.Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Closed));
        completed.Trades.Single(x => !x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.OrderCompleted));
        aggregate.RecordFundTradeClosing(close).AggregateVersion.Should().Be(completed.AggregateVersion);
        ((Action)(() => aggregate.RecordFundTradeSubmission(evidence))).Should().Throw<InvalidOperationException>();
    }

    [Fact] public void Partial_close_keeps_position_setup_open_and_permits_remaining_close()
    {
        var (aggregate, evidence) = Fixture(); aggregate.RecordFundTradeOpening(evidence);
        var close = evidence with { SetupTrade = new() { OrderId = 10, TradeId = 21 }, ExecutionOrderId = 31,
            ExecutionAttemptId = Guid.NewGuid(), ClosingExecution = true, OpeningExecutionTradeId = 40 };
        aggregate.RecordFundTradeSubmission(close);
        var partial = aggregate.RecordFundTradeClosing(close);
        partial.Order.Status.Should().Be(nameof(FundCompositionState.Draft));
        partial.Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Open));
        partial.Trades.Single(x => !x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.OrderPartiallyFilled));
        var remainder = close with { ExecutionAttemptId = Guid.NewGuid(), ExecutionOrderId = 32, FullyClosed = true };
        aggregate.RecordFundTradeSubmission(remainder);
        aggregate.RecordFundTradeClosing(remainder).Order.Status.Should().Be(nameof(FundCompositionState.Executed));
        ((Action)(() => aggregate.RecordFundTradeClosing(close))).Should().Throw<InvalidOperationException>();
    }

    [Fact] public void State_preserving_execution_evidence_commits_a_contiguous_source_event()
    {
        var (composition, opening) = Fixture(false);
        composition.RecordFundTradeOpening(opening);
        var fund = new PortfolioFundAggregate();
        fund.RestoreSnapshot(new PortfolioFundAggregateSnapshot(1,
            TomasAI.IFM.Domain.Portfolio.UnitTests.Command.PortfolioFundAggregateTests.Draft() with { PortfolioId = 1, FundId = 2 }, [],
            composition.CaptureState().ToArray(), [Guid.NewGuid()]));
        var prior = fund.Composition(10).AggregateVersion;
        var command = new RecordFundTradeSubmissionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new(1, 2),
            ExecutionEvidence = opening with { ClosingExecution = true, OpeningExecutionTradeId = 40,
                ExecutionOrderId = 31, ExecutionAttemptId = Guid.NewGuid() }
        };
        var result = command.Execute(fund, Now, "test");
        result.Success.Should().BeTrue();
        fund.Composition(10).AggregateVersion.Should().Be(prior + 1);
        fund.Composition(10).Trades.Single().TradeState.Should().Be(nameof(TradeState.Open));
        fund.Composition(10).Trades.Single().ExecutionOrderId.Should().Be(opening.ExecutionOrderId);
    }

    [Fact] public void Workflow_close_without_secondary_setup_preserves_opening_execution_identity()
    {
        var (aggregate, evidence) = Fixture(false); aggregate.RecordFundTradeOpening(evidence);
        var close = evidence with { ExecutionOrderId = 31, ExecutionAttemptId = Guid.NewGuid(),
            ClosingExecution = true, OpeningExecutionTradeId = 40, FullyClosed = true };
        var result = aggregate.RecordFundTradeClosing(close);
        result.Trades.Single().TradeState.Should().Be(nameof(TradeState.Closed));
        result.Trades.Single().ExecutionOrderId.Should().Be(30);
        result.Trades.Single().ExecutionTradeId.Should().Be(40);
    }
}
