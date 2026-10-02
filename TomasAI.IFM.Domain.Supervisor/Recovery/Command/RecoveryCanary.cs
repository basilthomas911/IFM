using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command;

/// <summary>Validates and publishes one isolated, exact-generation recovery canary.</summary>
public static class RecoveryCanary
{
    /// <summary>Applies one side-effect-free canary source event for durable projection.</summary>
    public static ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this RecoveryCanaryCommand command,
        RecoveryCanaryCommandState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recorded = new RecoveryCanaryRecordedEvent
        {
            Subject = new(ActorType.Event, RecoveryCanaryRecordedEvent.Actor,
                RecoveryCanaryRecordedEvent.Verb, command.Subject.EntityId),
            Id = Guid.NewGuid(),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.CorrelationId.ToString("N"),
            ReceivedOn = DateTime.UtcNow,
            CorrelationId = command.CorrelationId,
            GenerationId = command.GenerationId,
            ValueDate = command.ValueDate,
            Dataset = command.Dataset
        };
        state.Update(recorded, command);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
    }
}
