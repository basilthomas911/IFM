using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Event;
/// <summary>Handles the concrete ScheduledTaskRunRequestedComplete notification.</summary>
public static class ScheduledTaskRunRequestedComplete
{

    /// <param name="source">
    /// The concrete source-correlated notification.
    /// </param>
    extension(ScheduledTaskRunRequestedCompleteEvent source)
    {
        /// <summary>Observes a request whose host publication has already been handled by its projector.</summary>
        ///
        /// <param name="context">The event actor context.</param>
        /// <param name="logger">The owning actor logger.</param>
        /// <returns>The bounded notification dispatch operation.</returns>
        public ValueTask ExecuteAsync(ScheduledTaskEventContext context, ILogger<ScheduledTaskEventActor> logger)
        {
            return ValueTask.CompletedTask;
        }
    }
}
