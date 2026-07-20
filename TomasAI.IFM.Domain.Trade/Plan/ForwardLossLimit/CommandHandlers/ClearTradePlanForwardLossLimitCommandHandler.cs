using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.CommandHandlers;

public class ClearTradePlanForwardLossLimitCommandHandler : BaseBoundedContextCommandHandler<TradePlanForwardLossLimitBoundedContextState>,
    IBoundedContextCommandHandler<ClearTradePlanForwardLossLimitCommand, TradePlanForwardLossLimitBoundedContextState>
{
    /// <summary>
    /// Executes the specified command to clear the forward loss limit for a trade plan.
    /// </summary>
    /// <remarks>This method processes the provided command by generating a corresponding event and updating
    /// the bounded context state. Ensure that the <paramref name="e"/> parameter is valid and that the <paramref
    /// name="state"/> is in a modifiable state before calling this method.</remarks>
    /// <param name="e">The command containing the details required to clear the forward loss limit, including the entity ID, the
    /// originator, and the timestamp.</param>
    /// <param name="state">The current state of the trade plan forward loss limit aggregate, which will be updated as part of the
    /// operation.</param>
    /// <returns><see langword="true"/> if the forward loss limit was successfully cleared and the state was updated; otherwise,
    /// <see langword="false"/>.</returns>
    public bool Execute(ClearTradePlanForwardLossLimitCommand e, TradePlanForwardLossLimitBoundedContextState state)
        => state.Update(new TradePlanForwardLossLimitClearedEvent
            {
                ForwardLossLimitId = e.EntityId,
                ClearedOn = e.OriginatedOn,
                ClearedBy= e.OriginatedBy
            },e);
}
