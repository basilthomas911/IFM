using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class CloseFundOrder
{

    /// <summary>
    /// Attempts to close a fund order by applying the specified close command to the current fund state.
    /// </summary>
    /// <param name="e">The command containing the details required to close the fund order.</param>
    /// <param name="state">The current state of the fund, used to validate and update the fund order status.</param>
    /// <returns>true if the fund order was successfully closed; otherwise, false.</returns>
    /// <exception cref="CloseFundOrderException">Thrown if the fund does not exist or if the specified fund order does not exist in the current state.</exception>
    public static ServiceResult<GuidResult> Execute(this CloseFundOrderCommand e, FundCommandState state)
        =>e switch
        {
            _ when !state.FundExists 
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when !state.FundOrderExists(e.FundOrderId.FundId, e.FundOrderId.OrderId) 
                => e.UpdateFailed(FundOrderDoesNotExist(e)),
            _ when state.IsFundOrderClosed(e.FundOrderId.FundId, e.FundOrderId.OrderId) 
                => e.UpdateFailed(FundOrderAlreadyClosed(e)),
            _ => e.UpdatedOk(() => state.Update(e.CreateFundOrderClosedEvent(), e))
        };

    /// <summary>
    /// Creates a FundOrderClosedEvent based on the details provided in the CloseFundOrderCommand. 
    /// This event represents the successful closure of a fund order and contains relevant information 
    /// such as the command ID, subject, entity ID, fund order ID, closed date, and the user who initiated the closure.
    /// </summary>
    /// <param name="e">The close fund order command containing the details for the event.</param>
    /// <returns>The created fund order closed event.</returns>
    static FundOrderClosedEvent CreateFundOrderClosedEvent(this CloseFundOrderCommand e)
       => new()
       {
           CommandId = e.CommandId,
           Subject = new ActorSubject(ActorType.Event, FundOrderClosedEvent.Actor, FundOrderClosedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           FundOrderId = e.FundOrderId,
           ClosedOn = e.OriginatedOn,
           ClosedBy = e.OriginatedBy
       };

    public static string FundDoesNotExist(CloseFundOrderCommand e) => $"{e.CommandName}: fundId {e.FundOrderId.FundId} does not exist";
    public static string FundOrderDoesNotExist(CloseFundOrderCommand e) => $"{e.CommandName}: orderId {e.FundOrderId.OrderId} does not exist";
    public static string FundOrderAlreadyClosed(CloseFundOrderCommand e) => $"{e.CommandName}: orderId {e.FundOrderId.OrderId} is already closed";

}
