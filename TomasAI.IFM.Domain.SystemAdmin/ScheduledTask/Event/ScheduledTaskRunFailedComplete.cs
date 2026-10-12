using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
/// <summary>Handles the concrete ScheduledTaskRunFailedComplete notification.</summary>
public static class ScheduledTaskRunFailedComplete
{
    /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
    /// <param name="source">The concrete source-correlated notification.</param>
    /// <param name="context">Readonly durable transport and logging services.</param>
    /// <returns>The bounded notification dispatch operation.</returns>
    public static async ValueTask ExecuteAsync(this ScheduledTaskRunFailedCompleteEvent source, ScheduledTaskEventContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        if (source.ScheduledTaskRun is not { Stage: "OperatorResolved" } run) return;
        var command = new TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands.RecordScheduledTaskRunCompletionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = run.ScheduleId, RunId = run.Id.Value,
            Operator = "ScheduledTaskEvent", OperationCommandId = run.OperationCommandId,
            Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Command,
                TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands.RecordScheduledTaskRunCompletionCommand.Actor,
                TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands.RecordScheduledTaskRunCompletionCommand.Verb, run.ScheduleId.Format())
        };
        var result = await (context.RequestAsync<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands.RecordScheduledTaskRunCompletionCommand, TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.ScheduledTaskId>(command)).ConfigureAwait(false);
        if (!result.Success) global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write12(context.Logger, null, nameof(ScheduledTaskRunFailedComplete), nameof(ExecuteAsync), run.Id.Format(), result.ErrorMessage);
    }
}
