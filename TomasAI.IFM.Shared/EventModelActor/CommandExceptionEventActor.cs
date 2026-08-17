using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using ActorCommandExceptionEvent = TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Terminates the durable generic command-failure path. Domain command actors
/// publish typed failure events when one exists; unexpected command failures use
/// this framework mailbox so the original failure is not replaced by a missing
/// producer error.
/// </summary>
public sealed class CommandExceptionEventActor(
    IActorSupervisor supervisor,
    ILogger<CommandExceptionEventActor> logger)
    : BaseEventActor<CommandExceptionEventActor>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Event, Actor))
{
    public const string Actor = "CommandException";
    const string ServiceId = nameof(CommandExceptionEventActor);

    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);

        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Event, Name: Actor }
            || subject.Verb is not (
                ActorCommandExceptionEvent.CommandFail
                or CommandValidationExceptionEvent.CommandValidationFail
                or StorageExceptionEvent.StorageFail))
            return default!;

        return message.AsEvent<ActorCommandExceptionEvent>()!;
    }

    protected override ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (@event is not ActorCommandExceptionEvent errorEvent)
            throw new InvalidOperationException(
                $"Unable to resolve {Actor} event from message: {@event?.Subject}");

        logger.LogErrorEvent(
            ServiceId,
            "Command {CommandName} failed with code {ErrorCode}: {ErrorMessage}",
            errorEvent.CommandName,
            errorEvent.ErrorCode,
            errorEvent.ErrorMessage);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent @event,
        Exception ex)
    {
        logger.LogErrorEvent(
            ServiceId,
            ex,
            "Failed to handle framework command exception on thread {ThreadId}.",
            threadId);
        return ValueTask.CompletedTask;
    }
}
