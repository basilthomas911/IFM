namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Writes read models only from committed scheduled-task source events.</summary>
public interface IScheduledTaskProjectionWriter
{
    /// <summary>Projects one definition with revision-fenced PostgreSQL writes.</summary>
    ValueTask ProjectAsync(ScheduledTaskDefinition definition, CancellationToken cancellationToken = default);
    /// <summary>Projects one catalog with revision-fenced PostgreSQL writes.</summary>
    ValueTask ProjectAsync(ScheduledTaskCatalog catalog, CancellationToken cancellationToken = default);
    /// <summary>Projects one run with revision-fenced PostgreSQL writes.</summary>
    ValueTask ProjectAsync(ScheduledTaskRun run, CancellationToken cancellationToken = default);
}
