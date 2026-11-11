using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.Storage;
/// <summary>Defines SystemAdmin projection mutations.</summary>
public interface ISystemAdminDbWriteContext
{
    /// <summary>Writes backup read rows and their completion receipt on the source transaction.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction; this method does not commit it.</param>
    /// <param name="projectorName">The stable projector identity supplied by the owning domain.</param>
    /// <param name="snapshot">Accepted projection rows prepared from an authoritative domain event.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    Task<EventProjectionApplyOutcome> ApplyDatabaseBackupSnapshotAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken = default);
    /// <summary>Applies accepted ApplyDatabaseBackupSnapshotAsync persistence values.</summary>
    /// <param name="projectorName">The stable projector identity supplied by the owning domain.</param>
    /// <param name="snapshot">Accepted projection rows prepared from an authoritative domain event.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupSnapshotAsync(string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken = default);
    /// <summary>Applies accepted ClearDatabaseBackupProjectionsAsync persistence values.</summary>
    /// <param name="projectorName">The stable projector identity supplied by the owning domain.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>Completion of the persistence operation.</returns>
    ValueTask ClearDatabaseBackupProjectionsAsync(string projectorName, CancellationToken cancellationToken = default);
    /// <summary>Persists an accepted dispatch identity in the caller-owned source transaction.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction; this method does not commit it.</param>
    /// <param name="eventId">The persisted source event revision.</param>
    /// <param name="eventUuid">The immutable source event identity.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> EnqueueDatabaseBackupExecutionIntentAsync(IEnlistedPostgresTransaction transaction, long eventId, Guid eventUuid, CancellationToken cancellationToken = default);
    /// <summary>Acknowledges one published dispatch identity without deleting audit history.</summary>
    /// <param name="eventId">The persisted source event revision.</param>
    /// <param name="eventUuid">The immutable source event identity.</param>
    /// <param name="publishedUtc">The UTC publication acknowledgement time.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> MarkDatabaseBackupExecutionIntentPublishedAsync(long eventId, Guid eventUuid, DateTime publishedUtc, CancellationToken cancellationToken = default);
}
