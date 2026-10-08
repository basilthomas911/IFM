using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command;
/// <summary>Computes and applies the RecordScheduledTaskHostCapability operation through its source event.</summary>
public static class RecordScheduledTaskHostCapability
{
    /// <summary>Checks failure guards before applying the single computed business event.</summary>
    /// <param name="command">The requested operation and originating command identity.</param>
    /// <param name="state">The owning command state reconstructed from persisted events.</param>
    /// <param name="now">The current UTC instant supplied by the actor clock.</param>

    /// <returns>The command identity, or the rejected calculation/application reason.</returns>
    public static ServiceResult<GuidResult> Execute(this RecordScheduledTaskHostCapabilityCommand command,
        ScheduledTaskCatalogCommandState state, DateTimeOffset now)
    {
        var errorMsg = "ScheduledTaskCatalog.STATE.APPLY_FAILED;unable to apply RecordScheduledTaskHostCapability event";
        var updated = command.Compute(state.ScheduledTaskCatalog, now, out var model) switch
        {
            _ when !model.Accepted => command.UpdateFailed(ref errorMsg, model.RejectionReason),
            _ when !model.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "ScheduledTaskCatalog.IDENTITY.INVALID;computed aggregate identity is invalid"),
            _ => state.Update(command.CreateScheduledTaskHostCapabilityRecordedEvent(model), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }
    /// <summary>Produces a business model without mutating state or performing I/O.</summary>
    internal static bool Compute(this RecordScheduledTaskHostCapabilityCommand command, ScheduledTaskCatalog? current,
        DateTimeOffset now, out ScheduledTaskCatalogChange model)
    {
        model = ScheduledTaskCatalogComputation.Compute(command, current, now);
        return model.Accepted;
    }
    /// <summary>Creates the source event with the originating command identity and calculated payload.</summary>
    internal static ScheduledTaskHostCapabilityRecordedEvent CreateScheduledTaskHostCapabilityRecordedEvent(this RecordScheduledTaskHostCapabilityCommand command, ScheduledTaskCatalogChange model) => new()
    {
        CommandId = command.CommandId,
        OperationCommandId = command.OperationCommandId == Guid.Empty ? command.CommandId : command.OperationCommandId,
        Subject = new(ActorType.Event, ScheduledTaskHostCapabilityRecordedEvent.Actor, ScheduledTaskHostCapabilityRecordedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        AggregateId = command.EntityId.Format(),
        EventSource = command.EventSource,
        ScheduledTaskCatalog = model.ScheduledTaskCatalog
    };
}
