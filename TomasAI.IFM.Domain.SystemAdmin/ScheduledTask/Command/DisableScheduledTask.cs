using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command;
/// <summary>Handles the DisableScheduledTask command through one explicit decision and event application.</summary>
public static class DisableScheduledTask
{

    /// <param name="command">
    /// The concrete operation and originating command identity.
    /// The concrete operation and originating identity.
    /// </param>
    extension(DisableScheduledTaskCommand command)
    {
        /// <summary>Computes the business change, rejects failed guards and applies one source event to the owning state.</summary>
        ///
        /// <param name="context">The owning actor's explicit services and clock.</param>
        /// <param name="state">The aggregate reconstructed from committed source events.</param>
        /// <returns>The command acknowledgement; the repository owns subsequent persistence.</returns>
        public ServiceResult<GuidResult> Execute(ICommandActorContext<ScheduledTaskCommandActor> context, ScheduledTaskCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(state);
            var owner = context as ScheduledTaskCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
            var now = owner.Clock.GetUtcNow();
            var errorMsg = "ScheduledTask.STATE.APPLY_FAILED;unable to apply DisableScheduledTask event";
            var updated = command.Compute(state.ScheduledTaskDefinition, now, out var scheduledTaskChange) switch
            {
                _ when !scheduledTaskChange.Accepted => command.UpdateFailed(ref errorMsg, scheduledTaskChange.RejectionReason),
                _ when !scheduledTaskChange.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "ScheduledTask.IDENTITY.INVALID;computed aggregate identity is invalid"),
                _ => state.Update(command.CreateScheduledTaskDisabledEvent(scheduledTaskChange), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
        }

        /// <summary>Computes the proposed scheduled-task definition without mutating state or performing I/O.</summary>
        ///
        /// <param name="current">The accepted business snapshot; computation never mutates it.</param>
        /// <param name="now">The explicit UTC instant from the owning actor clock.</param>
        /// <param name="scheduledTaskChange">The proposed business change or classified rejection.</param>
        /// <returns>Whether a proposed change was accepted.</returns>
        internal bool Compute(ScheduledTaskDefinition? current,
            DateTimeOffset now, out ScheduledTaskChange scheduledTaskChange)
        {
            scheduledTaskChange = ScheduledTaskComputation.Compute(command, current, now);
            return scheduledTaskChange.Accepted;
        }

        /// <summary>Creates the source event with the originating command identity and calculated payload.</summary>
        ///
        /// <param name="scheduledTaskChange">The proposed definition or classified rejection.</param>
        /// <returns>The source event carrying the command identity and accepted business payload.</returns>
        internal ScheduledTaskDisabledEvent CreateScheduledTaskDisabledEvent(ScheduledTaskChange scheduledTaskChange) => new()
        {
            CommandId = command.CommandId,
            OperationCommandId = command.OperationCommandId == Guid.Empty ? command.CommandId : command.OperationCommandId,
            Subject = new(ActorType.Event, ScheduledTaskDisabledEvent.Actor, ScheduledTaskDisabledEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ScheduledTaskDefinition = scheduledTaskChange.ScheduledTaskDefinition
        };
    }
}
