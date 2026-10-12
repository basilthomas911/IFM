using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
/// <summary>Handles the concrete ScheduledTaskRunRequestedFail notification.</summary>
public static class ScheduledTaskRunRequestedFail
{
    /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
    /// <param name="source">The concrete source-correlated notification.</param>
    /// <param name="context">Readonly durable transport and logging services.</param>
    /// <returns>The bounded notification dispatch operation.</returns>
    public static ValueTask ExecuteAsync(this ScheduledTaskRunRequestedFailEvent source, ScheduledTaskEventContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write7(context.Logger, null, nameof(ScheduledTaskRunRequestedFail), nameof(ExecuteAsync), source.EventName, source.EntityId.Format(), source.CommandId, source.ErrorMessage);
        return ValueTask.CompletedTask;
    }
}
