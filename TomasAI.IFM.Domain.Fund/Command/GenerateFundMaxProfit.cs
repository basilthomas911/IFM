using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class GenerateFundMaxProfit
{
    /// <summary>
    /// Executes the specified fund max profit generation command within the given context and updates the fund state
    /// accordingly.
    /// </summary>
    /// <param name="e">The command representing the request to generate the fund's maximum profit. Cannot be null.</param>
    /// <param name="state">The current state of the fund to be updated. Must represent an existing fund.</param>
    /// <returns>true if the fund state was successfully updated with the generated max profit event; otherwise, false.</returns>
    /// <exception cref="GenerateFundRiskMarginException">Thrown if the specified fund does not exist in the provided state.</exception>
    public static ServiceResult<GuidResult> Execute(this GenerateFundMaxProfitCommand e, FundCommandState state)
        => e switch
        {
            _ when !state.FundExists 
                => e.UpdateFailed(FundDoesNotExist(e)),
            _ => e.UpdatedOk(() => state.Update(e.CreateFundMaxProfitGeneratedEvent(), e))
        };

    /// <summary>
    /// Creates a FundMaxProfitGeneratedEvent based on the provided GenerateFundMaxProfitCommand, populating the event's
    /// </summary>
    /// <param name="e">The command representing the request to generate the fund's maximum profit. Cannot be null.</param>
    /// <returns></returns>
    static FundMaxProfitGeneratedEvent CreateFundMaxProfitGeneratedEvent(this GenerateFundMaxProfitCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FundMaxProfitGeneratedEvent.Actor, FundMaxProfitGeneratedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FundOrder = e.FundOrder
        };

    public static string FundDoesNotExist(GenerateFundMaxProfitCommand e)
        => $"{e.CommandName}: fundId {e.FundOrder.FundId} does not exist";
}
