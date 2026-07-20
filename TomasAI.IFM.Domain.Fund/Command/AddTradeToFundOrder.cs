using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class AddTradeToFundOrder
{
    /// <summary>
    /// Attempts to add a trade to a fund order within the specified fund state, validating that the fund, order, and
    /// trade do not violate business constraints.
    /// </summary>
    /// <param name="e">The command containing the details of the trade to add to the fund order. Cannot be null.</param>
    /// <param name="state">The current state of the fund, used to validate and apply the operation. Cannot be null.</param>
    /// <returns>true if the trade was successfully added to the fund order; otherwise, false.</returns>
    /// <exception cref="AddTradeToFundOrderException">Thrown if the fund does not exist, the fund order does not exist, or the trade already exists in the fund order.</exception>
    public static ServiceResult<GuidResult> Execute(this AddTradeToFundOrderCommand e, FundCommandState state)
        => e switch
        {
            _ when !state.FundExists
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when state.FundExists && state.FundId != e.FundOrderTrade.FundId
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when !state.FundOrderExists(e.FundOrderTrade.FundId, e.FundOrderTrade.OrderId)
                => e.UpdateFailed(FundOrderDoesNotExist(e)),
            _ when state.FundOrderTradeExists(e.FundOrderTrade.FundId, e.FundOrderTrade.OrderId, e.FundOrderTrade.TradeId)
                => e.UpdateFailed(FundOrderTradeAlreadyExists(e)),
            _ => e.UpdatedOk(() => state.Update(e.CreateTradeAddedToFundOrderEvent(), e))
        };

    /// <summary>
    /// Creates a TradeAddedToFundOrderEvent based on the details of the AddTradeToFundOrderCommand, populating the event's properties accordingly.
    /// </summary>
    /// <param name="e">The command containing the details of the trade to add to the fund order.</param>
    /// <returns>The created event.</returns>
    static TradeAddedToFundOrderEvent CreateTradeAddedToFundOrderEvent(this AddTradeToFundOrderCommand e)
            => new()
            {
                CommandId = e.CommandId,
                Subject = new ActorSubject(ActorType.Event, TradeAddedToFundOrderEvent.Actor, TradeAddedToFundOrderEvent.Verb, e.EntityId.Format()),
                EntityId = e.EntityId,
                FundOrderTrade = e.FundOrderTrade
            };
    public static string FundDoesNotExist(AddTradeToFundOrderCommand e) => $"{e.CommandName}: fundId {e.FundOrderTrade.FundId} does not exist";
    public static string FundOrderDoesNotExist(AddTradeToFundOrderCommand e) => $"{e.CommandName}: orderId {e.FundOrderTrade.OrderId} does not exist";
    public static string FundOrderTradeAlreadyExists(AddTradeToFundOrderCommand e) => $"{e.CommandName}: tradeId {e.FundOrderTrade.TradeId} already exists";
}
