using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Model;
/// <summary>Pure broker-order decisions; authoritative changes occur only in State.Apply.</summary>
internal static class BrokerOrderComputation
{
    /// <summary>Computes the CreateBrokerOrder decision without mutating state or performing broker effects.</summary>
    internal static BrokerOrderCompute CreateBrokerOrder(CreateBrokerOrderCommand command, BrokerOrderDefinition? brokerOrderDefinition)
    {
        if (brokerOrderDefinition is not null)
            return brokerOrderDefinition.OperationId == command.OperationId ? BrokerOrderCompute.Replay(brokerOrderDefinition!) : BrokerOrderCompute.Reject("BrokerOrder.CREATE.CONFLICT; broker order already exists for another operation.");
        if (!BrokerOrderRequestMapper.TryCreate(command.Order, command.EntityId.Execution.ExecutionAttemptId, command.EntityId.ComponentId, command.OperationId, out _, out var reason))
            return BrokerOrderCompute.Reject(reason);
        var proposedBrokerOrderDefinition = new BrokerOrderDefinition
        {
            Id = command.EntityId,
            Order = command.Order,
            OperationId = command.OperationId,
            Status = BrokerOrderStatus.PlacePending,
            Revision = 1,
            ChangedAtUtc = command.EffectiveAtUtc,
            CurrentSignedNetDebitLimit = command.Order.Components.Single(component => component.ComponentId == command.EntityId.ComponentId).SignedNetDebitLimit ?? 0m,
            PendingMutation = BrokerMutationKind.Place,
            StatusBeforeMutation = BrokerOrderStatus.Unknown
        };
        return BrokerOrderCompute.Accept(proposedBrokerOrderDefinition);
    }

    /// <summary>Computes the RecordBrokerDispatch decision without mutating state or performing broker effects.</summary>
    internal static BrokerOrderCompute RecordBrokerDispatch(RecordBrokerDispatchCommand command, BrokerOrderDefinition? brokerOrderDefinition)
    {
        if (brokerOrderDefinition is null)
            return BrokerOrderCompute.Reject("BrokerOrder.NOT_FOUND");
        if (brokerOrderDefinition.OperationId != command.OperationId)
            return BrokerOrderCompute.Reject("BrokerOrder.OPERATION.CONFLICT");
        if (brokerOrderDefinition is { Status: BrokerOrderStatus.Rejected, LastObservation.Kind: BrokerOrderObservationKind.Rejected })
            return BrokerOrderCompute.Replay(brokerOrderDefinition);
        if (brokerOrderDefinition.PendingMutation == BrokerMutationKind.Unknown && string.Equals(brokerOrderDefinition.DispatchCategory, command.Category, StringComparison.Ordinal) && string.Equals(brokerOrderDefinition.DispatchDetail, command.Detail, StringComparison.Ordinal))
            return BrokerOrderCompute.Replay(brokerOrderDefinition!);
        if (brokerOrderDefinition.PendingMutation == BrokerMutationKind.Unknown && brokerOrderDefinition.LastObservation is not null)
            return command.Outcome == BrokerDispatchResult.RejectedLocally
                ? BrokerOrderCompute.Reject("BrokerOrder.DISPATCH.CONFLICTS_WITH_AUTHORITATIVE_OBSERVATION")
                : BrokerOrderCompute.Replay(brokerOrderDefinition);
        var acceptedStatus = brokerOrderDefinition.PendingMutation switch
        {
            BrokerMutationKind.Place => BrokerOrderStatus.Dispatched,
            BrokerMutationKind.UpdateLimit => BrokerOrderStatus.UpdatePending,
            BrokerMutationKind.Cancel => BrokerOrderStatus.CancelPending,
            _ => BrokerOrderStatus.Unknown
        };
        if (acceptedStatus == BrokerOrderStatus.Unknown)
            return BrokerOrderCompute.Reject("BrokerOrder.DISPATCH.MUTATION_UNKNOWN");
        var localRejectionStatus = brokerOrderDefinition.PendingMutation == BrokerMutationKind.Place ? BrokerOrderStatus.Rejected : brokerOrderDefinition.StatusBeforeMutation;
        var receiptStatus = command.Outcome switch
        {
            BrokerDispatchResult.AcceptedForDispatch => acceptedStatus,
            BrokerDispatchResult.OutcomeUnknown => BrokerOrderStatus.OutcomeUnknown,
            _ => localRejectionStatus
        };
        var authoritativeStatusAlreadyAdvanced = brokerOrderDefinition.Status is BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.Filled or BrokerOrderStatus.Cancelled;
        var status = authoritativeStatusAlreadyAdvanced && command.Outcome is not BrokerDispatchResult.RejectedLocally ? brokerOrderDefinition.Status : receiptStatus;
        if (brokerOrderDefinition.Status == status && string.Equals(brokerOrderDefinition.DispatchCategory, command.Category, StringComparison.Ordinal) && string.Equals(brokerOrderDefinition.DispatchDetail, command.Detail, StringComparison.Ordinal))
            return BrokerOrderCompute.Replay(brokerOrderDefinition!);
        if (brokerOrderDefinition.Status is not (BrokerOrderStatus.PlacePending or BrokerOrderStatus.UpdatePending or BrokerOrderStatus.CancelPending) && !authoritativeStatusAlreadyAdvanced)
            return BrokerOrderCompute.Reject($"BrokerOrder.DISPATCH.INVALID_STATE;{brokerOrderDefinition.Status}");
        if (authoritativeStatusAlreadyAdvanced && command.Outcome == BrokerDispatchResult.RejectedLocally && brokerOrderDefinition.PendingMutation == BrokerMutationKind.Place)
            return BrokerOrderCompute.Reject("BrokerOrder.DISPATCH.CONFLICTS_WITH_AUTHORITATIVE_OBSERVATION");
        var rejected = command.Outcome == BrokerDispatchResult.RejectedLocally;
        var proposedBrokerOrderDefinition = brokerOrderDefinition with
        {
            Status = rejected && authoritativeStatusAlreadyAdvanced ? brokerOrderDefinition.Status : status,
            Revision = brokerOrderDefinition.Revision + 1,
            DispatchCategory = command.Category,
            DispatchDetail = command.Detail,
            ChangedAtUtc = command.RecordedAtUtc,
            LastObservation = null,
            PendingMutation = rejected ? BrokerMutationKind.Unknown : brokerOrderDefinition.PendingMutation,
            RequestedSignedNetDebitLimit = rejected ? null : brokerOrderDefinition.RequestedSignedNetDebitLimit
        };
        return BrokerOrderCompute.Accept(proposedBrokerOrderDefinition);
    }

    /// <summary>Computes the RecordBrokerOrderObservation decision without mutating state or performing broker effects.</summary>
    internal static BrokerOrderCompute RecordBrokerOrderObservation(RecordBrokerOrderObservationCommand command, BrokerOrderDefinition? brokerOrderDefinition, string? priorObservationHash)
    {
        if (brokerOrderDefinition is null)
            return BrokerOrderCompute.Reject("BrokerOrder.NOT_FOUND");
        var observation = command.Observation;
        if (priorObservationHash is not null)
            return string.Equals(priorObservationHash, observation.ContentHash, StringComparison.Ordinal) ? BrokerOrderCompute.Replay(brokerOrderDefinition!) : BrokerOrderCompute.Reject("BrokerOrder.OBSERVATION_ID_CONFLICT");
        if (!string.Equals(brokerOrderDefinition.Order.BrokerAccountAlias, observation.AccountAlias, StringComparison.Ordinal) || observation.ComponentId != command.EntityId.ComponentId)
            return BrokerOrderCompute.Reject("BrokerOrder.OBSERVATION_CORRELATION_FAILED");
        var isLateExecutionFact = observation.Kind is BrokerOrderObservationKind.Execution or BrokerOrderObservationKind.Commission or BrokerOrderObservationKind.OrderCompleted;
        if (observation.OperationId != Guid.Empty && observation.OperationId != brokerOrderDefinition.OperationId && (!isLateExecutionFact || observation.OperationId != brokerOrderDefinition.PriorOperationId))
            return BrokerOrderCompute.Reject("BrokerOrder.OBSERVATION_OPERATION_CONFLICT");
        var sameOperation = observation.OperationId == brokerOrderDefinition.OperationId;
        var confirmsUpdate = sameOperation && observation.Kind == BrokerOrderObservationKind.Acknowledged &&
            brokerOrderDefinition.PendingMutation == BrokerMutationKind.UpdateLimit;
        var confirmsPlacement = brokerOrderDefinition.PendingMutation == BrokerMutationKind.Place &&
            observation.Kind is BrokerOrderObservationKind.Acknowledged or BrokerOrderObservationKind.Execution;
        var nextStatus = ResolveStatus(brokerOrderDefinition.Status, brokerOrderDefinition.StatusBeforeMutation, observation.Kind);
        if (nextStatus == BrokerOrderStatus.Unknown)
            return BrokerOrderCompute.Reject($"BrokerOrder.OBSERVATION_INVALID_STATE;{brokerOrderDefinition.Status};{observation.Kind}");
        if (confirmsUpdate && brokerOrderDefinition.StatusBeforeMutation == BrokerOrderStatus.PartiallyFilled)
            nextStatus = BrokerOrderStatus.PartiallyFilled;
        var resolvesMutation = confirmsPlacement || confirmsUpdate || nextStatus is BrokerOrderStatus.Filled or BrokerOrderStatus.Cancelled or BrokerOrderStatus.Rejected;
        var proposedBrokerOrderDefinition = brokerOrderDefinition with
        {
            Status = nextStatus,
            Revision = brokerOrderDefinition.Revision + 1,
            BrokerRevision = Math.Max(brokerOrderDefinition.BrokerRevision, observation.OrderRevision),
            LastObservation = observation,
            ChangedAtUtc = observation.OccurredAtUtc,
            CurrentSignedNetDebitLimit = confirmsUpdate
                ? brokerOrderDefinition.RequestedSignedNetDebitLimit ?? brokerOrderDefinition.CurrentSignedNetDebitLimit
                : brokerOrderDefinition.CurrentSignedNetDebitLimit,
            PendingMutation = resolvesMutation ? BrokerMutationKind.Unknown : brokerOrderDefinition.PendingMutation,
            RequestedSignedNetDebitLimit = resolvesMutation ? null : brokerOrderDefinition.RequestedSignedNetDebitLimit
        };
        return BrokerOrderCompute.Accept(proposedBrokerOrderDefinition);
    }

    /// <summary>Resolves the lifecycle status allowed by an authoritative broker observation.</summary>
    private static BrokerOrderStatus ResolveStatus(BrokerOrderStatus current, BrokerOrderStatus statusBeforeMutation, BrokerOrderObservationKind observation) => observation switch
    {
        BrokerOrderObservationKind.Acknowledged when current is BrokerOrderStatus.PlacePending or BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.OutcomeUnknown or BrokerOrderStatus.UpdatePending => current == BrokerOrderStatus.UpdatePending && statusBeforeMutation == BrokerOrderStatus.PartiallyFilled ? BrokerOrderStatus.PartiallyFilled : BrokerOrderStatus.Working,
        BrokerOrderObservationKind.Acknowledged when current == BrokerOrderStatus.PartiallyFilled => BrokerOrderStatus.PartiallyFilled,
        BrokerOrderObservationKind.Execution when current is BrokerOrderStatus.PlacePending or BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.UpdatePending or BrokerOrderStatus.CancelPending or BrokerOrderStatus.Cancelled or BrokerOrderStatus.OutcomeUnknown => BrokerOrderStatus.PartiallyFilled,
        BrokerOrderObservationKind.Commission when current is BrokerOrderStatus.PlacePending or BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.Filled or BrokerOrderStatus.UpdatePending or BrokerOrderStatus.CancelPending or BrokerOrderStatus.Cancelled or BrokerOrderStatus.OutcomeUnknown => current,
        BrokerOrderObservationKind.OrderCompleted when current is BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.CancelPending or BrokerOrderStatus.UpdatePending => BrokerOrderStatus.Filled,
        BrokerOrderObservationKind.Cancelled when current is BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.CancelPending or BrokerOrderStatus.OutcomeUnknown => BrokerOrderStatus.Cancelled,
        BrokerOrderObservationKind.Rejected when current is BrokerOrderStatus.PlacePending or BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.OutcomeUnknown => BrokerOrderStatus.Rejected,
        _ => BrokerOrderStatus.Unknown
    };
    /// <summary>Computes the RequestBrokerOrderCancel decision without mutating state or performing broker effects.</summary>
    internal static BrokerOrderCompute RequestBrokerOrderCancel(RequestBrokerOrderCancelCommand command, BrokerOrderDefinition? brokerOrderDefinition)
    {
        if (brokerOrderDefinition is null)
            return BrokerOrderCompute.Reject("BrokerOrder.NOT_FOUND");
        if (brokerOrderDefinition.OperationId == command.OperationId && brokerOrderDefinition.Status == BrokerOrderStatus.CancelPending)
            return BrokerOrderCompute.Replay(brokerOrderDefinition!);
        if (brokerOrderDefinition.PendingMutation != BrokerMutationKind.Unknown || brokerOrderDefinition.Status is not (BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled))
            return BrokerOrderCompute.Reject($"BrokerOrder.CANCEL.INVALID_STATE;{brokerOrderDefinition.Status}");
        if (brokerOrderDefinition.BrokerRevision <= 0)
            return BrokerOrderCompute.Reject("BrokerOrder.CANCEL.BROKER_REVISION_UNKNOWN");
        var proposedBrokerOrderDefinition = brokerOrderDefinition with
        {
            PriorOperationId = brokerOrderDefinition.OperationId,
            OperationId = command.OperationId,
            StatusBeforeMutation = brokerOrderDefinition.Status,
            Status = BrokerOrderStatus.CancelPending,
            PendingMutation = BrokerMutationKind.Cancel,
            Revision = brokerOrderDefinition.Revision + 1,
            ChangedAtUtc = command.EffectiveAtUtc,
            DispatchCategory = string.Empty,
            DispatchDetail = string.Empty,
            LastObservation = null
        };
        return BrokerOrderCompute.Accept(proposedBrokerOrderDefinition);
    }

    /// <summary>Computes the RequestBrokerOrderLimitUpdate decision without mutating state or performing broker effects.</summary>
    internal static BrokerOrderCompute RequestBrokerOrderLimitUpdate(RequestBrokerOrderLimitUpdateCommand command, BrokerOrderDefinition? brokerOrderDefinition)
    {
        if (brokerOrderDefinition is null)
            return BrokerOrderCompute.Reject("BrokerOrder.NOT_FOUND");
        if (brokerOrderDefinition.OperationId == command.OperationId && (brokerOrderDefinition.RequestedSignedNetDebitLimit ?? brokerOrderDefinition.CurrentSignedNetDebitLimit) == command.NewSignedNetDebitLimit)
            return BrokerOrderCompute.Replay(brokerOrderDefinition!);
        if (brokerOrderDefinition.PendingMutation != BrokerMutationKind.Unknown || brokerOrderDefinition.Status is not (BrokerOrderStatus.Working or BrokerOrderStatus.Dispatched or BrokerOrderStatus.PartiallyFilled))
            return BrokerOrderCompute.Reject($"BrokerOrder.UPDATE.INVALID_STATE;{brokerOrderDefinition.Status}");
        if (brokerOrderDefinition.BrokerRevision <= 0)
            return BrokerOrderCompute.Reject("BrokerOrder.UPDATE.BROKER_REVISION_UNKNOWN");
        if (command.NewSignedNetDebitLimit == brokerOrderDefinition.CurrentSignedNetDebitLimit)
            return BrokerOrderCompute.Replay(brokerOrderDefinition!);
        var component = brokerOrderDefinition.Order.Components.SingleOrDefault(item => item.ComponentId == command.EntityId.ComponentId);
        if (component is null)
            return BrokerOrderCompute.Reject("BrokerOrder.UPDATE.COMPONENT_NOT_FOUND");
        var minimum = component.MinimumSignedNetDebitLimit;
        var maximum = component.MaximumSignedNetDebitLimit;
        var tick = component.TickIncrement;
        if (minimum is null || maximum is null || tick is null || tick <= 0m || command.NewSignedNetDebitLimit < minimum || command.NewSignedNetDebitLimit > maximum || decimal.Remainder(command.NewSignedNetDebitLimit, tick.Value) != 0m)
            return BrokerOrderCompute.Reject("BrokerOrder.UPDATE.OUTSIDE_APPROVED_ENVELOPE");
        var proposedBrokerOrderDefinition = brokerOrderDefinition with
        {
            PriorOperationId = brokerOrderDefinition.OperationId,
            OperationId = command.OperationId,
            RequestedSignedNetDebitLimit = command.NewSignedNetDebitLimit,
            StatusBeforeMutation = brokerOrderDefinition.Status,
            Status = BrokerOrderStatus.UpdatePending,
            PendingMutation = BrokerMutationKind.UpdateLimit,
            Revision = brokerOrderDefinition.Revision + 1,
            ChangedAtUtc = command.EffectiveAtUtc,
            DispatchCategory = string.Empty,
            DispatchDetail = string.Empty,
            LastObservation = null
        };
        return BrokerOrderCompute.Accept(proposedBrokerOrderDefinition);
    }
}
