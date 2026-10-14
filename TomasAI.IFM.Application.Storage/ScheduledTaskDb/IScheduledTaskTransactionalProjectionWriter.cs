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
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskDefinition value, CancellationToken token);
    /// <summary>Applies an accepted snapshot on the caller-owned transaction without committing it.</summary>
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskCatalog value, CancellationToken token);
    /// <summary>Applies an accepted snapshot on the caller-owned transaction without committing it.</summary>
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskRun value, CancellationToken token);
}