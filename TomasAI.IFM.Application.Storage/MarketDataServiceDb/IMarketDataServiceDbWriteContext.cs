using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Defines write operations for Market Data Service persistence.</summary>
public interface IMarketDataServiceDbWriteContext
{
    /// <summary>Inserts or updates a Databento contract assignment.</summary>
    Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(FuturesRolloverContractAssignment assignment, long expectedRowVersion, CancellationToken cancellationToken = default);
    /// <summary>Deletes a Databento contract assignment.</summary>
    Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
    /// <summary>Atomically replaces the front- and second-month VX assignments.</summary>
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second, long expectedFrontVersion, long expectedSecondVersion, CancellationToken cancellationToken = default);
    /// <summary>Appends a watchdog observation idempotently.</summary>
    Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken = default);
    /// <summary>Updates a watchdog observation.</summary>
    Task<DatabentoWatchdogObservation> UpdateObservationAsync(DatabentoWatchdogObservation observation, long expectedRowVersion, string changedBy, CancellationToken cancellationToken = default);
    /// <summary>Deletes a watchdog observation.</summary>
    Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
    /// <summary>Persists a dataset incident transition idempotently.</summary>
    Task<DatasetIncidentTransition> PersistDatasetIncidentAsync(DatasetIncidentTransition transition, CancellationToken cancellationToken = default);
    /// <summary>Persists an immutable composition route plan idempotently.</summary>
    Task SaveAsync(CompositionRoutePlan plan, CancellationToken cancellationToken);
    /// <summary>Applies a durable subscription authority mutation.</summary>
    Task<DurableIntentResult> ApplyAsync(DurableAuthorityMutation mutation, CancellationToken cancellationToken = default);
    /// <summary>Acknowledges delivery of a durable subscription outbox item.</summary>
    Task<bool> AcknowledgeOutboxAsync(string scope, string dataset, Guid transitionId, CancellationToken cancellationToken = default);
}
