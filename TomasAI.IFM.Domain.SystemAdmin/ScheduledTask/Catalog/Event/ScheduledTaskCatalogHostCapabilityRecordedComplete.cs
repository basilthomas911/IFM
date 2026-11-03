using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Event;
/// <summary>Handles the concrete ScheduledTaskCatalogHostCapabilityRecordedComplete notification.</summary>
public static class ScheduledTaskCatalogHostCapabilityRecordedComplete
{
    /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
    /// <param name="source">The concrete source-correlated notification.</param>
    /// <param name="context">Readonly durable transport and logging services.</param>
    /// <returns>The bounded notification dispatch operation.</returns>
    public static ValueTask ExecuteAsync(this ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent source, ScheduledTaskEventContext context)
    {
        return ValueTask.CompletedTask;
    }
}
