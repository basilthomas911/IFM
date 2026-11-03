using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Event;
/// <summary>Handles the concrete ScheduledTaskRunStageRecordedComplete notification.</summary>
public static class ScheduledTaskRunStageRecordedComplete
{

    /// <param name="source">
    /// The concrete source-correlated notification.
    /// </param>
    extension(ScheduledTaskRunStageRecordedCompleteEvent source)
    {
        /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
        ///
        /// <param name="context">Readonly durable transport and logging services.</param>
        /// <returns>The bounded notification dispatch operation.</returns>
        public ValueTask ExecuteAsync(ScheduledTaskEventContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(source);
            if (source.ScheduledTaskRun?.CompletedEndOfDayValueDate is { } completedDate &&
                context.OperationalDate?.ApplyCompletedEndOfDay(completedDate) == true)
            {
                global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write6(context.Logger, null, nameof(ScheduledTaskRunStageRecordedComplete), nameof(ExecuteAsync), completedDate, source.EntityId.Format(), source.CommandId);
            }
            return ValueTask.CompletedTask;
        }
    }
}
