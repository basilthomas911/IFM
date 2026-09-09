using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

[Trait("Category", "PortfolioFinancial")]
public sealed class RiskResizingTests
{
    [Fact]
    public async Task Changed_capacity_before_reservation_can_resize_and_zero_remaining_capacity_rejects()
    {
        var (view, financial, at) = await Input();
        view = view with { FinancialHandoff = null };
        RiskResizing.Changed(view.RiskExecution!, RiskResizing.Authority(view.RiskExecution!, financial, at)).Should().BeTrue();
        financial = financial with { Value = financial.Value! with { AvailableCash = 0 } };
        var next = RiskResizing.Next(view, financial, at);
        next.RiskResize!.PreviousReservationOperationId.Should().BeEmpty();
        var result = new RiskEvaluator().Calculate(next.RiskExecution!);
        result.Outcome.Should().Be(RiskAssessmentOutcome.Rejected);
        result.StrategyUnits.Should().Be(0);
        next.RiskExecution!.ExpiresAtUtc.Should().Be(view.RiskExecution!.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Terminal_capacity_refusal_prompts_authoritative_reconciliation(bool serviceFailure)
    {
        var (view, _, _) = await Input();
        var api = Substitute.For<IPortfolioFinancialApi>();
        api.ReserveAsync(Arg.Any<ReservePortfolioTradeRiskCommand>(), Arg.Any<CancellationToken>())
            .Returns(serviceFailure ? new ServiceFailed<FunctionResult<CapacityReservationCompletedEvent, CapacityReservationFailedEvent>>(
                FinancialReasons.RevisionConflict, "Financial revision changed") :
                new ServiceOk<FunctionResult<CapacityReservationCompletedEvent, CapacityReservationFailedEvent>>(
                FunctionResult<CapacityReservationCompletedEvent, CapacityReservationFailedEvent>.Fail(
                    new() { ErrorCode = FinancialReasons.RevisionConflict })));
        var context = Substitute.For<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor.IIntrinsicTimeStrategyWorkflowRealtimeContext>();
        context.FinancialApi.Returns(api);
        await TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.ExecuteRiskFinancialHandoff
            .ExecuteFinancialHandoffAsync(view, context);
        context.ReceivedCalls().Count(x => x.GetMethodInfo().Name == "SendAsync").Should().Be(1);
        view.RiskExecution!.EntityId.AttemptOrdinal.Should().Be(1);
    }

    [Fact]
    public async Task Fenced_contention_prepares_smaller_independent_attempt_without_extending_evidence()
    {
        var (view, financial, at) = await Input();
        var old = view.RiskExecution!;
        var oldBytes = MessagePackBinarySerializer.Shared.Serialize(view);
        var next = RiskResizing.Next(view, financial, at, Absent(financial));
        next.RiskExecution!.EntityId.AttemptOrdinal.Should().Be(2);
        next.RiskExecution.CommandId.Should().NotBe(old.CommandId);
        next.RiskExecution.InputSha256.Should().Be(next.RiskExecution.Fingerprint());
        next.RiskExecution.ExpiresAtUtc.Should().Be(old.ExpiresAtUtc);
        next.RiskExecution.PolicyHash.Should().Be(old.PolicyHash);
        next.RiskExecution.ConfigurationPayloadSha256.Should().Be(old.ConfigurationPayloadSha256);
        next.RiskExecution.Funding.Should().Equal(old.Funding);
        next.RiskExecution.CompositionResult.Should().BeSameAs(old.CompositionResult);
        next.RiskExecution.MarketSnapshot.Should().BeSameAs(old.MarketSnapshot);
        next.FinancialHandoff.Should().BeNull();
        next.RiskManagement.Result.Should().BeNull();
        var calculated = new RiskEvaluator().Calculate(next.RiskExecution);
        calculated.StrategyUnits.Should().Be(2);
        calculated.OrderId.Should().Be(view.RiskManagement.Result!.RiskResult!.OrderId);
        MessagePackBinarySerializer.Shared.Serialize(view).Should().Equal(oldBytes);
        var replay = RiskResizing.Next(view, financial, at, Absent(financial));
        replay.RiskExecution.InputSha256.Should().Be(next.RiskExecution.InputSha256);
        var wire = MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(MessagePackBinarySerializer.Shared.Serialize(next))!;
        wire.RiskExecution!.InputSha256.Should().Be(next.RiskExecution.InputSha256);
        wire.RiskResize!.PreviousInvocationId.Should().Be(old.CommandId);
        wire.RiskResize.PreviousReservationOperationId.Should().Be(view.FinancialHandoff!.ReservationRequest.OperationId);
        wire.RiskResize.AbsenceFinancialRevision.Should().Be(2);
    }

    [Theory]
    [InlineData(FinancialReadStatus.NotFound, 1)]
    [InlineData(FinancialReadStatus.Unavailable, 2)]
    [InlineData(FinancialReadStatus.Unknown, 2)]
    [InlineData(FinancialReadStatus.Found, 2)]
    public async Task Missing_or_uncertain_receipt_alone_cannot_replace_original_request(FinancialReadStatus status, long revision)
    {
        var (view, financial, at) = await Input();
        var receipt = new FinancialRead<FinancialOperationOutcome>(status, null, revision, at);
        Action act = () => RiskResizing.Next(view, financial, at, receipt);
        act.Should().Throw<RiskCalculationException>().Which.ReasonCode.Should().Be("RM.RESIZE.RESERVATION_UNRESOLVED");
    }

    [Theory]
    [InlineData(RiskFinancialHandoffPhase.FundPending)]
    [InlineData(RiskFinancialHandoffPhase.Authorized)]
    [InlineData(RiskFinancialHandoffPhase.Consumed)]
    [InlineData(RiskFinancialHandoffPhase.Submitted)]
    public async Task Existing_authorization_or_execution_cannot_be_resized(RiskFinancialHandoffPhase phase)
    {
        var (view, financial, at) = await Input();
        view = view with { FinancialHandoff = view.FinancialHandoff! with { Phase = phase } };
        Action act = () => RiskResizing.Next(view, financial, at, Absent(financial));
        act.Should().Throw<RiskCalculationException>();
    }

    [Fact]
    public async Task Third_contention_stops_without_a_fourth_invocation()
    {
        var (view, financial, at) = await Input();
        for (var attempt = 2; attempt <= 3; attempt++)
        {
            view = RiskResizing.Next(view, financial, at, Absent(financial));
            view = Complete(view);
            view.RiskExecution!.EntityId.AttemptOrdinal.Should().Be(attempt);
            var reserve = RiskFinancialHandoff.Reserve(view, view.RiskManagement.Result!.RiskResult!, financial, at);
            view = view with { FinancialHandoff = new() { Phase = RiskFinancialHandoffPhase.ReservePending, ReservationRequest = reserve } };
            financial = financial with { FinancialRevision = financial.FinancialRevision + 1 };
        }
        var stopped = RiskResizing.Next(view, financial, at, Absent(financial));
        stopped.Status.Should().Be(WorkflowStrategyMachineStatus.Completed);
        stopped.Outcome.Should().Be(StrategyWorkflowOutcome.NoTrade);
        stopped.StopReasonCode.Should().Be("RM.CAPACITY.CONTENTION_EXHAUSTED");
        stopped.RiskExecution.Should().BeSameAs(view.RiskExecution);
    }

    [Fact]
    public async Task Expired_evidence_stops_and_revoked_authority_never_gets_refreshed()
    {
        var (view, financial, at) = await Input();
        var expired = view.RiskExecution!.ExpiresAtUtc;
        RiskResizing.Next(view, financial with { ObservedAtUtc = expired }, expired,
            Absent(financial)).Outcome.Should().Be(StrategyWorkflowOutcome.TimedOut);
        financial = financial with { Value = financial.Value! with { Authority = financial.Value.Authority with { AuthorityEpoch = 2 } } };
        Action act = () => RiskResizing.Next(view, financial, at, Absent(financial));
        act.Should().Throw<RiskCalculationException>().Which.ReasonCode.Should().Be("RM.RESIZE.AUTHORITY");
    }

    [Fact]
    public async Task Command_commits_resized_invocation_and_duplicate_old_advance_is_ignored()
    {
        var (view, financial, at) = await Input();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(new WorkflowStrategyStateUpdatedEvent { State = view, EntityId = view.EntityId,
            WorkflowId = view.WorkflowId, WorkflowRevision = view.WorkflowRevision }, false);
        var api = Substitute.For<IPortfolioFinancialApi>();
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(), Arg.Any<GetPostingReceiptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FinancialRead<FinancialOperationOutcome>>(Absent(financial)));
        api.GetFinancialAdmissionSnapshotAsync(Arg.Any<FinancialReadScope>(), Arg.Any<GetFinancialAdmissionSnapshotRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FinancialRead<FinancialAdmissionSnapshot>>(financial));
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.FinancialApi.Returns(api); context.TimeProvider.Returns(new FixedClock(at));
        var command = RiskFinancialHandoff.Advance(view);
        await command.ExecuteAsync(context, state);
        state.CurrentView!.RiskExecution!.EntityId.AttemptOrdinal.Should().Be(2);
        var saved = MessagePackBinarySerializer.Shared.Serialize(state.CurrentView);
        await command.ExecuteAsync(context, state);
        MessagePackBinarySerializer.Shared.Serialize(state.CurrentView).Should().Equal(saved);
    }

    static FinancialRead<FinancialOperationOutcome> Absent(FinancialRead<FinancialAdmissionSnapshot> financial)
        => new(FinancialReadStatus.NotFound, null, financial.FinancialRevision, financial.ObservedAtUtc);

    static IntrinsicTimeStrategyWorkflowView Complete(IntrinsicTimeStrategyWorkflowView view)
    {
        var result = new RiskEvaluator().Calculate(view.RiskExecution!);
        result.Outcome.Should().Be(RiskAssessmentOutcome.Approved);
        return view with { WorkflowRevision = view.WorkflowRevision + 1, RiskManagement = view.RiskManagement with
        { ProcessingStatus = StrategyActorProcessingStatus.Completed, Result = StrategyStageResultEnvelope.CreateRisk(result), SourceEventId = result.InvocationId } };
    }

    static async Task<(IntrinsicTimeStrategyWorkflowView View, FinancialRead<FinancialAdmissionSnapshot> Financial, DateTime At)> Input()
    {
        var input = await RiskPreparationTests.Input();
        var request = RiskPreparation.Create(input.View, input.Policy, input.Financial, Guid.NewGuid(), input.At);
        var view = Complete(input.View with { RiskExecution = request, WorkflowRevision = request.InputWorkflowRevision });
        var result = view.RiskManagement.Result!.RiskResult!;
        result.StrategyUnits.Should().Be(10);
        var reserve = RiskFinancialHandoff.Reserve(view, result, input.Financial, input.At);
        view = view with { FinancialHandoff = new() { Phase = RiskFinancialHandoffPhase.ReservePending, ReservationRequest = reserve } };
        var financial = input.Financial with { FinancialRevision = 2, Value = input.Financial.Value! with
        {
            Usage = input.Financial.Value.Limits.Where(x => x.Measure == CapacityMeasure.GrossContracts)
                .Select(x => new CapacityUsed(x.ScopeKind, x.ScopeKey, x.Measure, x.Unit, x.Maximum - 2, 0, 0)).ToArray()
        } };
        return (view, financial, input.At);
    }

    sealed class FixedClock(DateTime now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(now); }
}
