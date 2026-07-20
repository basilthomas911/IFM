using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Domain.Trade.Plan.CommandHandlers;

public class UpdateTradePlanCommandHandler 
    : BaseBoundedContextCommandHandler<TradePlanBoundedContextState>,
    IBoundedContextCommandHandler<UpdateTradePlanCommand, TradePlanBoundedContextState>
{
    
    public bool Execute(UpdateTradePlanCommand e, TradePlanBoundedContextState state)
        => UpdateTradePlan(e, state);

    /// <summary>
    /// Updates the trade plan if it has changed.
    /// </summary>
    /// <remarks>The method checks whether the provided trade plan differs from the current state. If a change
    /// is detected, it updates the state with a new <see cref="TradePlanUpdatedEvent"/>.</remarks>
    /// <param name="e">The command containing the new trade plan to be applied.</param>
    /// <param name="state">The current state of the trade plan aggregate.</param>
    /// <returns><see langword="true"/> if the trade plan was updated successfully; otherwise, <see langword="false"/>.</returns>
    static bool UpdateTradePlan(UpdateTradePlanCommand e, TradePlanBoundedContextState state)
        => state.HasTradePlanChanged(e.TradePlan) && 
            state.Update(new TradePlanUpdatedEvent {TradePlan = e.TradePlan}, e);

}
