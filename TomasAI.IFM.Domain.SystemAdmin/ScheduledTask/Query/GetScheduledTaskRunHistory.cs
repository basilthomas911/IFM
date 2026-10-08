using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Reads scheduled-task logs through persisted identities without consulting command state.</summary>
public static class GetScheduledTaskRunHistory
{
    /// <summary>Reads a bounded page and replies through the actor query route.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTaskRunHistoryQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId);
        var result = await store.GetRunHistoryAsync(query.Environment, query.HostId, query.ScheduleId, query.PageSize, query.PagingState, cancellationToken);
        await context.ReplyAsync<ScheduledTaskRunPage>(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ScheduledTaskRunPage>(result));
    }
}
