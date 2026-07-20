using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.State;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class AddOrderToFund
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ServiceResult<GuidResult> Execute(this AddOrderToFundCommand e, FundCommandState state)
        => e switch
        {
            _ when !state.FundExists 
                => e.UpdateFailed( FundDoesNotExist(e)),
            _ when state.FundExists && state.FundId != e.FundOrder.FundId 
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when state.FundOrderExists(e.FundOrder.FundId, e.FundOrder.OrderId) 
                => e.UpdateFailed( FundOrderAlreadyExists(e)),
            _ when e.FundOrder.OrderStatus != OrderStatus.Open 
                => e.UpdateFailed(FundOrderNotInOpenState(e)),
            _ => e.UpdatedOk( () => state.Update(e.CreateOrderAddedToFundEvent(), e))
        };

    /// <summary>
    /// Creates an OrderAddedToFundEvent based on the details provided in the AddOrderToFundCommand, including setting the appropriate command ID, subject, entity ID, and fund order information.
    /// </summary>
    /// <param name="e">The command containing the details of the fund order to add. Cannot be null.</param>
    /// <returns>The created OrderAddedToFundEvent.</returns>
    static OrderAddedToFundEvent CreateOrderAddedToFundEvent(this AddOrderToFundCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OrderAddedToFundEvent.Actor, OrderAddedToFundEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FundOrder = e.FundOrder
        };

    static string FundDoesNotExist(AddOrderToFundCommand e) => $"{e.CommandName} fundId {e.FundOrder.FundId} does not exist";
    static string FundOrderAlreadyExists(AddOrderToFundCommand e) => $"{e.CommandName}: orderId {e.FundOrder.OrderId} already exists";
    static string FundOrderNotInOpenState(AddOrderToFundCommand e) => $"{e.CommandName}: orderId {e.FundOrder.OrderId} invalid order status {e.FundOrder.OrderStatus}";
}
