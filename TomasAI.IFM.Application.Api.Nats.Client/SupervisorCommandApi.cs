using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Uses NATS request/reply to execute Supervisor actor lifecycle commands.</summary>
public sealed class SupervisorCommandApi(IActorProducer actorProducer) : NatsClientApi(actorProducer), ISupervisorCommandApi
{
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> ExecuteActorOperationAsync(
        ActorThreadId target,
        long expectedGeneration,
        SupervisorActorOperationKind operation,
        string requester,
        string reason,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var entityId = ActorEntityId.Default;
        var commandId = Guid.NewGuid();
        return operation switch
        {
            SupervisorActorOperationKind.Pause => Send(new PauseSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, PauseSupervisorActorCommand.Actor, PauseSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.Drain => Send(new DrainSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, DrainSupervisorActorCommand.Actor, DrainSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.Resume => Send(new ResumeSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, ResumeSupervisorActorCommand.Actor, ResumeSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.Stop => Send(new StopSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, StopSupervisorActorCommand.Actor, StopSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.Restart => Send(new RestartSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, RestartSupervisorActorCommand.Actor, RestartSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.Quarantine => Send(new QuarantineSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, QuarantineSupervisorActorCommand.Actor, QuarantineSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.Retire => Send(new RetireSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, RetireSupervisorActorCommand.Actor, RetireSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.Recycle => Send(new RecycleSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, RecycleSupervisorActorCommand.Actor, RecycleSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            SupervisorActorOperationKind.AcknowledgeIncident => Send(new AcknowledgeIncidentSupervisorActorCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Command, AcknowledgeIncidentSupervisorActorCommand.Actor, AcknowledgeIncidentSupervisorActorCommand.Verb, entityId.Format()),
                EntityId = entityId, Target = target, ExpectedGeneration = expectedGeneration,
                Requester = requester, Reason = reason, TimeoutTicks = timeout.Ticks
            }),
            _ => ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceFailed<GuidResult>(9701, "Unsupported Supervisor operation."))
        };

        ValueTask<ServiceResult<GuidResult>> Send<TCommand>(TCommand command)
            where TCommand : class, ICommand<ActorEntityId>
            => RequestCommandResultAsync<TCommand, ActorEntityId, GuidResult>(
                command, entityId, cancellationToken);
    }
}
