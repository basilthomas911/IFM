using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Defines SystemAdmin projection mutations.</summary>
public interface ISystemAdminDbWriteContext
{
    ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupEventAsync(
        string projectorName,
        DatabaseBackupEventContract domainEvent,
        CancellationToken cancellationToken = default);
    ValueTask ClearDatabaseBackupProjectionsAsync(
        string projectorName,
        CancellationToken cancellationToken = default);
}
