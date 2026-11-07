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
    [Fact]
    public void Operator_trade_reference_is_separate_from_the_generated_instruction_reference()
    {
        var (aggregate, evidence) = Fixture();
        var rows = aggregate.RecordFundTradeSubmission(evidence).Trades;
        var opening = rows.Single(trade => trade.PrimaryTrade);
        var closing = rows.Single(trade => !trade.PrimaryTrade);
        opening.TradeReference.Should().Be("Open");
        closing.TradeReference.Should().Be("Close");
        opening.InstructionReference.Should().Be(FundOrderTradeReference.Create("ESZ26", new(2026, 10, 9), null));
        closing.InstructionReference.Should().Be(FundOrderTradeReference.Create("ESZ26", new(2026, 10, 9), null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Closing_setup_inherits_opening_maturity_and_rejects_a_different_expiry(bool differentExpiry)
    {
        var (aggregate, evidence) = Fixture(includeClosing: false);
        var opened = aggregate.RecordFundTradeOpening(evidence);
        var request = new AddManualFundOrderTradeRequest {
            PortfolioId = 1, FundId = 2, OrderId = 10, ExpectedOrderVersion = opened.AggregateVersion,
            TradeId = 21, TradeType = nameof(TradeType.LongIronCondor),
            TradeState = nameof(TradeState.NewTrade), TradeAction = nameof(TradeAction.Buy), PrimaryTrade = false,
            EffectiveDate = new(2026,10,9), BaseContractSymbol = "ES", BaseContractId = "ESZ26",
            RequestedAtUtc = Now, MaturityDate = differentExpiry ? new(2026,12,18) : null
        };
        if (differentExpiry)
        {
            var act = () => aggregate.AddManualTrade(request, "test");
            act.Should().Throw<InvalidOperationException>().WithMessage("*opening trade's maturity*");
        }
        else
        {
            var closing = aggregate.AddManualTrade(request, "test").Trades.Single(trade => !trade.PrimaryTrade);
            closing.RequestedMaturityDate.Should().Be(evidence.MaturityDate);
            closing.MaturityDate.Should().BeNull();
        }
    }
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
        released.Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.OrderCancelled));
        var retried = aggregate.RecordFundTradeSubmission(evidence with { ExecutionAttemptId = Guid.NewGuid(), ExecutionOrderId = 31 });
        retried.Trades.Single(x => x.PrimaryTrade).ExecutionOrderId.Should().Be(31);
    }

    [Fact] public void Release_cannot_undo_an_established_trade()
    {
        var (aggregate, evidence) = Fixture();
        aggregate.RecordFundTradeOpening(evidence);
        aggregate.ReleaseFundTradeSubmission(evidence).Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Open));
    }

    [Fact] public void Broker_rejection_persists_reason_and_allows_removing_the_unfilled_setup()
    {
        var (aggregate, evidence) = Fixture(includeClosing: false);
        aggregate.RecordFundTradeSubmission(evidence);
        const string reason = "EM.EXPIRED: Approved order is expired or time is not UTC.";
        var rejected = aggregate.ReleaseFundTradeSubmission(evidence with { RejectionDetail = reason });
        var trade = rejected.Trades.Single();
        trade.TradeState.Should().Be(nameof(TradeState.Rejected));
        trade.TradeStatus.Should().Be(reason);
        trade.ExecutionOrderId.Should().Be(0);
        aggregate.RecordFundTradeSubmission(evidence).Trades.Single().TradeStatus.Should().Be(reason);
        var removed = aggregate.RemoveManualTrade(new()
        {
            PortfolioId = 1, FundId = 2, OrderId = 10, TradeId = 20,
            ExpectedOrderVersion = rejected.AggregateVersion, RequestedAtUtc = Now
        });
        removed.Trades.Should().BeEmpty();
    }

    [Fact]
    public void Each_execution_transition_updates_the_status_with_the_state()
    {
        var (aggregate, evidence) = Fixture(includeClosing: false);
        aggregate.RecordFundTradeSubmission(evidence).Trades.Single().TradeStatus
            .Should().Be("Order submitted for execution.");
        aggregate.ReleaseFundTradeSubmission(evidence with { RejectionDetail = "Broker rejected the order." });
        var retry = evidence with { ExecutionAttemptId = Guid.NewGuid(), ExecutionOrderId = 31 };
        var submitted = aggregate.RecordFundTradeSubmission(retry).Trades.Single();
        submitted.TradeState.Should().Be(nameof(TradeState.OrderSubmitted));
        submitted.TradeStatus.Should().Be("Order submitted for execution.");
        var opened = aggregate.RecordFundTradeOpening(retry).Trades.Single();
        opened.TradeState.Should().Be(nameof(TradeState.Open));
        opened.TradeStatus.Should().Be("Opening execution completed.");
    }

    [Fact]
    public void Reconciled_rejected_close_can_be_removed_without_removing_its_opening_trade()
    {
        var (aggregate, opening) = Fixture();
        aggregate.RecordFundTradeOpening(opening);
        var closing = opening with
        {
            SetupTrade = new() { OrderId = 10, TradeId = 21 }, ClosingExecution = true,
            OpeningExecutionTradeId = opening.ExecutionTradeId, ExecutionOrderId = 31,
            ExecutionAttemptId = Guid.NewGuid(), RejectionDetail = "EM.EXPIRED: Approval expired."
        };
        var rejected = aggregate.ReleaseFundTradeSubmission(closing);
        rejected.Trades.Single(x => x.TradeId == 21).TradeStatus.Should().Be(closing.RejectionDetail);
        aggregate.ReleaseFundTradeSubmission(closing).AggregateVersion.Should().Be(rejected.AggregateVersion);
        var removed = aggregate.RemoveManualTrade(new()
        {
            PortfolioId = 1, FundId = 2, OrderId = 10, TradeId = 21,
            ExpectedOrderVersion = rejected.AggregateVersion, RequestedAtUtc = Now
        });
        removed.Trades.Should().ContainSingle().Which.TradeState.Should().Be(nameof(TradeState.Open));
        rejected.Order.Status.Should().Be(nameof(FundCompositionState.Opened));
        removed.Order.Status.Should().Be(nameof(FundCompositionState.Opened));
    }

    [Fact]
    public void Order_status_tracks_opening_closing_rejection_removal_and_retry()
    {
        var (aggregate, opening) = Fixture();
        aggregate.RecordFundTradeSubmission(opening).Order.Status.Should().Be(nameof(FundCompositionState.Opening));
        aggregate.RecordFundTradeSubmission(opening with { ExecutionStatus = "PartiallyFilled" })
            .Order.Status.Should().Be(nameof(FundCompositionState.Opening));
        aggregate.RecordFundTradeOpening(opening).Order.Status.Should().Be(nameof(FundCompositionState.Opened));
        var close = opening with { SetupTrade = new() { OrderId = 10, TradeId = 21 }, ClosingExecution = true,
            OpeningExecutionTradeId = opening.ExecutionTradeId, ExecutionOrderId = 31, ExecutionAttemptId = Guid.NewGuid() };
        aggregate.RecordFundTradeSubmission(close).Order.Status.Should().Be(nameof(FundCompositionState.Closing));
        var rejected = aggregate.ReleaseFundTradeSubmission(close with { RejectionDetail = "Rejected" });
        rejected.Order.Status.Should().Be(nameof(FundCompositionState.Opened));
        var removed = aggregate.RemoveManualTrade(new() { PortfolioId = 1, FundId = 2, OrderId = 10,
            TradeId = 21, ExpectedOrderVersion = rejected.AggregateVersion, RequestedAtUtc = Now });
        removed.Order.Status.Should().Be(nameof(FundCompositionState.Opened));
        var added = aggregate.AddManualTrade(new() { PortfolioId = 1, FundId = 2, OrderId = 10,
            TradeId = 22, ExpectedOrderVersion = removed.AggregateVersion, TradeType = nameof(TradeType.LongIronCondor),
            TradeAction = nameof(TradeAction.Buy), TradeState = nameof(TradeState.NewTrade), PrimaryTrade = false,
            EffectiveDate = new(2026,10,9), BaseContractSymbol = "ES", BaseContractId = "ESZ26", RequestedAtUtc = Now }, "test");
        added.Order.Status.Should().Be(nameof(FundCompositionState.Opened));
        var retry = close with { SetupTrade = new() { OrderId = 10, TradeId = 22 }, ExecutionOrderId = 32,
            ExecutionAttemptId = Guid.NewGuid(), FullyClosed = true };
        aggregate.RecordFundTradeSubmission(retry).Order.Status.Should().Be(nameof(FundCompositionState.Closing));
        var filled = aggregate.RecordFundTradeSubmission(retry with { ExecutionStatus = "Filled" });
        filled.Order.Status.Should().Be(nameof(FundCompositionState.Closing));
        filled.Trades.Single(row => !row.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.OrderFilled));
        aggregate.RecordFundTradeSubmission(retry with { ExecutionStatus = "PartiallyFilled" })
            .Trades.Single(row => !row.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.OrderFilled));
        aggregate.RecordFundTradeClosing(retry).Order.Status.Should().Be(nameof(FundCompositionState.Closed));
        var restored = new PortfolioFundCompositionAggregate();
        restored.Restore(aggregate.CaptureState());
        restored.ReservationForOrder(10).Order.Status.Should().Be(nameof(FundCompositionState.Closed));
        restored.ReservationForOrder(10).Trades.Should().OnlyContain(row => row.TradeState == nameof(TradeState.Closed));
    }

    [Theory]
    [InlineData(null, FundCompositionState.Cancelled)]
    [InlineData("Rejected", FundCompositionState.Rejected)]
    public void Removing_a_terminal_unfilled_opening_resets_the_order_to_draft(string? rejection,
        FundCompositionState expected)
    {
        var (aggregate, opening) = Fixture(false);
        aggregate.RecordFundTradeSubmission(opening);
        var terminal = aggregate.ReleaseFundTradeSubmission(opening with { RejectionDetail = rejection });
        terminal.Order.Status.Should().Be(expected.ToString());
        aggregate.RemoveManualTrade(new() { PortfolioId = 1, FundId = 2, OrderId = 10, TradeId = 20,
            ExpectedOrderVersion = terminal.AggregateVersion, RequestedAtUtc = Now }).Order.Status
            .Should().Be(nameof(FundCompositionState.Draft));
    }

    [Fact]
    public void A_working_partial_close_remains_closing_until_terminal_cancellation()
    {
        var (aggregate, opening) = Fixture();
        aggregate.RecordFundTradeOpening(opening);
        var close = opening with { SetupTrade = new() { OrderId = 10, TradeId = 21 }, ClosingExecution = true,
            OpeningExecutionTradeId = opening.ExecutionTradeId, ExecutionOrderId = 31, ExecutionAttemptId = Guid.NewGuid() };
        aggregate.RecordFundTradeSubmission(close);
        var progress = close with { ExecutionStatus = "PartiallyFilled" };
        aggregate.RecordFundTradeSubmission(progress).Order.Status.Should().Be(nameof(FundCompositionState.Closing));
        aggregate.RecordFundTradeClosing(close with { ExecutionStatus = "Cancelled" }).Order.Status
            .Should().Be(nameof(FundCompositionState.Opened));
        aggregate.RecordFundTradeSubmission(progress).Order.Status.Should().Be(nameof(FundCompositionState.Opened));
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
        completed.Order.Status.Should().Be(nameof(FundCompositionState.Closed));
        completed.Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Closed));
        completed.Trades.Single(x => !x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Closed));
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
        partial.Order.Status.Should().Be(nameof(FundCompositionState.Opened));
        partial.Trades.Single(x => x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.Open));
        partial.Trades.Single(x => !x.PrimaryTrade).TradeState.Should().Be(nameof(TradeState.OrderPartiallyFilled));
        var remainder = close with { ExecutionAttemptId = Guid.NewGuid(), ExecutionOrderId = 32, FullyClosed = true };
        aggregate.RecordFundTradeSubmission(remainder);
        aggregate.RecordFundTradeClosing(remainder).Order.Status.Should().Be(nameof(FundCompositionState.Closed));
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
