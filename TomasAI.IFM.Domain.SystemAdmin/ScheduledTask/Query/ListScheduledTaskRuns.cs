using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Handles ListScheduledTaskRuns without consulting command-state objects.</summary>
public static class ListScheduledTaskRuns
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]     /// <summary>Reads persisted projections or previews the supplied timing, then sends a typed response.</summary>
    public static async ValueTask ExecuteAsync(this ListScheduledTaskRunsQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
        var result = await store.GetRunsAsync(query.Environment, query.HostId, query.ScheduleId, query.PageSize, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync<ScheduledTaskRun[]>(query.Subject.ThreadId, query.Subject.Verb,
            result is null ? new ServiceFailed<ScheduledTaskRun[]>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTaskRun[]>(result)).ConfigureAwait(false);
    }
}
