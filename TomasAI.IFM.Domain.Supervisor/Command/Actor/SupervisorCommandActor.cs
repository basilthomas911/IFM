using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Events;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Command.Actor;

/// <summary>Processes only authorized, audited Supervisor lifecycle commands.</summary>
public sealed class SupervisorCommandActor(
    IActorSupervisor actorSupervisor,
    ISupervisorManagedActorLifecycle lifecycle,
    ISupervisorOperatorAuthorizer authorizer,
    Health.SupervisorOperationStore operations,
    ISupervisorIncidentStore incidents,
    ILogger<SupervisorCommandActor> logger) : IActor<SupervisorCommandActor>
{
    int _running;
    public const string ActorName = ExecuteSupervisorActorOperationCommand.Actor;
    public ActorMailboxId Id { get; } = new(ActorType.Command, ActorName);
    public IActorMailbox Mailbox { get; } = new ActorMailbox(actorSupervisor, new(ActorType.Command, ActorName));
    public bool IsRunning => Volatile.Read(ref _running) != 0;

    public ValueTask StartAsync(IActorSupervisor supervisor)
    {
        Volatile.Write(ref _running, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync()
    {
        Volatile.Write(ref _running, 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleMessageAsync(IActorMessage message)
        => HandleMessageAsync(message, message.Subject.ThreadId, CancellationToken.None);

    public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        => HandleMessageAsync(message, threadId, CancellationToken.None);

    public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId, CancellationToken cancellationToken)
    {
        ServiceResult<SupervisorActorOperationResult> reply;
        try
        {
            var command = message.AsCommand<ExecuteSupervisorActorOperationCommand>()
                ?? throw new InvalidOperationException("The Supervisor command payload is invalid.");
            if (!authorizer.IsAuthorized(command.Requester, command.Operation))
            {
                var request = new SupervisorActorOperationRequest(command.CommandId, command.Target,
                    command.ExpectedGeneration, command.Operation, command.Requester, command.Reason, command.Timeout);
                var rejected = new SupervisorActorOperationResult(
                    Shared.Enums.SupervisorOperationOutcome.Rejected, command.CommandId, command.Target,
                    command.ExpectedGeneration, "Authorization", "The requester is not authorized.");
                operations.Record(request, rejected);
                reply = new ServiceFailed<SupervisorActorOperationResult>(command.ErrorCode,
                    rejected.FailureReason!, rejected);
            }
            else
            {
                var request = new SupervisorActorOperationRequest(command.CommandId, command.Target,
                    command.ExpectedGeneration, command.Operation, command.Requester, command.Reason, command.Timeout);
                var acknowledged = command.Operation == Shared.Enums.SupervisorActorOperationKind.AcknowledgeIncident
                    && incidents.Acknowledge(command.Target, command.Requester, command.Reason);
                var result = command.Operation == Shared.Enums.SupervisorActorOperationKind.AcknowledgeIncident
                    ? new SupervisorActorOperationResult(
                        acknowledged ? Shared.Enums.SupervisorOperationOutcome.Succeeded : Shared.Enums.SupervisorOperationOutcome.Rejected,
                        command.CommandId, command.Target, command.ExpectedGeneration, "IncidentAcknowledgement",
                        acknowledged ? null : "The incident could not be acknowledged.")
                    : await lifecycle.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                operations.Record(request, result);
                var auditEvent = new SupervisorActorOperationRecordedEvent
                {
                    Subject = new(ActorType.Event, SupervisorActorOperationRecordedEvent.Actor,
                        SupervisorActorOperationRecordedEvent.Verb, command.Subject.EntityId),
                    EntityId = command.EntityId,
                    Id = Guid.NewGuid(),
                    CommandId = command.CommandId,
                    AggregateId = command.Target.ToString(),
                    ReceivedOn = DateTime.UtcNow,
                    Target = command.Target,
                    ExpectedGeneration = command.ExpectedGeneration,
                    Operation = command.Operation,
                    Requester = command.Requester,
                    Reason = command.Reason,
                    TimeoutTicks = command.TimeoutTicks,
                    Outcome = result.Outcome,
                    Stage = result.Stage,
                    FailureReason = result.FailureReason ?? string.Empty
                };
                try
                {
                    await actorSupervisor.GetProducer(Id).SendAsync<SupervisorActorOperationRecordedEvent, ActorEntityId>(
                        auditEvent.Subject, auditEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception auditPublicationFailure) when (auditPublicationFailure is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
                {
                    logger.LogError(auditPublicationFailure,
                        "Supervisor operation {OperationId} was recorded locally but its audit event could not be published.",
                        command.CommandId);
                }
                reply = result.Succeeded
                    ? new ServiceOk<SupervisorActorOperationResult>(result)
                    : new ServiceFailed<SupervisorActorOperationResult>(command.ErrorCode,
                        result.FailureReason ?? result.Stage, result);
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
        {
            logger.LogError(exception, "Supervisor command failed for {ActorThreadId}.", threadId);
            reply = new ServiceFailed<SupervisorActorOperationResult>(9701, "Supervisor command processing failed.");
        }
        try { await message.ReplyAsync(reply).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
        {
            logger.LogError(exception, "Supervisor command reply failed for {ActorThreadId}.", threadId);
        }
    }
}
