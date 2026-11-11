using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Writes read models only from committed scheduled-task source events.</summary>
public interface IScheduledTaskProjectionWriter
{
    /// <summary>Projects one definition with revision-fenced PostgreSQL writes.</summary>
    /// <param name="definition">The complete definition from a committed source event.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>Completion of the accepted projection write.</returns>
    ValueTask ProjectAsync(ScheduledTaskDefinition definition, CancellationToken cancellationToken = default);
    /// <summary>Projects one catalog with revision-fenced PostgreSQL writes.</summary>
    /// <param name="catalog">The complete catalog from a committed source event.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>Completion of the accepted projection write.</returns>
    ValueTask ProjectAsync(ScheduledTaskCatalog catalog, CancellationToken cancellationToken = default);
    /// <summary>Projects one run with revision-fenced PostgreSQL writes.</summary>
    /// <param name="run">The complete run from a committed source event.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>Completion of the accepted projection write.</returns>
    ValueTask ProjectAsync(ScheduledTaskRun run, CancellationToken cancellationToken = default);
}
