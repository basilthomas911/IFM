using TomasAI.IFM.Domain.Trade.Order.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command;

/// <summary>Amends a draft order by creating an event and applying it through actor state.</summary>
public static class AmendTradeOrder
{
    /// <summary>Computes an amendment without mutation, then applies its event to the supplied state.</summary>
    /// <param name="command">The replacement order with the same identity and next revision.</param>
    /// <param name="state">The actor state that owns the current order and pending events.</param>
    /// <returns>The command ID on success, or a failed result when amendment or event application is rejected.</returns>
    public static ServiceResult<GuidResult> Execute(this AmendTradeOrderCommand command, TradeOrderCommandState state)
    {
        var errorMsg = "unable to apply amended Trade Order event";
        var updated = command.Compute(state.TradeOrderDefinition, out var orderChange) switch
        {
            _ when !orderChange.Accepted => command.UpdateFailed(ref errorMsg, $"{orderChange.RejectionCode};{orderChange.RejectionReason}"),
            _ when !orderChange.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "computed Trade Order definition is invalid for this order"),
            _ => state.Update(command.CreateTradeOrderChangedEvent(orderChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the amendment without changing actor state.</summary>
    /// <param name="command">The command containing the proposed replacement order.</param>
    /// <param name="current">The current order owned by actor state, or null when absent.</param>
    /// <param name="orderChange">The computed replacement or the reason the amendment was rejected.</param>
    /// <returns>True when the computed order change contains a valid replacement; otherwise, false.</returns>
    internal static bool Compute(this AmendTradeOrderCommand command, TradeOrderDefinition? current,
        out TradeOrderCompute orderChange)
    {
        orderChange = TradeOrderComputation.Amend(current, command.Order);
        return orderChange.Accepted;
    }

    /// <summary>Creates the amendment event without changing actor state.</summary>
    /// <param name="command">The originating amendment command.</param>
    /// <param name="orderChange">The successfully computed amendment containing the replacement.</param>
    /// <returns>The event to apply and persist through the command state.</returns>
    internal static TradeOrderChangedEvent CreateTradeOrderChangedEvent(this AmendTradeOrderCommand command,
        TradeOrderCompute orderChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, TradeOrderChangedEvent.Actor, TradeOrderChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        TradeOrderDefinition = orderChange.TradeOrderDefinition!
    };
}
