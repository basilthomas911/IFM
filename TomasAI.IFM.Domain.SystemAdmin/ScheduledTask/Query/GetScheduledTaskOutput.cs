using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Reads scheduled-task logs through persisted identities without consulting command state.</summary>
public static class GetScheduledTaskOutput
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]     /// <summary>Reads a bounded page and replies through the actor query route.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTaskOutputQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, IScheduledTaskOutputReader output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
        var run = await (store.GetRunAsync(query.Environment, query.HostId, query.ScheduleId, query.EntityId, query.IntendedFireTimeUtc, cancellationToken)).ConfigureAwait(false) ?? throw new InvalidOperationException("ScheduledTask run projection was not found.");
        var result = await (output.ReadAsync(string.IsNullOrWhiteSpace(run.OutputDirectory) ? Path.Combine(run.TaskKey, run.Id.Format()) : run.OutputDirectory, query.Offset, cancellationToken)).ConfigureAwait(false);
        if (!result.Available) result = result with { Text = run.StandardOutputTail };
        await (context.ReplyAsync<ScheduledTaskOutputPage>(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ScheduledTaskOutputPage>(result))).ConfigureAwait(false);
    }
}
