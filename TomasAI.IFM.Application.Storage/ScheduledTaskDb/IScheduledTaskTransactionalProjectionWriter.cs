using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Writes required administrative projections on the caller-owned event transaction.</summary>
public interface IScheduledTaskTransactionalProjectionWriter
{
    /// <summary>The configured storage provider.</summary>
    string ProviderName { get; }

    /// <summary>The connection used to validate transaction enlistment.</summary>
    string ConnectionString { get; }

    /// <summary>Applies an accepted snapshot on the caller-owned transaction without committing it.</summary>
    /// <param name="transaction">The caller-owned transaction; this method does not commit it.</param>
    /// <param name="value">The accepted complete snapshot from a committed source event.</param>
    /// <param name="token">Cancels the enlisted persistence operation.</param>
    /// <returns>Completion of the accepted projection write.</returns>
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskDefinition value, CancellationToken token);
    /// <summary>Applies an accepted snapshot on the caller-owned transaction without committing it.</summary>
    /// <param name="transaction">The caller-owned transaction; this method does not commit it.</param>
    /// <param name="value">The accepted complete snapshot from a committed source event.</param>
    /// <param name="token">Cancels the enlisted persistence operation.</param>
    /// <returns>Completion of the accepted projection write.</returns>
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskCatalog value, CancellationToken token);
    /// <summary>Applies an accepted snapshot on the caller-owned transaction without committing it.</summary>
    /// <param name="transaction">The caller-owned transaction; this method does not commit it.</param>
    /// <param name="value">The accepted complete snapshot from a committed source event.</param>
    /// <param name="token">Cancels the enlisted persistence operation.</param>
    /// <returns>Completion of the accepted projection write.</returns>
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskRun value, CancellationToken token);
}
