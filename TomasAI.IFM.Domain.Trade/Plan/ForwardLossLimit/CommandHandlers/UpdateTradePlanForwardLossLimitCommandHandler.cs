using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.CommandHandlers;

public class UpdateTradePlanForwardLossLimitCommandHandler : BaseBoundedContextCommandHandler<TradePlanForwardLossLimitBoundedContextState>,
   IBoundedContextCommandHandler<UpdateTradePlanForwardLossLimitCommand, TradePlanForwardLossLimitBoundedContextState>
{
    /// <summary>
    /// Executes the command to update the trade plan forward loss limit.
    /// </summary>
    /// <param name="e"> The command containing the details for updating the trade plan forward loss limit.</param>
    /// <param name="state"> The current state of the trade plan forward loss limit aggregate.</param>
    public bool Execute(UpdateTradePlanForwardLossLimitCommand e, TradePlanForwardLossLimitBoundedContextState state)
        => UpdateTradePlanForwardLossLimit(e, state);

    /// <summary>
    /// Updates the forward loss limit state of a trade plan based on the provided command and current state.
    /// </summary>
    /// <remarks>This method evaluates the type of forward loss limit specified in the command and updates the
    /// state accordingly. If the limit type is <see cref="ForwardLossLimitType.LimitWarning"/> and the forward loss
    /// limit has been reached, the state is updated to reflect that the limit has been reached. Otherwise, the state is
    /// updated to reflect a warning. If no update is required, the method returns <see langword="false"/>.</remarks>
    /// <param name="e">The command containing the updated forward loss limit details and metadata.</param>
    /// <param name="state">The current state of the trade plan forward loss limit.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    static bool UpdateTradePlanForwardLossLimit(UpdateTradePlanForwardLossLimitCommand e, TradePlanForwardLossLimitBoundedContextState state)
    {
        if (e.TradePlanForwardLossLimit.LimitType == ForwardLossLimitType.LimitWarning)
        {
            if (state.HasForwardLossLimitBeenReached())
                return state.Update(new TradePlanForwardLossLimitReachedUpdatedEvent
                {
                    TradePlanForwardLossLimit = e.TradePlanForwardLossLimit with { LimitType = ForwardLossLimitType.LimitReached },
                    UpdatedOn = e.OriginatedOn,
                    UpdatedBy = e.OriginatedBy
                }, e);
            else
                return state.Update(new TradePlanForwardLossLimitWarningUpdatedEvent
                {
                    TradePlanForwardLossLimit = e.TradePlanForwardLossLimit with { LimitType = ForwardLossLimitType.LimitWarning },
                    UpdatedOn = e.OriginatedOn,
                    UpdatedBy = e.OriginatedBy
                }, e);
        }
        return false;
    }
}
