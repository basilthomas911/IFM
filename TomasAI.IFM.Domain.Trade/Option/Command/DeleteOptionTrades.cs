using TomasAI.IFM.Domain.Trade.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Option.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

/// <summary>Handles deletion of every option trade associated with an order.</summary>
public static class DeleteOptionTrades
{
    /// <summary>Deletes the order's option trades and returns the command result.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this DeleteOptionTradesCommand command,
        ICommandActorContext<OptionTradeCommandActor> context,
        OptionTradeCommandState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var db = context.DbFactory.TradeDb;
        var trades = await db.GetOptionTradesAsync(command.OrderId.Id).ConfigureAwait(false);
        foreach (var trade in trades)
            await db.DeleteOptionTradeAsync(trade.OrderId, trade.TradeId).ConfigureAwait(false);

        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }
}
