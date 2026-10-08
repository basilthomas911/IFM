using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Reads scheduled-task logs through persisted identities without consulting command state.</summary>
public static class GetScheduledTaskOutput
{
    /// <summary>Reads a bounded page and replies through the actor query route.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTaskOutputQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, IScheduledTaskOutputReader output, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId);
        var run = await store.GetRunAsync(query.Environment, query.HostId, query.ScheduleId, query.EntityId, query.IntendedFireTimeUtc, cancellationToken) ?? throw new InvalidOperationException("ScheduledTask run projection was not found.");
        var result = await output.ReadAsync(string.IsNullOrWhiteSpace(run.OutputDirectory) ? Path.Combine(run.TaskKey, run.Id.Format()) : run.OutputDirectory, query.Offset, cancellationToken);
        if (!result.Available) result = result with { Text = run.StandardOutputTail };
        await context.ReplyAsync<ScheduledTaskOutputPage>(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ScheduledTaskOutputPage>(result));
    }
}
