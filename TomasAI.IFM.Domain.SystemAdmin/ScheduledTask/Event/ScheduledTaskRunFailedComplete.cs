using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Event;
/// <summary>Handles the concrete ScheduledTaskRunFailedComplete notification.</summary>
public static class ScheduledTaskRunFailedComplete
{

    /// <param name="source">
    /// The concrete source-correlated notification.
    /// </param>
    extension(ScheduledTaskRunFailedCompleteEvent source)
    {
        /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
        ///
        /// <param name="context">The event actor context.</param>
        /// <param name="logger">The owning actor logger.</param>
        /// <returns>The bounded notification dispatch operation.</returns>
        public async ValueTask ExecuteAsync(ScheduledTaskEventContext context, ILogger<ScheduledTaskEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(source);
            if (source.ScheduledTaskRun is not { Stage: "OperatorResolved" } run) return;
            var command = new RecordScheduledTaskRunCompletionCommand
            {
                CommandId = ScheduledTaskIdentities.OperatorResolutionRelease(run.Id),
                EntityId = run.ScheduleId,
                RunId = run.Id.Value,
                Operator = "ScheduledTaskEvent",
                OperationCommandId = run.OperationCommandId,
                Subject = new(ActorType.Command, RecordScheduledTaskRunCompletionCommand.Actor,
                    RecordScheduledTaskRunCompletionCommand.Verb, run.ScheduleId.Format())
            };
            var result = await context.RequestAsync<RecordScheduledTaskRunCompletionCommand, ScheduledTaskId>(command).ConfigureAwait(false);
            if (!result.Success) global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write12(logger, null, nameof(ScheduledTaskRunFailedComplete), nameof(ExecuteAsync), run.Id.Format(), result.ErrorMessage);
        }
    }
}
