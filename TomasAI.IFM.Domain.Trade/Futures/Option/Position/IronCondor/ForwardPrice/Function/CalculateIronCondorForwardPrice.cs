using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;
/// <summary>Runs the original adjustment against a single immutable capture without job services or probability calculations.</summary>
public static class CalculateIronCondorForwardPrice
{
    /// <summary>Computes a price and dispatches its completed terminal event; persistence belongs to the Function base.</summary>
    /// <param name="command">The exact position, four leg observations and calendar dates.</param>
    /// <param name="state">The request-local completed-event state owned by the Function framework.</param>
    /// <param name="context">The registered actor capabilities.</param>
    /// <param name="dispatch">Creates a terminal event through the actor event map.</param>
    /// <param name="cancellationToken">Cancels before numerical work begins.</param>
    /// <returns>The price completion awaiting framework persistence.</returns>
    public static ValueTask<FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<ForwardPriceSessionId>>> ExecuteAsync(
        this CalculateIronCondorForwardPriceCommand command, IronCondorForwardPriceFunctionState state,
        IIronCondorForwardPriceFunctionContext context,
        Func<FunctionEventContext<CalculateIronCondorForwardPriceCommand>, FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<ForwardPriceSessionId>>> dispatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dispatch);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(dispatch(new(typeof(IronCondorForwardPriceCalculatedEvent), command, command.Compute())));
    }
    /// <summary>Delegates pure pricing to the Trade.Shared-owned formula; never mutates actor state.</summary>
    /// <param name="command">The frozen calculation request.</param>
    /// <returns>The unchanged raw component sum with calculation provenance.</returns>
    internal static IronCondorForwardPrice Compute(this CalculateIronCondorForwardPriceCommand command) => IronCondorForwardPriceCalculation.Calculate(command);
}
