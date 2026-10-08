using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
/// <summary>Handles the concrete ScheduledTaskScheduleChangedComplete notification.</summary>
public static class ScheduledTaskScheduleChangedComplete
{
    /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
    /// <param name="source">The concrete source-correlated notification.</param>
    /// <param name="context">Readonly durable transport and logging services.</param>
    /// <returns>The bounded notification dispatch operation.</returns>
    public static ValueTask ExecuteAsync(this ScheduledTaskScheduleChangedCompleteEvent source, ScheduledTaskEventContext context)
    {
        return ScheduledTaskRuntimeDispatch.ApplyAsync(source, source.ScheduledTaskDefinition!, source.OperationCommandId, context);
    }
}
