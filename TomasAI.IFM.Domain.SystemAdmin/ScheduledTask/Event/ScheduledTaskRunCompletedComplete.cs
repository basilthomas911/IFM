using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Event;
/// <summary>Handles the concrete ScheduledTaskRunCompletedComplete notification.</summary>
public static class ScheduledTaskRunCompletedComplete
{

    /// <param name="source">
    /// The concrete source-correlated notification.
    /// </param>
    extension(ScheduledTaskRunCompletedCompleteEvent source)
    {
        /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
        ///
        /// <param name="context">The event actor context.</param>
        /// <param name="logger">The owning actor logger.</param>
        /// <returns>The bounded notification dispatch operation.</returns>
        public ValueTask ExecuteAsync(ScheduledTaskEventContext context, ILogger<ScheduledTaskEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(source);
            if (source.ScheduledTaskRun?.GetCompletedMarketCloseDate() is { } completedDate &&
                context.OperationalDate?.ApplyCompletedEndOfDay(completedDate) == true)
            {
                global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write6(logger, null, nameof(ScheduledTaskRunCompletedComplete), nameof(ExecuteAsync), completedDate, source.EntityId.Format(), source.CommandId);
            }
            return ValueTask.CompletedTask;
        }
    }
}
