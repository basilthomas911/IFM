using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Validation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command;
/// <summary>Handles the RegisterScheduledTaskProject command through one explicit decision and event application.</summary>
public static class RegisterScheduledTaskProject
{
    /// <summary>Computes the business change, rejects failed guards and applies one source event to the owning state.</summary>
    /// <param name="command">The concrete operation and originating command identity.</param>
    /// <param name="context">The owning actor's explicit services and clock.</param>
    /// <param name="state">The aggregate reconstructed from committed source events.</param>
    /// <returns>The command acknowledgement; the repository owns subsequent persistence.</returns>
    public static ServiceResult<GuidResult> Execute(this RegisterScheduledTaskProjectCommand command,
        ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ScheduledTaskCatalogCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var owner = context as ScheduledTaskCatalogCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
        var now = owner.Clock.GetUtcNow();
        var errorMsg = "ScheduledTaskCatalog.STATE.APPLY_FAILED;unable to apply RegisterScheduledTaskProject event";
        var updated = command.Compute(state.ScheduledTaskCatalog, now, out var scheduledTaskCatalogChange) switch
        {
            _ when !scheduledTaskCatalogChange.Accepted => command.UpdateFailed(ref errorMsg, scheduledTaskCatalogChange.RejectionReason),
            _ when !scheduledTaskCatalogChange.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "ScheduledTaskCatalog.IDENTITY.INVALID;computed aggregate identity is invalid"),
            _ => state.Update(command.CreateScheduledTaskCatalogProjectRegisteredEvent(scheduledTaskCatalogChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed scheduled-task catalog without mutating state or performing I/O.</summary>
    /// <param name="command">The concrete operation and originating identity.</param>
    /// <param name="current">The accepted business snapshot; computation never mutates it.</param>
    /// <param name="now">The explicit UTC instant from the owning actor clock.</param>
    /// <param name="scheduledTaskCatalogChange">The proposed business change or classified rejection.</param>
    /// <returns>Whether a proposed change was accepted.</returns>
    internal static bool Compute(this RegisterScheduledTaskProjectCommand command, ScheduledTaskCatalog? current,
        DateTimeOffset now, out ScheduledTaskCatalogChange scheduledTaskCatalogChange)
    {
        scheduledTaskCatalogChange = ScheduledTaskCatalogComputation.Compute(command, current, now);
        return scheduledTaskCatalogChange.Accepted;
    }
    /// <summary>Creates the source event with the originating command identity and calculated payload.</summary>
    /// <param name="command">The concrete operation and originating identity.</param>
    /// <param name="scheduledTaskCatalogChange">The proposed catalog or classified rejection.</param>
    /// <returns>The source event carrying the command identity and accepted business payload.</returns>
    internal static ScheduledTaskCatalogProjectRegisteredEvent CreateScheduledTaskCatalogProjectRegisteredEvent(this RegisterScheduledTaskProjectCommand command, ScheduledTaskCatalogChange scheduledTaskCatalogChange) => new()
    {
        CommandId = command.CommandId,
        OperationCommandId = command.OperationCommandId == Guid.Empty ? command.CommandId : command.OperationCommandId,
        Subject = new(ActorType.Event, ScheduledTaskCatalogProjectRegisteredEvent.Actor, ScheduledTaskCatalogProjectRegisteredEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        AggregateId = command.EntityId.Format(),
        EventSource = command.EventSource,
        ScheduledTaskCatalog = scheduledTaskCatalogChange.ScheduledTaskCatalog
    };
}
