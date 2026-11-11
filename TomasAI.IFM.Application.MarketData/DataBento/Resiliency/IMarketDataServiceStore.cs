namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Application port for qualified rollover and watchdog operations.</summary>
public interface IMarketDataServiceStore : IMarketDataServiceStateStore
{
    /// <summary>Records the observation once, returning the existing row for an identical identity.</summary>
    Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation,
        CancellationToken cancellationToken = default);
}
