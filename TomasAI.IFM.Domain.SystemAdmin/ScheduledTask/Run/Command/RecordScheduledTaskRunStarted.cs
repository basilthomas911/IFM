using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Validation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command;
/// <summary>Handles the RecordScheduledTaskRunStarted command through one explicit decision and event application.</summary>
public static class RecordScheduledTaskRunStarted
{

    /// <param name="command">
    /// The concrete operation and originating command identity.
    /// The concrete operation and originating identity.
    /// </param>
    extension(RecordScheduledTaskRunStartedCommand command)
    {
        /// <summary>Computes the business change, rejects failed guards and applies one source event to the owning state.</summary>
        ///
        /// <param name="context">The owning actor's explicit services and clock.</param>
        /// <param name="state">The aggregate reconstructed from committed source events.</param>
        /// <returns>The command acknowledgement; the repository owns subsequent persistence.</returns>
        public ServiceResult<GuidResult> Execute(ICommandActorContext<ScheduledTaskRunCommandActor> context, ScheduledTaskRunCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(state);
            var owner = context as ScheduledTaskRunCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
            var now = owner.Clock.GetUtcNow();
            var errorMsg = "ScheduledTaskRun.STATE.APPLY_FAILED;unable to apply RecordScheduledTaskRunStarted event";
            var updated = command.Compute(state.ScheduledTaskRun, now, out var scheduledTaskRunChange) switch
            {
                _ when !scheduledTaskRunChange.Accepted => command.UpdateFailed(ref errorMsg, scheduledTaskRunChange.RejectionReason),
                _ when !scheduledTaskRunChange.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "ScheduledTaskRun.IDENTITY.INVALID;computed aggregate identity is invalid"),
                _ => state.Update(command.CreateScheduledTaskRunStartedEvent(scheduledTaskRunChange), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
        }

        /// <summary>Computes the proposed scheduled-task run without mutating state or performing I/O.</summary>
        ///
        /// <param name="current">The accepted business snapshot; computation never mutates it.</param>
        /// <param name="now">The explicit UTC instant from the owning actor clock.</param>
        /// <param name="scheduledTaskRunChange">The proposed business change or classified rejection.</param>
        /// <returns>Whether a proposed change was accepted.</returns>
        internal bool Compute(ScheduledTaskRun? current,
            DateTimeOffset now, out ScheduledTaskRunChange scheduledTaskRunChange)
        {
            scheduledTaskRunChange = ScheduledTaskRunComputation.Compute(command, current, now);
            return scheduledTaskRunChange.Accepted;
        }

        /// <summary>Creates the source event with the originating command identity and calculated payload.</summary>
        ///
        /// <param name="scheduledTaskRunChange">The proposed run receipt or classified rejection.</param>
        /// <returns>The source event carrying the command identity and accepted business payload.</returns>
        internal ScheduledTaskRunStartedEvent CreateScheduledTaskRunStartedEvent(ScheduledTaskRunChange scheduledTaskRunChange) => new()
        {
            CommandId = command.CommandId,
            OperationCommandId = command.OperationCommandId == Guid.Empty ? command.CommandId : command.OperationCommandId,
            Subject = new(ActorType.Event, ScheduledTaskRunStartedEvent.Actor, ScheduledTaskRunStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ScheduledTaskRun = scheduledTaskRunChange.ScheduledTaskRun
        };
    }
}
