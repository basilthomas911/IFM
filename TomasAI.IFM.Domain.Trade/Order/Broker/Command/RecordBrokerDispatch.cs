using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Handles RecordBrokerDispatch through computation, failure guards and state-owned event application.</summary>
public static class RecordBrokerDispatch
{
    /// <summary>Applies a valid broker-order decision; exact replay succeeds without another event.</summary>
    /// <param name="command">The concrete broker-order intent.</param>
    /// <param name="state">The authoritative broker-order state.</param>
    /// <returns>The command ID on success, or the business/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this RecordBrokerDispatchCommand command,
        BrokerOrderCommandState state)
    {
        var errorMsg = "BrokerOrder.STATE.APPLY_FAILED";
        var computed = command.Compute(state.BrokerOrderDefinition, out var brokerOrderChange);
        // An accepted replay has no new intent to persist or dispatch.
        if (brokerOrderChange.Accepted && brokerOrderChange.IsReplay && brokerOrderChange.IsValidFor(command.EntityId))
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var updated = computed switch
        {
            _ when !brokerOrderChange.Accepted
                => command.UpdateFailed(ref errorMsg, brokerOrderChange.RejectionReason),
            _ when !brokerOrderChange.IsValidFor(command.EntityId)
                => command.UpdateFailed(ref errorMsg, "BrokerOrder.COMPUTED_ORDER.INVALID"),
            _ => state.Update(command.CreateBrokerOrderChangedEvent(brokerOrderChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes proposed business data without modifying its inputs or actor state.</summary>
    /// <param name="command">The proposed broker-order intent.</param>
    /// <param name="brokerOrderDefinition">The current authoritative definition, if any.</param>
    /// <param name="brokerOrderChange">The proposed definition, replay or rejection.</param>
    /// <returns>True when the business decision is accepted.</returns>
    internal static bool Compute(this RecordBrokerDispatchCommand command,
        BrokerOrderDefinition? brokerOrderDefinition, out BrokerOrderCompute brokerOrderChange)
    {
        brokerOrderChange = BrokerOrderComputation.RecordBrokerDispatch(command, brokerOrderDefinition);
        return brokerOrderChange.Accepted;
    }

    /// <summary>Creates the private event carrying the guarded broker-order definition.</summary>
    /// <param name="command">The originating command identity and route.</param>
    /// <param name="brokerOrderChange">The accepted business decision.</param>
    /// <returns>The source event to apply and persist through State.Update.</returns>
    internal static BrokerOrderChangedEvent CreateBrokerOrderChangedEvent(this RecordBrokerDispatchCommand command,
        BrokerOrderCompute brokerOrderChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, BrokerOrderChangedEvent.Actor, BrokerOrderChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        BrokerOrderDefinition = brokerOrderChange.BrokerOrderDefinition!
    };
}
