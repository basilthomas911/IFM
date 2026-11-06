using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Owns the pure transition decisions for each concrete exit-workflow command.</summary>
public static class ExitWorkflowLifecycleHandlers
{
    extension(RecordExitOrderCompositionCommand command)
    {
        /// <summary>Accepts exact composition evidence once and preserves it in the next full snapshot.</summary>
        public ServiceResult<GuidResult> Execute<TState>(ExitPositionWorkflowCommandStateBase<TState> state)
            where TState : ExitPositionWorkflowCommandStateBase<TState>
        {
            if (state.Snapshot is not { } current || command.Receipt is not { } receipt)
                return Reject(command, "EXIT.COMPOSITION.EVIDENCE_MISSING");
            if (receipt.Id == Guid.Empty || receipt.EntityId != command.EntityId ||
                receipt.CommandId != ExitWorkflowLifecycleIdentity.Function(command.EntityId, ComposeExitOrderCommand.Verb) ||
                receipt.Composition is not { } composition || composition.WorkflowId != command.EntityId ||
                composition.StrategyKind != current.Projection.StrategyKind ||
                composition.Position.Id != current.Started.ExitPlan.Position.Id ||
                composition.ExitAction != current.Started.ExitPlan.Action ||
                string.IsNullOrWhiteSpace(composition.CompositionHash) ||
                !string.Equals(receipt.RequestFingerprint, ExitWorkflowLifecycleIdentity.CompositionFingerprint(current.Started), StringComparison.Ordinal))
                return Reject(command, "EXIT.COMPOSITION.EVIDENCE_MISMATCH");
            if (current.CompositionReceiptId == receipt.Id)
                return Accept(command);
            if (!MayAdvance(command, state, ExitPositionWorkflowState.Started))
                return Reject(command, "EXIT.WORKFLOW.PREDECESSOR_MISMATCH");
            var projection = Next(current, ExitPositionWorkflowState.OrderComposed, receipt.ReceivedOn) with { Composition = composition };
            return Apply(command, state, new ExitPositionWorkflowOrderComposedEvent(),
                current with { Projection = projection, CompositionReceiptId = receipt.Id });
        }
    }

    extension(RecordExitRiskDecisionCommand command)
    {
        /// <summary>Accepts the exact Portfolio risk receipt for the previously committed composition.</summary>
        public ServiceResult<GuidResult> Execute<TState>(ExitPositionWorkflowCommandStateBase<TState> state)
            where TState : ExitPositionWorkflowCommandStateBase<TState>
        {
            if (state.Snapshot is not { Projection.Composition: { } composition } current ||
                command.Receipt is not { Decision: { } decision } receipt)
                return Reject(command, "EXIT.RISK.EVIDENCE_MISSING");
            if (receipt.Id == Guid.Empty || receipt.EntityId != command.EntityId ||
                receipt.CommandId != ExitWorkflowLifecycleIdentity.Function(command.EntityId, EvaluatePositionExitRiskCommand.Verb) ||
                !string.Equals(receipt.RequestFingerprint, ExitWorkflowLifecycleIdentity.RiskFingerprint(composition, current.CompositionReceiptId), StringComparison.Ordinal) ||
                decision.ExecuteTradeOrder && (decision.TradeOrder is null || decision.PortfolioCompletedEventId == Guid.Empty))
                return Reject(command, "EXIT.RISK.EVIDENCE_MISMATCH");
            if (current.RiskReceiptId == receipt.Id)
                return Accept(command);
            if (!MayAdvance(command, state, ExitPositionWorkflowState.OrderComposed))
                return Reject(command, "EXIT.WORKFLOW.PREDECESSOR_MISMATCH");
            var next = decision.ExecuteTradeOrder ? ExitPositionWorkflowState.RiskAccepted : ExitPositionWorkflowState.NoTradeOrders;
            var projection = Next(current, next, receipt.ReceivedOn) with { RiskDecision = decision };
            return Apply(command, state, new ExitPositionWorkflowRiskRecordedEvent(),
                current with { Projection = projection, RiskReceiptId = receipt.Id });
        }
    }

    extension(CompleteExitPositionWorkflowCommand command)
    {
        /// <summary>Records successful Trade Order handoff once, using its stable execution and bind identities.</summary>
        public ServiceResult<GuidResult> Execute<TState>(ExitPositionWorkflowCommandStateBase<TState> state)
            where TState : ExitPositionWorkflowCommandStateBase<TState>
        {
            if (state.Snapshot is not { Projection.RiskDecision: { ExecuteTradeOrder: true, TradeOrder: not null } decision } current ||
                command.ExecutionAttemptId != ExitWorkflowLifecycleIdentity.OrderHandoff(decision, "execution") ||
                command.BindingCommandId != ExitWorkflowLifecycleIdentity.OrderHandoff(decision, "bind"))
                return Reject(command, "EXIT.DISPATCH.EVIDENCE_MISMATCH");
            if (current.Projection.State == ExitPositionWorkflowState.Completed && current.BindingCommandId == command.BindingCommandId)
                return Accept(command);
            if (!MayAdvance(command, state, ExitPositionWorkflowState.RiskAccepted))
                return Reject(command, "EXIT.WORKFLOW.PREDECESSOR_MISMATCH");
            return Apply(command, state, new ExitPositionWorkflowCompletedEvent(), current with
            {
                Projection = Next(current, ExitPositionWorkflowState.Completed, current.Projection.UpdatedAtUtc),
                ExecutionAttemptId = command.ExecutionAttemptId,
                BindingCommandId = command.BindingCommandId
            });
        }
    }

    extension(FailExitPositionWorkflowCommand command)
    {
        /// <summary>Records a terminal failure against its exact accepted predecessor.</summary>
        public ServiceResult<GuidResult> Execute<TState>(ExitPositionWorkflowCommandStateBase<TState> state)
            where TState : ExitPositionWorkflowCommandStateBase<TState>
        {
            if (state.Snapshot is not { } current || string.IsNullOrWhiteSpace(command.ReasonCode))
                return Reject(command, "EXIT.FAILURE.EVIDENCE_MISSING");
            if (current.Projection.State == ExitPositionWorkflowState.Failed)
                return string.Equals(current.FailureCode, command.ReasonCode, StringComparison.Ordinal) &&
                    string.Equals(current.FailureDetail, command.Detail, StringComparison.Ordinal)
                    ? Accept(command) : Reject(command, "EXIT.FAILURE.EVIDENCE_MISMATCH");
            if (current.Projection.State is not (ExitPositionWorkflowState.Started or ExitPositionWorkflowState.OrderComposed or ExitPositionWorkflowState.RiskAccepted) ||
                !MayAdvance(command, state, current.Projection.State))
                return Reject(command, "EXIT.WORKFLOW.PREDECESSOR_MISMATCH");
            return Apply(command, state, new ExitPositionWorkflowTerminatedEvent(), current with
            {
                Projection = Next(current, ExitPositionWorkflowState.Failed, current.Projection.UpdatedAtUtc),
                FailureCode = command.ReasonCode,
                FailureDetail = command.Detail
            });
        }
    }

    internal static List<ValidationError> Validate(ExitPositionWorkflowLifecycleCommand command, string actor, string verb)
        => command.EntityId.IsValid && command.PredecessorEventId != Guid.Empty && command.ExpectedRevision > 0 &&
            command.Subject == new ActorSubject(ActorType.Command, actor, verb, command.EntityId.Format()) &&
            command.CommandId == ExitWorkflowLifecycleIdentity.Transition(command.EntityId, verb, command.PredecessorEventId)
            ? [] : [new ValidationError("Invalid exit workflow transition identity, route or predecessor.")];

    static bool MayAdvance<TState>(ExitPositionWorkflowLifecycleCommand command, ExitPositionWorkflowCommandStateBase<TState> state,
        ExitPositionWorkflowState expected) where TState : ExitPositionWorkflowCommandStateBase<TState> =>
        state.Snapshot is { Projection: { } projection } && projection.WorkflowId == command.EntityId &&
        projection.StageRevision == command.ExpectedRevision && projection.State == expected && state.CurrentSourceEventId == command.PredecessorEventId;

    static ExitPositionWorkflowProjection Next(ExitPositionWorkflowSnapshot snapshot, ExitPositionWorkflowState state, DateTime atUtc)
        => snapshot.Projection with { State = state, StageRevision = snapshot.Projection.StageRevision + 1, UpdatedAtUtc = DateTime.SpecifyKind(atUtc, DateTimeKind.Utc) };

    static ServiceResult<GuidResult> Apply<TState>(ExitPositionWorkflowLifecycleCommand command, ExitPositionWorkflowCommandStateBase<TState> state,
        ExitPositionWorkflowLifecycleEvent source, ExitPositionWorkflowSnapshot snapshot) where TState : ExitPositionWorkflowCommandStateBase<TState> =>
        state.Update(source with
        {
            Id = Guid.NewGuid(), ReceivedOn = snapshot.Projection.UpdatedAtUtc,
            EntityId = command.EntityId, CommandId = command.CommandId, Snapshot = snapshot, AggregateId = command.EntityId.Format(),
            Subject = new(ActorType.Event, command.Subject.Name + "Event", source.EventName, command.EntityId.Format())
        }, command) ? Accept(command) : Reject(command, "EXIT.WORKFLOW.APPLY_FAILED");

    static ServiceResult<GuidResult> Accept(ICommand command) => new ServiceOk<GuidResult>(new(command.CommandId));
    static ServiceResult<GuidResult> Reject(ICommand command, string error) => new ServiceFailed<GuidResult>(command.ErrorCode, error);
}
