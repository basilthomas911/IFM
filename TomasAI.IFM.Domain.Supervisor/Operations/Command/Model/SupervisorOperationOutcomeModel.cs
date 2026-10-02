using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.Model;

/// <summary>Constructs the immutable private event for one already-decided Supervisor operation.</summary>
internal static class SupervisorOperationOutcomeModel
{
    internal static SupervisorActorOperationRecordedEvent Create(
        ISupervisorOperationCommand command, SupervisorActorOperationResult result,
        Guid eventId, DateTime receivedUtc) => new()
    {
        Subject = new(ActorType.Event, SupervisorActorOperationRecordedEvent.Actor,
            SupervisorActorOperationRecordedEvent.Verb, command.Subject.EntityId),
        EntityId = command.EntityId,
        Id = eventId,
        CommandId = command.CommandId,
        AggregateId = command.Target.ToString(),
        ReceivedOn = receivedUtc,
        Target = command.Target,
        ExpectedGeneration = command.ExpectedGeneration,
        Operation = command.OperationKind,
        Requester = command.Requester,
        Reason = command.Reason,
        TimeoutTicks = command.TimeoutTicks,
        Outcome = result.Outcome,
        Stage = result.Stage,
        FailureReason = result.FailureReason ?? string.Empty
    };
}
