using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Records a price-only broker-order change before the external broker is called.</summary>
public static class RequestBrokerOrderLimitUpdate
{
    /// <summary>Applies a new approved limit only while the broker order is working.</summary>
    public static ServiceResult<GuidResult> Execute(
        this RequestBrokerOrderLimitUpdateCommand command,
        BrokerOrderCommandState state)
    {
        var current = state.Current;
        if (current is null) return command.UpdateFailed("BO.NOT_FOUND");
        if (current.OperationId == command.OperationId &&
            current.CurrentSignedNetDebitLimit == command.NewSignedNetDebitLimit)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        if (current.Status is not (BrokerOrderStatus.Working or BrokerOrderStatus.Dispatched or
            BrokerOrderStatus.PartiallyFilled))
            return command.UpdateFailed($"BO.UPDATE.INVALID_STATE;{current.Status}");
        if (current.BrokerRevision <= 0)
            return command.UpdateFailed("BO.UPDATE.BROKER_REVISION_UNKNOWN");
        if (command.NewSignedNetDebitLimit == current.CurrentSignedNetDebitLimit)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        var component = current.Order.Components.SingleOrDefault(item =>
            item.ComponentId == command.EntityId.ComponentId);
        if (component is null) return command.UpdateFailed("BO.UPDATE.COMPONENT_NOT_FOUND");
        var minimum = component.MinimumSignedNetDebitLimit;
        var maximum = component.MaximumSignedNetDebitLimit;
        var tick = component.TickIncrement;
        if (minimum is null || maximum is null || tick is null || tick <= 0m ||
            command.NewSignedNetDebitLimit < minimum || command.NewSignedNetDebitLimit > maximum ||
            decimal.Remainder(command.NewSignedNetDebitLimit, tick.Value) != 0m)
            return command.UpdateFailed("BO.UPDATE.OUTSIDE_APPROVED_ENVELOPE");
        var next = current with
        {
            PriorOperationId = current.OperationId,
            OperationId = command.OperationId,
            CurrentSignedNetDebitLimit = command.NewSignedNetDebitLimit,
            StatusBeforeMutation = current.Status,
            Status = BrokerOrderStatus.UpdatePending,
            PendingMutation = BrokerMutationKind.UpdateLimit,
            Revision = current.Revision + 1,
            ChangedAtUtc = command.EffectiveAtUtc,
            LastObservation = null
        };
        return Apply(command, state, next);
    }

    private static ServiceResult<GuidResult> Apply(
        RequestBrokerOrderLimitUpdateCommand command,
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
