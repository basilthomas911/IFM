using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Applies one authoritative normalized broker observation to its durable broker-order stream.</summary>
public static class RecordBrokerOrderObservation
{
    /// <summary>Ignores exact redelivery, rejects changed-content reuse, and records a material observation.</summary>
    public static ServiceResult<GuidResult> Execute(
        this RecordBrokerOrderObservationCommand command,
        BrokerOrderCommandState state)
    {
        var current = state.Current;
        if (current is null)
            return command.UpdateFailed("BO.NOT_FOUND");

        var observation = command.Observation;
        if (state.TryGetObservationHash(observation.ObservationId, out var priorHash))
            return string.Equals(priorHash, observation.ContentHash, StringComparison.Ordinal)
                ? new ServiceOk<GuidResult>(new(command.CommandId))
                : command.UpdateFailed("BO.OBSERVATION_ID_CONFLICT");

        if (!string.Equals(current.Order.BrokerAccountAlias, observation.AccountAlias, StringComparison.Ordinal) ||
            observation.ComponentId != command.EntityId.ComponentId)
            return command.UpdateFailed("BO.OBSERVATION_CORRELATION_FAILED");

        var isLateExecutionFact = observation.Kind is BrokerOrderObservationKind.Execution or
            BrokerOrderObservationKind.Commission or BrokerOrderObservationKind.OrderCompleted;
        if (observation.OperationId != Guid.Empty && observation.OperationId != current.OperationId &&
            (!isLateExecutionFact || observation.OperationId != current.PriorOperationId))
            return command.UpdateFailed("BO.OBSERVATION_OPERATION_CONFLICT");

        var nextStatus = ResolveStatus(current.Status, current.StatusBeforeMutation, observation.Kind);
        if (nextStatus == BrokerOrderStatus.Unknown)
            return command.UpdateFailed($"BO.OBSERVATION_INVALID_STATE;{current.Status};{observation.Kind}");

        var next = current with
        {
            Status = nextStatus,
            Revision = current.Revision + 1,
            BrokerRevision = Math.Max(current.BrokerRevision, observation.OrderRevision),
            LastObservation = observation,
            ChangedAtUtc = observation.OccurredAtUtc
        };
        var applied = state.Update(new BrokerOrderChangedEvent
        {
            Subject = new(ActorType.Event, BrokerOrderChangedEvent.Actor,
                BrokerOrderChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            State = next
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : command.UpdateFailed("BO.STATE.APPLY_FAILED");
    }

    private static BrokerOrderStatus ResolveStatus(
        BrokerOrderStatus current,
        BrokerOrderStatus statusBeforeMutation,
        BrokerOrderObservationKind observation) => observation switch
        {
            BrokerOrderObservationKind.Acknowledged when current is BrokerOrderStatus.PlacePending or BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.OutcomeUnknown or BrokerOrderStatus.UpdatePending =>
                current == BrokerOrderStatus.UpdatePending && statusBeforeMutation == BrokerOrderStatus.PartiallyFilled
                    ? BrokerOrderStatus.PartiallyFilled
                    : BrokerOrderStatus.Working,
            BrokerOrderObservationKind.Acknowledged when current == BrokerOrderStatus.PartiallyFilled =>
                BrokerOrderStatus.PartiallyFilled,
            BrokerOrderObservationKind.Execution when current is BrokerOrderStatus.PlacePending or BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.UpdatePending or BrokerOrderStatus.CancelPending or BrokerOrderStatus.Cancelled =>
                BrokerOrderStatus.PartiallyFilled,
            BrokerOrderObservationKind.Commission when current is BrokerOrderStatus.PlacePending or BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.Filled or BrokerOrderStatus.UpdatePending or BrokerOrderStatus.CancelPending or BrokerOrderStatus.Cancelled => current,
            BrokerOrderObservationKind.OrderCompleted when current is BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.CancelPending or BrokerOrderStatus.UpdatePending =>
                BrokerOrderStatus.Filled,
            BrokerOrderObservationKind.Cancelled when current is BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.CancelPending =>
                BrokerOrderStatus.Cancelled,
            BrokerOrderObservationKind.Rejected when current is BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.OutcomeUnknown =>
                BrokerOrderStatus.Rejected,
            _ => BrokerOrderStatus.Unknown
        };
}
