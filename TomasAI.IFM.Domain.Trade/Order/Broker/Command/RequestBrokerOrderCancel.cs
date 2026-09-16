using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Records an individual broker-order cancellation before the broker is called.</summary>
public static class RequestBrokerOrderCancel
{
    /// <summary>Moves a working logical order into its durable cancel-pending state.</summary>
    public static ServiceResult<GuidResult> Execute(
        this RequestBrokerOrderCancelCommand command,
        BrokerOrderCommandState state)
    {
        var current = state.Current;
        if (current is null) return command.UpdateFailed("BO.NOT_FOUND");
        if (current.OperationId == command.OperationId && current.Status == BrokerOrderStatus.CancelPending)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        if (current.Status is not (BrokerOrderStatus.Dispatched or BrokerOrderStatus.Working or
            BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.OutcomeUnknown))
            return command.UpdateFailed($"BO.CANCEL.INVALID_STATE;{current.Status}");
        if (current.BrokerRevision <= 0)
            return command.UpdateFailed("BO.CANCEL.BROKER_REVISION_UNKNOWN");
        var next = current with
        {
            PriorOperationId = current.OperationId,
            OperationId = command.OperationId,
            StatusBeforeMutation = current.Status,
            Status = BrokerOrderStatus.CancelPending,
            PendingMutation = BrokerMutationKind.Cancel,
            Revision = current.Revision + 1,
            ChangedAtUtc = command.EffectiveAtUtc,
            LastObservation = null
        };
        return Apply(command, state, next);
    }

    private static ServiceResult<GuidResult> Apply(
        RequestBrokerOrderCancelCommand command,
        BrokerOrderCommandState state,
        BrokerOrderDefinition next)
    {
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
}
