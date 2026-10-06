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
        var updated = command.Compute(state.TradeOrderDefinition, out var model) switch
        {
            _ when !model.Accepted => command.UpdateFailed(ref errorMsg, $"{model.RejectionCode};{model.RejectionReason}"),
            _ when !model.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "computed Trade Order definition is invalid for this order"),
            _ => state.Update(command.CreateTradeOrderChangedEvent(model), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the amendment model without changing actor state.</summary>
    /// <param name="command">The command containing the proposed replacement order.</param>
    /// <param name="current">The current order owned by actor state, or null when absent.</param>
    /// <param name="model">The computed replacement or the reason the amendment was rejected.</param>
    /// <returns>True when the model contains a valid replacement; otherwise, false.</returns>
    internal static bool Compute(this AmendTradeOrderCommand command, TradeOrderDefinition? current,
        out TradeOrderCompute model)
    {
        model = TradeOrderComputation.Amend(current, command.Order);
        return model.Accepted;
    }

    /// <summary>Creates the amendment event without changing actor state.</summary>
    /// <param name="command">The originating amendment command.</param>
    /// <param name="model">The successfully computed amendment model containing the replacement.</param>
    /// <returns>The event to apply and persist through the command state.</returns>
    internal static TradeOrderChangedEvent CreateTradeOrderChangedEvent(this AmendTradeOrderCommand command,
        TradeOrderCompute model) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, TradeOrderChangedEvent.Actor, TradeOrderChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        TradeOrderDefinition = model.TradeOrderDefinition!
    };
}
