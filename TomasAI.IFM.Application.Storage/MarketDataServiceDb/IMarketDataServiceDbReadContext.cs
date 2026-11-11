using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Defines read operations for Market Data Service persistence.</summary>
public interface IMarketDataServiceDbReadContext
{
    /// <summary>Reads one accepted watchdog observation by its stable origin identity.</summary>
    /// <param name="observationId">The originating observation identity.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The committed observation, or null.</returns>
    Task<DatabentoWatchdogObservation?> ReadWatchdogObservationAsync(Guid observationId, CancellationToken cancellationToken = default);

    /// <summary>Gets the assignment for a Databento contract role.</summary>
    /// <param name="role">The role supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role, CancellationToken cancellationToken = default);
    /// <summary>Gets all current Databento contract assignments.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a watchdog observation by its storage identifier.</summary>
    /// <param name="id">The id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<DatabentoWatchdogObservation?> GetObservationAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>Gets watchdog observations matching the supplied filters.</summary>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="status">The status supplied by the caller.</param>
    /// <param name="pageSize">The page size supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(DateOnly? valueDate = null, DatabentoMajorStatus? status = null, int pageSize = 100, CancellationToken cancellationToken = default);
    /// <summary>Gets all currently open dataset incidents.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<IReadOnlyList<DatasetIncidentTransition>> ListOpenDatasetIncidentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets an immutable composition route plan by content identity.</summary>
    /// <param name="planId">The plan id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<CompositionRoutePlan?> ReadAsync(string planId, CancellationToken cancellationToken);
    /// <summary>Gets the durable subscription snapshot for a scope and dataset.</summary>
    /// <param name="scope">The scope supplied by the caller.</param>
    /// <param name="dataset">The dataset supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<DurableSubscriptionSnapshot> ReadAsync(string scope, string dataset, CancellationToken cancellationToken = default);
    /// <summary>Gets a previously persisted durable-intent operation result.</summary>
    /// <param name="scope">The scope supplied by the caller.</param>
    /// <param name="dataset">The dataset supplied by the caller.</param>
    /// <param name="operationId">The operation id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<DurableIntentResult?> FindOperationAsync(string scope, string dataset, Guid operationId, CancellationToken cancellationToken = default);
    /// <summary>Gets pending durable subscription outbox items.</summary>
    /// <param name="scope">The scope supplied by the caller.</param>
    /// <param name="dataset">The dataset supplied by the caller.</param>
    /// <param name="pageSize">The page size supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingOutboxAsync(string scope, string dataset, int pageSize = 100, CancellationToken cancellationToken = default);
    /// <summary>Reads one ordered page from the committed recovery log.</summary>
    /// <param name="valueDate">The requested trading date.</param>
    /// <param name="dataset">An exact dataset filter, or null to include all.</param>
    /// <param name="operationId">An exact incident filter, or null to include all.</param>
    /// <param name="limit">The caller-qualified maximum number of records.</param>
    /// <param name="token">Cancels the query.</param>
    /// <returns>Stored JSON rows ordered by incident and sequence.</returns>
    Task<IReadOnlyList<string>> ReadRecoveryLogAsync(DateOnly valueDate, string? dataset, Guid? operationId, int limit, CancellationToken token = default);
}
