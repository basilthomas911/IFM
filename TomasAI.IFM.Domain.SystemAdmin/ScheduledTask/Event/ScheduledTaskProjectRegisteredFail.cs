using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
/// <summary>Handles the concrete ScheduledTaskProjectRegisteredFail notification.</summary>
public static class ScheduledTaskProjectRegisteredFail
{
    /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
    /// <param name="source">The concrete source-correlated notification.</param>
    /// <param name="context">Readonly durable transport and logging services.</param>
    /// <returns>The bounded notification dispatch operation.</returns>
    public static ValueTask ExecuteAsync(this ScheduledTaskProjectRegisteredFailEvent source, ScheduledTaskEventContext context)
    {
        context.Logger.LogError("{Component}.{Method} Projection failed for {EventName} {AggregateId} {CommandId}: {ErrorMessage}", nameof(ScheduledTaskProjectRegisteredFail), nameof(ExecuteAsync), source.EventName, source.EntityId.Format(), source.CommandId, source.ErrorMessage);
        return ValueTask.CompletedTask;
    }
}
