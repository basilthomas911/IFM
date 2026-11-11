namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Exact watchdog lookup and prepared write operations supplied by persistence.</summary>
public interface IWatchdogObservationPersistence
{
    /// <summary>Reads the row for one stable observation identity.</summary>
    /// <param name="observationId">The originating observation identity.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The committed row, or null.</returns>
    Task<DatabentoWatchdogObservation?> ReadWatchdogObservationAsync(Guid observationId, CancellationToken cancellationToken = default);

    /// <summary>Inserts the supplied row without replacing an existing observation.</summary>
    /// <param name="observation">The owner-prepared observation and allocated identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of the idempotent database mutation.</returns>
    Task InsertWatchdogObservationAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken = default);
}
