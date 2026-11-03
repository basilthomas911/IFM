using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Event;
/// <summary>Handles the concrete ScheduledTaskCatalogProjectRegisteredComplete notification.</summary>
public static class ScheduledTaskCatalogProjectRegisteredComplete
{

    /// <param name="source">
    /// The concrete source-correlated notification.
    /// </param>
    extension(ScheduledTaskCatalogProjectRegisteredCompleteEvent source)
    {
        /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
        ///
        /// <param name="context">Readonly durable transport and logging services.</param>
        /// <returns>The bounded notification dispatch operation.</returns>
        public ValueTask ExecuteAsync(ScheduledTaskEventContext context)
        {
            return ValueTask.CompletedTask;
        }
    }
}
