using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command;
/// <summary>Computes and applies the RemoveScheduledTask operation through its source event.</summary>
public static class RemoveScheduledTask
{
    /// <summary>Checks failure guards before applying the single computed business event.</summary>
    /// <param name="command">The requested operation and originating command identity.</param>
    /// <param name="state">The owning command state reconstructed from persisted events.</param>
    /// <param name="now">The current UTC instant supplied by the actor clock.</param>
    /// <param name="catalog">Persisted host deployment facts used for admission.</param>
    /// <returns>The command identity, or the rejected calculation/application reason.</returns>
    public static ServiceResult<GuidResult> Execute(this RemoveScheduledTaskCommand command,
        ScheduledTaskCommandState state, DateTimeOffset now, ScheduledTaskCatalog? catalog)
    {
        var errorMsg = "ScheduledTask.STATE.APPLY_FAILED;unable to apply RemoveScheduledTask event";
        var updated = command.Compute(state.ScheduledTaskDefinition, now, catalog, out var model) switch
        {
            _ when !model.Accepted => command.UpdateFailed(ref errorMsg, model.RejectionReason),
            _ when !model.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "ScheduledTask.IDENTITY.INVALID;computed aggregate identity is invalid"),
            _ => state.Update(command.CreateScheduledTaskRemovedEvent(model), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }
    /// <summary>Produces a business model without mutating state or performing I/O.</summary>
    internal static bool Compute(this RemoveScheduledTaskCommand command, ScheduledTaskDefinition? current,
        DateTimeOffset now, ScheduledTaskCatalog? catalog, out ScheduledTaskChange model)
    {
        model = ScheduledTaskComputation.Compute(command, current, now, catalog);
        return model.Accepted;
    }
    /// <summary>Creates the source event with the originating command identity and calculated payload.</summary>
    internal static ScheduledTaskRemovedEvent CreateScheduledTaskRemovedEvent(this RemoveScheduledTaskCommand command, ScheduledTaskChange model) => new()
    {
        CommandId = command.CommandId,
        OperationCommandId = command.OperationCommandId == Guid.Empty ? command.CommandId : command.OperationCommandId,
        Subject = new(ActorType.Event, ScheduledTaskRemovedEvent.Actor, ScheduledTaskRemovedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        AggregateId = command.EntityId.Format(),
        EventSource = command.EventSource,
        ScheduledTaskDefinition = model.ScheduledTaskDefinition
    };
}
