using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
/// <summary>Handles the concrete ScheduledTaskScheduleChangedFail notification.</summary>
public static class ScheduledTaskScheduleChangedFail
{

    /// <param name="source">
    /// The concrete source-correlated notification.
    /// </param>
    extension(ScheduledTaskScheduleChangedFailEvent source)
    {
        /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
        ///
        /// <param name="context">Readonly durable transport and logging services.</param>
        /// <returns>The bounded notification dispatch operation.</returns>
        public ValueTask ExecuteAsync(ScheduledTaskEventContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(source);
            global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write1(context.Logger, null, nameof(ScheduledTaskScheduleChangedFail), nameof(ExecuteAsync), source.EventName, source.EntityId.Format(), source.CommandId, source.ErrorMessage);
            return ValueTask.CompletedTask;
        }
    }
}
