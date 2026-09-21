using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Service.TradePosition;

/// <summary>Processes non-financial trade-position projection and analytics events.</summary>
public interface ITradePositionService
{
    /// <summary>Processes a trade-position change.</summary>
    /// <param name="event">The position change event.</param>
    /// <returns>A task that completes when processing finishes.</returns>
    Task ExecuteAsync(TradePositionChangedEvent @event);

    /// <summary>Processes an option-leg data change.</summary>
    /// <param name="event">The option-leg event.</param>
    /// <returns>A task that completes when processing finishes.</returns>
    Task ExecuteAsync(OptionTradeLegDataChangedEvent @event);

    /// <summary>Processes updated option-spread distribution statistics.</summary>
    /// <param name="event">The distribution statistics event.</param>
    /// <returns>A task that completes when processing finishes.</returns>
    Task ExecuteAsync(OptionTradeSpreadDistributionStatisticsUpdatedEvent @event);
}
