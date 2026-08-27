using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

internal static class OpenOptionTradePosition
{
    /// <summary>
    /// Executes the command to open an option trade position by validating the current state and updating it with an
    /// opened event.
    /// </summary>
    /// <param name="e">The command to execute.</param>
    /// <param name="state">The current state of the option trade command.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OpenOptionTradeException">The trade position does not exist or is already opened.</exception>
    public static ServiceResult<GuidResult> Execute(this OpenOptionTradePositionCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeDoesNotExist(e.EntityId) => throw new OpenOptionTradeException(
                $"{e.CommandName}: option trade position {e.EntityId.OrderId}/{e.EntityId.TradeId} does not exist"),
            _ when state.TradePositionState == TradePositionState.Opened => throw new OpenOptionTradeException(
                $"{e.CommandName}: option trade position {e.EntityId.OrderId}/{e.EntityId.TradeId} already opened"),
            _ => e.UpdateResult(() => state.Update(e.CreateOptionTradePositionOpenedEvent(), e))
        };

    /// <summary>
    /// Creates an <see cref="OptionTradePositionOpenedEvent"/> from an <see cref="OpenOptionTradePositionCommand"/>.
    /// Sets the trade position state to <see cref="TradePositionState.Opened"/>.
    /// </summary>
    /// <param name="e">The command identifying the option trade position to open.</param>
    /// <returns>A fully populated <see cref="OptionTradePositionOpenedEvent"/>.</returns>
    internal static OptionTradePositionOpenedEvent CreateOptionTradePositionOpenedEvent(this OpenOptionTradePositionCommand e)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradePositionOpenedEvent.Actor, OptionTradePositionOpenedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OptionTradeId = e.OptionTradeId,
            TradePositionState = TradePositionState.Opened,
            OpenedOn = e.OriginatedOn,
            OpenedBy = e.OriginatedBy
        };


}
