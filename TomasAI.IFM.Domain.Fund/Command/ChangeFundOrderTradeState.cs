using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class ChangeFundOrderTradeState
{
    /// <summary>
    /// Handles the ChangeFundOrderTradeStateCommand by validating the existence of the fund, order, and trade, and then updates the state with a FundOrderTradeStateChangedEvent. 
    /// Throws exceptions if any of the entities do not exist.
    /// </summary>
    /// <param name="e">The command containing the details of the fund order trade state change to be performed.</param>
    /// <param name="state">The current state of the fund, including information about the existence of the fund, order, and trade.</param>
    /// <returns>true if the fund order trade state was successfully updated; otherwise, false.</returns>
    /// <exception cref="ChangeFundOrderTradeStateException"></exception>
    public static ServiceResult<GuidResult> Execute(this ChangeFundOrderTradeStateCommand e, FundCommandState state)
        => e switch
        {
            _ when !state.FundExists
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when state.FundExists && state.FundId != e.FundOrderTradeId.FundId
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ when !state.FundOrderExists(e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId)
                => e.UpdateFailed( FundOrderDoesNotExist(e)),
            _ when !state.FundOrderTradeExists(e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId, e.FundOrderTradeId.TradeId)
                => e.UpdateFailed(FundOrderTradeDoesNotExist(e)),
            _ => e.UpdatedOk(() => state.Update(e.CreateFundOrderTradeStateChangedEvent(), e))
        };

    /// <summary>
    /// Creates a FundOrderTradeStateChangedEvent based on the details provided in the ChangeFundOrderTradeStateCommand. 
    /// This event captures the change in trade state for a specific fund order trade, including metadata such as the command ID, subject, entity ID, and timestamps.
    /// </summary>
    /// <param name="e">The command containing the details of the fund order trade state change to be performed.</param>
    /// <returns>The created event.</returns>
    static FundOrderTradeStateChangedEvent CreateFundOrderTradeStateChangedEvent(this ChangeFundOrderTradeStateCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FundOrderTradeStateChangedEvent.Actor, FundOrderTradeStateChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FundOrderTradeId = e.FundOrderTradeId,
            TradeState = e.TradeState,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

    static string FundDoesNotExist(ChangeFundOrderTradeStateCommand e) => $"{e.CommandName}: fundId {e.FundOrderTradeId.FundId} does not exist";
    static string FundOrderDoesNotExist(ChangeFundOrderTradeStateCommand e) => $"{e.CommandName}: orderId {e.FundOrderTradeId.OrderId} does not exist within fund {e.FundOrderTradeId.FundId}";
    static string FundOrderTradeDoesNotExist(ChangeFundOrderTradeStateCommand e) => $"{e.CommandName}: tradeId {e.FundOrderTradeId.TradeId} does not exist within order {e.FundOrderTradeId.OrderId}";
}
