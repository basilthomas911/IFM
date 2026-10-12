using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Reads scheduled-task logs through persisted identities without consulting command state.</summary>
public static class GetScheduledTaskRunHistory
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]     /// <summary>Reads a bounded page and replies through the actor query route.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTaskRunHistoryQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
        var result = await (store.GetRunHistoryAsync(query.Environment, query.HostId, query.ScheduleId, query.PageSize, query.PagingState, cancellationToken)).ConfigureAwait(false);
        await (context.ReplyAsync<ScheduledTaskRunPage>(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ScheduledTaskRunPage>(result))).ConfigureAwait(false);
    }
}
