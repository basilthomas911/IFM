using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Defines SystemAdmin projection mutations.</summary>
public interface ISystemAdminDbWriteContext
{
    /// <summary>Writes backup read rows and their completion receipt on the source transaction.</summary>
    Task<EventProjectionApplyOutcome> ApplyDatabaseBackupEventAsync(
        TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        string projectorName, DatabaseBackupEventContract domainEvent, CancellationToken cancellationToken = default);
    ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupEventAsync(
        string projectorName,
        DatabaseBackupEventContract domainEvent,
        CancellationToken cancellationToken = default);
    ValueTask ClearDatabaseBackupProjectionsAsync(
        string projectorName,
        CancellationToken cancellationToken = default);
}
