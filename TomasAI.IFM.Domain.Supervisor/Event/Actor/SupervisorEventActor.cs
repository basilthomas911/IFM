using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Health;
using TomasAI.IFM.Domain.Supervisor.Shared.Events;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Event.Actor;

/// <summary>Consumes Supervisor operational events and updates the bounded audit/history state.</summary>
public sealed class SupervisorEventActor(
    IActorSupervisor supervisor,
    SupervisorOperationStore operations,
    ILogger<SupervisorEventActor> logger) : IActor<SupervisorEventActor>
{
    int _running;
    public const string ActorName = SupervisorActorOperationRecordedEvent.Actor;
    public ActorMailboxId Id { get; } = new(ActorType.Event, ActorName);
    public IActorMailbox Mailbox { get; } = new ActorMailbox(supervisor, new(ActorType.Event, ActorName));
    public bool IsRunning => Volatile.Read(ref _running) != 0;
    public ValueTask StartAsync(IActorSupervisor actorSupervisor) { Volatile.Write(ref _running, 1); return ValueTask.CompletedTask; }
    public ValueTask StopAsync() { Volatile.Write(ref _running, 0); return ValueTask.CompletedTask; }
    public ValueTask HandleMessageAsync(IActorMessage message) => HandleMessageAsync(message, message.Subject.ThreadId);
    public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
    {
        try
        {
            var value = message.AsEvent<SupervisorActorOperationRecordedEvent>()
                ?? throw new InvalidOperationException("The Supervisor operation event payload is invalid.");
            var request = new SupervisorActorOperationRequest(value.CommandId, value.Target,
                value.ExpectedGeneration, value.Operation, value.Requester, value.Reason,
                TimeSpan.FromTicks(value.TimeoutTicks));
            var result = new SupervisorActorOperationResult(value.Outcome, value.CommandId, value.Target,
                value.ExpectedGeneration, value.Stage, string.IsNullOrEmpty(value.FailureReason) ? null : value.FailureReason);
            operations.Record(request, result);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
        {
            logger.LogError(exception, "Supervisor event failed for {ActorThreadId}.", threadId);
        }
        return ValueTask.CompletedTask;
    }
}
