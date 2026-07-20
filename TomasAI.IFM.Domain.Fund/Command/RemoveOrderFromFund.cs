using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class RemoveOrderFromFund
{
    /// <summary>
    /// Attempts to remove an order from a fund within the specified context and state.
    /// </summary>
    /// <param name="e">The command containing the details of the order to remove from the fund. Cannot be null.</param>
    /// <param name="state">The current state of the fund from which the order is to be removed. Cannot be null.</param>
    /// <returns>true if the order was successfully removed from the fund; otherwise, false.</returns>
    /// <exception cref="RemoveOrderFromFundException">Thrown if the specified fund does not exist, the fund ID does not match, or the order does not exist within the
    /// fund.</exception>
    public static ServiceResult<GuidResult> Execute(this RemoveOrderFromFundCommand e, FundCommandState state)
        =>  e switch
        {
            _ when !state.FundExists 
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when state.FundExists && state.FundId != e.FundOrderId.FundId 
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when !state.FundOrderExists(e.FundOrderId.FundId, e.FundOrderId.OrderId) 
            => e.UpdateFailed(FundOrderDoesNotExist(e)),
            _ => e.UpdatedOk( () => state.Update(e.CreateOrderRemovedFromFundEvent(), e))
        };

    /// <summary>
    /// Creates an OrderRemovedFromFundEvent based on the details provided in the RemoveOrderFromFundCommand. 
    /// This event captures the necessary information to represent the removal of an order from a fund, including the command ID, subject, entity ID, fund order ID, and metadata about when and by whom the order was removed.
    /// </summary>
    /// <param name="e">The command containing the details of the order to remove from the fund. Cannot be null.</param>
    /// <returns>The created event representing the removal of the order from the fund.</returns>
    static OrderRemovedFromFundEvent CreateOrderRemovedFromFundEvent(this RemoveOrderFromFundCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OrderRemovedFromFundEvent.Actor, OrderRemovedFromFundEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FundOrderId = e.FundOrderId,
            RemovedOn = DateTime.Now,
            RemovedBy = Environment.UserName
        };

    public static string FundDoesNotExist(RemoveOrderFromFundCommand e) 
        => $"{e.CommandName}: fundId {e.FundOrderId.FundId} does not exist";
    public static string FundOrderDoesNotExist(RemoveOrderFromFundCommand e) 
        => $"{e.CommandName}: orderId {e.FundOrderId.OrderId} does not exist within fund: {e.FundOrderId.FundId}";
}
