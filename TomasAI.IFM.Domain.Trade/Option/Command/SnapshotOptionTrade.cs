using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command;

internal static class SnapshotOptionTrade
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    /// <exception cref="OptionTradeSnapshotException"></exception>
    public static bool Execute(this SnapshotOptionTradeCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeDoesNotExist(e.EntityId) => throw new OptionTradeSnapshotException(
                $"{e.CommandName}: trade: {e.OrderId}:{e.TradeId} does not exist"),
            _ => state.Update(e.CreateOptionTradeSnapshotEvent(state.ToOptionTrade()), e)
        };

    /// <summary>
    /// Creates an <see cref="OptionTradeSnapshotEvent"/> from a <see cref="SnapshotOptionTradeCommand"/>
    /// and the current read model of the option trade.
    /// The snapshot captures the full state of the trade at the point the command is processed.
    /// </summary>
    /// <param name="e">The snapshot command carrying the order identifier and origination details.</param>
    /// <param name="optionTrade">The current read model representing the full state of the option trade to snapshot.</param>
    /// <returns>A fully populated <see cref="OptionTradeSnapshotEvent"/>.</returns>
    internal static OptionTradeSnapshotEvent CreateOptionTradeSnapshotEvent(this SnapshotOptionTradeCommand e, OptionTradeReadModel optionTrade)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeSnapshotEvent.Actor, OptionTradeSnapshotEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.OrderId,
            OptionTrade = optionTrade,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

}
