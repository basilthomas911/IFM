using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Defines read operations for Market Data Service persistence.</summary>
public interface IMarketDataServiceDbReadContext
{
    /// <summary>Gets the assignment for a Databento contract role.</summary>
    Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role, CancellationToken cancellationToken = default);
    /// <summary>Gets all current Databento contract assignments.</summary>
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a watchdog observation by its storage identifier.</summary>
    Task<DatabentoWatchdogObservation?> GetObservationAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>Gets watchdog observations matching the supplied filters.</summary>
    Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(DateOnly? valueDate = null, DatabentoMajorStatus? status = null, int pageSize = 100, CancellationToken cancellationToken = default);
    /// <summary>Gets all currently open dataset incidents.</summary>
    Task<IReadOnlyList<DatasetIncidentTransition>> ListOpenDatasetIncidentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets an immutable composition route plan by content identity.</summary>
    Task<CompositionRoutePlan?> ReadAsync(string planId, CancellationToken cancellationToken);
    /// <summary>Gets the durable subscription snapshot for a scope and dataset.</summary>
    Task<DurableSubscriptionSnapshot> ReadAsync(string scope, string dataset, CancellationToken cancellationToken = default);
    /// <summary>Gets a previously persisted durable-intent operation result.</summary>
    Task<DurableIntentResult?> FindOperationAsync(string scope, string dataset, Guid operationId, CancellationToken cancellationToken = default);
    /// <summary>Gets pending durable subscription outbox items.</summary>
    Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingOutboxAsync(string scope, string dataset, int pageSize = 100, CancellationToken cancellationToken = default);
}
