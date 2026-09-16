using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Handles the durable local result of one previously committed broker mutation.</summary>
public static class RecordBrokerDispatch
{
    /// <summary>Records a matching operation receipt and ignores its exact replay.</summary>
    public static ServiceResult<GuidResult> Execute(this RecordBrokerDispatchCommand command, BrokerOrderCommandState state)
    {
        var current = state.Current;
        if (current is null) return command.UpdateFailed("BO.NOT_FOUND");
        if (current.OperationId != command.OperationId) return command.UpdateFailed("BO.OPERATION.CONFLICT");
        if (current.PendingMutation == BrokerMutationKind.Unknown &&
            current.DispatchCategory == command.Category && current.DispatchDetail == command.Detail)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        var acceptedStatus = current.PendingMutation switch
        {
            BrokerMutationKind.Place => BrokerOrderStatus.Dispatched,
            BrokerMutationKind.UpdateLimit => current.StatusBeforeMutation,
            BrokerMutationKind.Cancel => BrokerOrderStatus.CancelPending,
            _ => BrokerOrderStatus.Unknown
        };
        if (acceptedStatus == BrokerOrderStatus.Unknown)
            return command.UpdateFailed("BO.DISPATCH.MUTATION_UNKNOWN");
        var localRejectionStatus = current.PendingMutation == BrokerMutationKind.Place
            ? BrokerOrderStatus.Rejected
            : current.StatusBeforeMutation;
        var receiptStatus = command.Outcome switch
        {
            BrokerDispatchResult.AcceptedForDispatch => acceptedStatus,
            BrokerDispatchResult.OutcomeUnknown => BrokerOrderStatus.OutcomeUnknown,
            _ => localRejectionStatus
        };
        var authoritativeStatusAlreadyAdvanced = current.Status is BrokerOrderStatus.Working or
            BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.Filled or BrokerOrderStatus.Cancelled;
        var status = authoritativeStatusAlreadyAdvanced && command.Outcome is not BrokerDispatchResult.RejectedLocally
            ? current.Status
            : receiptStatus;
        if (current.Status == status && current.DispatchCategory == command.Category && current.DispatchDetail == command.Detail)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        if (current.Status is not (BrokerOrderStatus.PlacePending or BrokerOrderStatus.UpdatePending or
                BrokerOrderStatus.CancelPending) && !authoritativeStatusAlreadyAdvanced)
            return command.UpdateFailed($"BO.DISPATCH.INVALID_STATE;{current.Status}");
        if (authoritativeStatusAlreadyAdvanced && command.Outcome == BrokerDispatchResult.RejectedLocally)
            return command.UpdateFailed("BO.DISPATCH.CONFLICTS_WITH_AUTHORITATIVE_OBSERVATION");
        var next = current with
        {
            Status = status, Revision = current.Revision + 1, DispatchCategory = command.Category,
            DispatchDetail = command.Detail, ChangedAtUtc = command.RecordedAtUtc, LastObservation = null,
            PendingMutation = command.Outcome == BrokerDispatchResult.OutcomeUnknown
                ? current.PendingMutation : BrokerMutationKind.Unknown
        };
        var applied = state.Update(new BrokerOrderChangedEvent
        {
            Subject = new(ActorType.Event, BrokerOrderChangedEvent.Actor, BrokerOrderChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId, State = next
        }, command);
        return applied ? new ServiceOk<GuidResult>(new(command.CommandId)) : command.UpdateFailed("BO.STATE.APPLY_FAILED");
    }
}
