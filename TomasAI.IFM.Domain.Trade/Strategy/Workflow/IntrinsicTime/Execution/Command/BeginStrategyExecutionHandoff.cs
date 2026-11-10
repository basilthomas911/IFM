using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;

/// <summary>Validates a new opening intent and accepts matching retries without replacing business identities.</summary>
public static class BeginStrategyExecutionHandoff
{
    extension(BeginStrategyExecutionHandoffCommand command)
    {
        /// <summary>Applies only a complete authorized emulator intent; replay does not allocate or dispatch.</summary>
        public ServiceResult<GuidResult> Execute(StrategyExecutionHandoffCommandState state, DateTime now)
        {
            var proposed = command.Intent;
            if (state.Definition is { } current)
                return current.IntentHash == proposed.IntentHash && current.Id == command.EntityId && Fingerprint(proposed) == current.IntentHash
                    ? new ServiceOk<GuidResult>(new(command.CommandId))
                    : new ServiceFailed<GuidResult>(command.ErrorCode, "EXECUTION.INTENT.CONFLICT");
            if (!command.EntityId.IsValid || proposed.Id != command.EntityId || proposed.Order.Id != command.EntityId.TradeOrder ||
                proposed.Authorization.Id == Guid.Empty || proposed.Authorization.EventId <= 0 ||
                proposed.Authorization.Route is not { Environment: BrokerEnvironment.Emulator } route ||
                proposed.Authorization.StateHash.Length != 64 || proposed.ConsumptionRequest is not null ||
                proposed.Revision != 0 || proposed.Execution is not null || proposed.FinancialReceipt is not null ||
                proposed.FundEvidence is not null || proposed.Consumption is not null || proposed.ExecutionRequestedAtUtc is not null ||
                proposed.UpdatedAtUtc != default || !string.IsNullOrEmpty(proposed.ReconciliationReason) ||
                route.AccountAlias != proposed.Order.BrokerAccountAlias || proposed.Order.BrokerEnvironment != BrokerEnvironment.Emulator ||
                proposed.Order.PositionType != TradeOrderPositionType.Opening ||
                proposed.IntentHash != Fingerprint(proposed) || proposed.Phase != StrategyExecutionHandoffPhase.Accepted)
                return new ServiceFailed<GuidResult>(command.ErrorCode, "EXECUTION.INTENT.INVALID_OR_EXPIRED");
            var next = proposed with { Revision = 1, UpdatedAtUtc = now };
            return state.Update(Snapshot(command, next, now), command)
                ? new ServiceOk<GuidResult>(new(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode, "EXECUTION.STATE.APPLY_FAILED");
        }
    }

    /// <summary>Includes immutable source ownership, order and original capacity request in duplicate detection.</summary>
    public static string Fingerprint(StrategyExecutionHandoffDefinition value) => FinancialCanonicalHash.Compute(new
    {
        value.Id, SourceId = value.Authorization.Id, value.Authorization.WorkflowId,
        value.Authorization, value.Order, value.ConsumptionRequest
    });

    /// <summary>Creates a complete source event after the caller has validated the transition.</summary>
    internal static StrategyExecutionHandoffStateSnapshottedEvent Snapshot(ICommand<OrderExecutionId> command,
        StrategyExecutionHandoffDefinition next, DateTime now) => new()
    {
        Id = Guid.NewGuid(), CommandId = command.CommandId, EntityId = command.EntityId,
        Subject = new(ActorType.Event, BeginStrategyExecutionHandoffCommand.Actor, "StateSnapshotted", command.EntityId.Format()),
        ReceivedOn = now, Definition = next
    };
}
