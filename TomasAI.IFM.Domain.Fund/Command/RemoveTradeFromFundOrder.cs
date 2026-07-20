using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class RemoveTradeFromFundOrder
{
    /// <summary>
    /// Attempts to remove a trade from a fund order within the specified fund state, applying the given command and
    /// updating the state accordingly.
    /// </summary>
    /// <param name="e">The command containing the details of the trade to remove from the fund order. Cannot be null.</param>
    /// <param name="state">The current state of the fund, used to validate and apply the removal operation. Cannot be null.</param>
    /// <returns>true if the trade was successfully removed from the fund order and the state was updated; otherwise, false.</returns>
    /// <exception cref="RemoveTradeFromFundOrderException">Thrown if the fund, fund order, or trade specified in the command does not exist in the current state.</exception>
    public static ServiceResult<GuidResult> Execute(this RemoveTradeFromFundOrderCommand e, FundCommandState state)
        =>e switch
        {
            _ when !state.FundExists 
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when state.FundExists && state.FundId != e.FundOrderTradeId.FundId 
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when !state.FundOrderExists(e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId) 
                => e.UpdateFailed(FundOrderDoesNotExist(e)),
            _ when !state.FundOrderTradeExists(e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId, e.FundOrderTradeId.TradeId) 
                => e.UpdateFailed(FundOrderTradeDoesNotExist(e)),
            _ => e.UpdatedOk( () => state.Update(e.CreateTradeRemovedFromFundOrderEvent(), e))
        };


    /// <summary>
    /// Creates a new instance of the TradeRemovedFromFundOrderEvent based on the details provided in the RemoveTradeFromFundOrderCommand.
    /// </summary>
    /// <param name="e">The command containing the details of the trade to remove from the fund order. Cannot be null.</param>
    /// <returns>The created event representing the removal of the trade from the fund order.</returns>
    static TradeRemovedFromFundOrderEvent CreateTradeRemovedFromFundOrderEvent(this RemoveTradeFromFundOrderCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, TradeRemovedFromFundOrderEvent.Actor, TradeRemovedFromFundOrderEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FundOrderTradeId = e.FundOrderTradeId,
            RemovedOn = DateTime.Now,
            RemovedBy = Environment.UserName
        };

    public static string FundDoesNotExist(RemoveTradeFromFundOrderCommand e) 
        => $"{e.CommandName}: fundId {e.FundOrderTradeId.FundId} does not exist";
    public static string FundOrderDoesNotExist(RemoveTradeFromFundOrderCommand e) 
        => $"{e.CommandName}: orderId {e.FundOrderTradeId.OrderId} does not exist";
    public static string FundOrderTradeDoesNotExist(RemoveTradeFromFundOrderCommand e) 
        => $"{e.CommandName}: tradeId {e.FundOrderTradeId.TradeId} does not exist";
}
