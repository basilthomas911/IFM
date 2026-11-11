using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Defines write operations for Market Data Service persistence.</summary>
public interface IMarketDataServiceDbWriteContext
{
    /// <summary>Inserts or updates a Databento contract assignment.</summary>
    /// <param name="assignment">The assignment supplied by the caller.</param>
    /// <param name="expectedRowVersion">The expected row version supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(FuturesRolloverContractAssignment assignment, long expectedRowVersion, CancellationToken cancellationToken = default);
    /// <summary>Deletes a Databento contract assignment.</summary>
    /// <param name="role">The role supplied by the caller.</param>
    /// <param name="expectedRowVersion">The expected row version supplied by the caller.</param>
    /// <param name="deletedBy">The deleted by supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
    /// <summary>Atomically replaces the front- and second-month VX assignments.</summary>
    /// <param name="front">The front supplied by the caller.</param>
    /// <param name="second">The second supplied by the caller.</param>
    /// <param name="expectedFrontVersion">The expected front version supplied by the caller.</param>
    /// <param name="expectedSecondVersion">The expected second version supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second, long expectedFrontVersion, long expectedSecondVersion, CancellationToken cancellationToken = default);
    /// <summary>Inserts a watchdog observation with the identifier supplied by the owner.</summary>
    /// <param name="observation">The complete accepted observation.</param>
    /// <param name="cancellationToken">Cancels the database write.</param>
    /// <returns>Completion of the idempotent write.</returns>
    Task InsertWatchdogObservationAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken = default);
    /// <summary>Updates a watchdog observation.</summary>
    /// <param name="observation">The observation supplied by the caller.</param>
    /// <param name="expectedRowVersion">The expected row version supplied by the caller.</param>
    /// <param name="changedBy">The changed by supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<DatabentoWatchdogObservation> UpdateObservationAsync(DatabentoWatchdogObservation observation, long expectedRowVersion, string changedBy, CancellationToken cancellationToken = default);
    /// <summary>Deletes a watchdog observation.</summary>
    /// <param name="id">The id supplied by the caller.</param>
    /// <param name="expectedRowVersion">The expected row version supplied by the caller.</param>
    /// <param name="deletedBy">The deleted by supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
    /// <summary>Persists a dataset incident transition idempotently.</summary>
    /// <param name="transition">The transition supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<DatasetIncidentTransition> PersistDatasetIncidentAsync(DatasetIncidentTransition transition, CancellationToken cancellationToken = default);
    /// <summary>Persists an immutable composition route plan idempotently.</summary>
    /// <param name="plan">The plan supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task SaveAsync(CompositionRoutePlan plan, CancellationToken cancellationToken);
    /// <summary>Applies a durable subscription authority mutation.</summary>
    /// <param name="command">The prepared command supplied by the owner.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<DurableIntentResult> CommitAsync(DurableSubscriptionCommand command, CancellationToken cancellationToken = default);
    /// <summary>Acknowledges delivery of a durable subscription outbox item.</summary>
    /// <param name="scope">The scope supplied by the caller.</param>
    /// <param name="dataset">The dataset supplied by the caller.</param>
    /// <param name="transitionId">The transition id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    Task<bool> AcknowledgeOutboxAsync(string scope, string dataset, Guid transitionId, CancellationToken cancellationToken = default);
    /// <summary>Appends the accepted recovery projection without overwriting existing evidence.</summary>
    /// <param name="row">The qualified immutable audit row.</param>
    /// <param name="token">Cancels the write.</param>
    /// <returns>Completion when the row is inserted or an identical row exists.</returns>
    Task AppendRecoveryLogAsync(RecoveryLogRow row, CancellationToken token = default);
}
