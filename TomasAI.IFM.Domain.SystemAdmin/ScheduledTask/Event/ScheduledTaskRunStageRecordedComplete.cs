using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
/// <summary>Handles the concrete ScheduledTaskRunStageRecordedComplete notification.</summary>
public static class ScheduledTaskRunStageRecordedComplete
{
    /// <summary>Dispatches committed desired work or observes its explicit projection outcome.</summary>
    /// <param name="source">The concrete source-correlated notification.</param>
    /// <param name="context">Readonly durable transport and logging services.</param>
    /// <returns>The bounded notification dispatch operation.</returns>
    public static ValueTask ExecuteAsync(this ScheduledTaskRunStageRecordedCompleteEvent source, ScheduledTaskEventContext context)
    {
        if (source.ScheduledTaskRun?.CompletedEndOfDayValueDate is { } completedDate &&
            context.OperationalDate?.ApplyCompletedEndOfDay(completedDate) == true)
        {
            context.Logger.LogInformation("{Component}.{Method} Operational date advanced after persisted EOD completion for {ValueDate} {RunId} {CommandId}",
                nameof(ScheduledTaskRunStageRecordedComplete), nameof(ExecuteAsync), completedDate, source.EntityId.Format(), source.CommandId);
        }
        return ValueTask.CompletedTask;
    }
}
