using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Creates a new bounded invocation only after the previous reservation is absent and fenced out.</summary>
public static class RiskResizing
{
    public const int MaximumAttempts = 3;

    public static bool IsFencedOut(ReservePortfolioTradeRiskCommand request, FinancialRead<FinancialOperationOutcome> receipt)
        // FinancialQueryStore reads the revision and receipt under the same FOR SHARE lock.
        // CapacityReservationStore checks that revision under its exclusive lock before any mutation.
        => request.ExpectedFinancialRevision > 0 && receipt.Status == FinancialReadStatus.NotFound
            && receipt.Value is null && receipt.FinancialRevision > request.ExpectedFinancialRevision;

    public static RiskSizingAuthority Authority(ExecuteRiskManagementPipelineCommand previous,
        FinancialRead<FinancialAdmissionSnapshot> read, DateTime now)
    {
        var financial = read.Value;
        RiskUnitModel.Require(now.Kind == DateTimeKind.Utc && read.Status == FinancialReadStatus.Found
            && read.FinancialRevision > 0 && read.ObservedAtUtc.Kind == DateTimeKind.Utc
            && read.ObservedAtUtc <= now && now - read.ObservedAtUtc <= TimeSpan.FromSeconds(1)
            && financial is { CanPrepareAdmission: true, MigrationQualified: true, OperatingState: "Active" }
            && financial.PortfolioId == previous.SizingAuthority.PortfolioId
            && financial.FundId == previous.SizingAuthority.FundId && financial.Authority == previous.Authority
            && financial.Environment == previous.SizingAuthority.Environment, "RM.RESIZE.AUTHORITY");
        var loss = financial!.Limits.SingleOrDefault(x => x.ScopeKind == CapacityScopeKind.Fund
            && x.ScopeKey == FinancialScopeKeys.Fund(financial.FundId)
            && x.Measure == CapacityMeasure.LossCharge && x.Unit == CapacityUnit.Usd);
        RiskUnitModel.Require(loss is { Enabled: true } && financial.MaximumRiskPerTrade > 0, "RM.AUTHORITY.LIMIT_MISSING");
        return previous.SizingAuthority with
        {
            AvailableCash = financial.AvailableCash, RiskCapital = Math.Max(0, financial.AvailableCash),
            PerTradeLossBudget = Math.Min(loss!.Maximum, financial.MaximumRiskPerTrade),
            Limits = financial.Limits.ToImmutableArray(), Usage = financial.Usage.ToImmutableArray(), EvaluatedAtUtc = now
        };
    }

    public static bool Changed(ExecuteRiskManagementPipelineCommand previous, RiskSizingAuthority current)
        => RiskContracts.Hash(previous.SizingAuthority) !=
            RiskContracts.Hash(current with { EvaluatedAtUtc = previous.SizingAuthority.EvaluatedAtUtc });

    public static IntrinsicTimeStrategyWorkflowView Next(IntrinsicTimeStrategyWorkflowView view,
        FinancialRead<FinancialAdmissionSnapshot> read, DateTime now,
        FinancialRead<FinancialOperationOutcome>? absence = null)
    {
        var previous = view.RiskExecution ?? throw new RiskCalculationException("RM.RESIZE.NO_INVOCATION");
        RiskUnitModel.Require(view is { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.RiskManagement }
            && view.RiskManagement.ProcessingStatus == StrategyActorProcessingStatus.Completed
            && view.RiskManagement.Result?.RiskResult is { Outcome: RiskAssessmentOutcome.Approved } result
            && result.InvocationId == previous.CommandId && previous.InputSha256 == previous.Fingerprint(), "RM.RESIZE.IDENTITY");
        if (view.FinancialHandoff is { } pending)
            RiskUnitModel.Require(pending.Phase == RiskFinancialHandoffPhase.ReservePending
                && pending.Reservation is null && pending.Authorization is null && absence is not null
                && pending.ReservationRequest.Body.RiskInvocationId == previous.CommandId
                && IsFencedOut(pending.ReservationRequest, absence)
                && read.FinancialRevision >= absence.FinancialRevision
                && read.Value?.BookId == pending.ReservationRequest.Body.BookId, "RM.RESIZE.RESERVATION_UNRESOLVED");
        var authority = Authority(previous, read, now);
        view = view with { RiskResize = new(previous.CommandId,
            view.FinancialHandoff?.ReservationRequest.OperationId ?? Guid.Empty,
            view.FinancialHandoff?.ReservationRequest.ExpectedFinancialRevision ?? 0,
            absence?.FinancialRevision ?? 0, read.FinancialRevision, now) };
        var candidate = previous.CompositionResult.ReadCompositionResult().Candidate!;
        // Re-sizing never buys a new quote lifetime, a new authority epoch, or a later deadline.
        if (now >= previous.ExpiresAtUtc || now >= view.ExpiresAtUtc
            || !RiskLatency.WithinAgeLimit(previous.SizingAuthority.Environment, now, candidate.EvaluatedAtUtc))
            return Stop(view, now, true, "RM.TIME.EXPIRED");
        if (previous.EntityId.AttemptOrdinal >= MaximumAttempts)
            return Stop(view, now, false, "RM.CAPACITY.CONTENTION_EXHAUSTED");
        RiskUnitModel.Require(previous.EntityId.AttemptOrdinal > 0, "RM.RESIZE.IDENTITY");
        var revision = checked(view.WorkflowRevision + 1);
        var entity = previous.EntityId with { InputWorkflowRevision = revision, AttemptOrdinal = previous.EntityId.AttemptOrdinal + 1 };
        var request = previous with
        {
            CommandId = RiskFinancialHandoff.Identity(previous.CommandId, $"Resize/{entity.AttemptOrdinal}"),
            EntityId = entity, Subject = new(ActorType.Function, ExecuteRiskManagementPipelineCommand.Actor,
                ExecuteRiskManagementPipelineCommand.Verb, entity.Format()),
            InputWorkflowRevision = revision, RequestedAtUtc = now, EvaluatedAtUtc = now,
            CausationId = previous.CommandId, SizingAuthority = authority
        };
        request = request with { InputSha256 = request.Fingerprint() };
        RiskUnitModel.Require(new List<ValidationError>().ValidateRiskFields(request).Count == 0, "RM.RESIZE.INVALID");
        // Earlier invocations, results and requests remain in their original committed workflow snapshots.
        return view with
        {
            WorkflowRevision = revision, UpdatedAtUtc = now, RiskExecution = request, FinancialHandoff = null, RiskExplanation = null,
            RiskManagement = view.RiskManagement with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing, ContinuationDecision = StrategyWorkflowContinuationDecision.None,
                Result = null, SourceEventId = Guid.Empty, CompletedAtUtc = null, FailedAtUtc = null, Failure = null,
                InputWorkflowRevision = revision, ExpiresAtUtc = request.ExpiresAtUtc, ContinuationReasonCodes = []
            }
        };
    }

    static IntrinsicTimeStrategyWorkflowView Stop(IntrinsicTimeStrategyWorkflowView view, DateTime now, bool expired, string reason)
        => view with
        {
            WorkflowRevision = checked(view.WorkflowRevision + 1), UpdatedAtUtc = now, TerminalAtUtc = now,
            Status = expired ? WorkflowStrategyMachineStatus.TimedOut : WorkflowStrategyMachineStatus.Completed,
            Outcome = expired ? StrategyWorkflowOutcome.TimedOut : StrategyWorkflowOutcome.NoTrade, StopReasonCode = reason,
            RiskManagement = view.RiskManagement with { ContinuationDecision = StrategyWorkflowContinuationDecision.Stop,
                ContinuationReasonCodes = [reason],
                ProcessingStatus = expired ? StrategyActorProcessingStatus.TimedOut : view.RiskManagement.ProcessingStatus,
                FailedAtUtc = expired ? now : view.RiskManagement.FailedAtUtc,
                Failure = expired ? new() { ErrorCode = 23103, ErrorType = "RiskManagementTimedOut",
                    ErrorMessage = reason, FailedAtUtc = now } : view.RiskManagement.Failure }
        };
}
