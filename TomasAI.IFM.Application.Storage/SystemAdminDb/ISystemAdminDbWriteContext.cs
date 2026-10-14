using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.Storage;
/// <summary>Defines SystemAdmin projection mutations.</summary>
public interface ISystemAdminDbWriteContext
{
    /// <summary>Writes backup read rows and their completion receipt on the source transaction.</summary>
    Task<EventProjectionApplyOutcome> ApplyDatabaseBackupSnapshotAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken = default);
    /// <summary>Applies accepted ApplyDatabaseBackupSnapshotAsync persistence values.</summary>
    ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupSnapshotAsync(string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken = default);
    /// <summary>Applies accepted ClearDatabaseBackupProjectionsAsync persistence values.</summary>
    ValueTask ClearDatabaseBackupProjectionsAsync(string projectorName, CancellationToken cancellationToken = default);
    /// <summary>Persists an accepted dispatch identity in the caller-owned source transaction.</summary>
    Task<int> EnqueueDatabaseBackupExecutionIntentAsync(IEnlistedPostgresTransaction transaction, long eventId, Guid eventUuid, CancellationToken cancellationToken = default);
    /// <summary>Acknowledges one published dispatch identity without deleting audit history.</summary>
    Task<int> MarkDatabaseBackupExecutionIntentPublishedAsync(long eventId, Guid eventUuid, DateTime publishedUtc, CancellationToken cancellationToken = default);
}