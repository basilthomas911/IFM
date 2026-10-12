using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;

/// <summary>Writes required administrative projections on the caller-owned event transaction.</summary>
public interface IScheduledTaskTransactionalProjectionWriter
{
    string ProviderName { get; }
    string ConnectionString { get; }
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskDefinition value, CancellationToken token);
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskCatalog value, CancellationToken token);
    Task ProjectAsync(IEnlistedPostgresTransaction transaction, ScheduledTaskRun value, CancellationToken token);
}
