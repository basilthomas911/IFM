namespace TomasAI.IFM.Application.MarketData;

/// <summary>Initializes the value-date-specific analytics before confirming a requested feed start.</summary>
public interface IMarketSessionAnalyticsInitializer
{
    /// <summary>Seeds and confirms the current session's indicator consumers; failed attempts remain retryable.</summary>
    Task InitializeAsync(DateOnly valueDate, CancellationToken cancellationToken);
}
