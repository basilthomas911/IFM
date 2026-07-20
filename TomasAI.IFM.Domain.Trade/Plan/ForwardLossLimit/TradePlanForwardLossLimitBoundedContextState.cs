using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>
/// Represents the bounded context state for managing forward loss limits in a trade plan.
/// </summary>
/// <remarks>This class maintains the state of forward loss limits and provides methods to check if the forward
/// loss limit has been reached, as well as to apply domain events that update or clear the forward loss
/// limits.</remarks>
public class TradePlanForwardLossLimitBoundedContextState 
    : BaseBoundedContextState<TradePlanForwardLossLimitBoundedContextState>

{
    List<TradePlanForwardLossLimitReadModel> _state = [];

    /// <summary>
    /// Determines whether the forward loss limit has been reached based on the current state.
    /// </summary>
    /// <remarks>The forward loss limit is considered reached when the number of entries in the state with a
    /// limit type of <see cref="ForwardLossLimitType.LimitWarning"/> is greater than or equal to 4.</remarks>
    /// <returns><see langword="true"/> if the forward loss limit has been reached; otherwise, <see langword="false"/>.</returns>
    internal bool HasForwardLossLimitBeenReached() 
        => _state.Count(e => e.LimitType == ForwardLossLimitType.LimitWarning) >= 4;

    /// <summary>
    /// Applies the specified domain event to update the current state of the object.
    /// </summary>
    /// <remarks>This method processes the provided domain event by delegating to the appropriate handler
    /// based on the event type. If the event type is not recognized, the method returns <see langword="false"/> without
    /// making any changes.</remarks>
    /// <param name="domainEvent">The domain event to apply. Must be a recognized event type.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                TradePlanForwardLossLimitWarningUpdatedEvent e => On(e),
                TradePlanForwardLossLimitClearedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// save updated trade plan forward loss limit
    /// </summary>
    /// <param name="e"></param>
    bool On(TradePlanForwardLossLimitWarningUpdatedEvent e)
    {
        _state.Add(e.TradePlanForwardLossLimit);
        return true;
    }

    /// <summary>
    /// clear forward loss limits
    /// </summary>
    /// <param name="e"></param>
    bool On(TradePlanForwardLossLimitClearedEvent e)
    {
        _state.Clear();
        return true;
    }

}
