using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Handles RecordBrokerOrderObservation through computation, failure guards and state-owned event application.</summary>
public static class RecordBrokerOrderObservation
{
    /// <summary>Applies a valid broker-order decision; exact replay succeeds without another event.</summary>
    /// <param name="command">The concrete broker-order intent.</param>
    /// <param name="state">The authoritative broker-order state.</param>
    /// <returns>The command ID on success, or the business/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this RecordBrokerOrderObservationCommand command,
        BrokerOrderCommandState state)
    {
        state.TryGetObservationHash(command.Observation.ObservationId, out var priorObservationHash);
        var errorMsg = "BrokerOrder.STATE.APPLY_FAILED";
        var computed = command.Compute(state.BrokerOrderDefinition, priorObservationHash, out var brokerOrderChange);
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
    /// <param name="priorObservationHash">The previously applied observation content hash, if any.</param>
    /// <param name="brokerOrderChange">The proposed definition, replay or rejection.</param>
    /// <returns>True when the business decision is accepted.</returns>
    internal static bool Compute(this RecordBrokerOrderObservationCommand command,
        BrokerOrderDefinition? brokerOrderDefinition, string? priorObservationHash, out BrokerOrderCompute brokerOrderChange)
    {
        brokerOrderChange = BrokerOrderComputation.RecordBrokerOrderObservation(command, brokerOrderDefinition, priorObservationHash);
        return brokerOrderChange.Accepted;
    }

    /// <summary>Creates the private event carrying the guarded broker-order definition.</summary>
    /// <param name="command">The originating command identity and route.</param>
    /// <param name="brokerOrderChange">The accepted business decision.</param>
    /// <returns>The source event to apply and persist through State.Update.</returns>
    internal static BrokerOrderChangedEvent CreateBrokerOrderChangedEvent(this RecordBrokerOrderObservationCommand command,
        BrokerOrderCompute brokerOrderChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, BrokerOrderChangedEvent.Actor, BrokerOrderChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        BrokerOrderDefinition = brokerOrderChange.BrokerOrderDefinition!
    };
}
