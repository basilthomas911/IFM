using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Handles GetScheduledTaskRun without consulting command-state objects.</summary>
public static class GetScheduledTaskRun
{
    /// <summary>Reads persisted projections or previews the supplied timing, then sends a typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTaskRunQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId);
        var result = await store.GetRunAsync(query.Environment, query.HostId, query.ScheduleId, query.EntityId, query.IntendedFireTimeUtc, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync<ScheduledTaskRun>(query.Subject.ThreadId, query.Subject.Verb,
            result is null ? new ServiceFailed<ScheduledTaskRun>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTaskRun>(result)).ConfigureAwait(false);
    }
}
