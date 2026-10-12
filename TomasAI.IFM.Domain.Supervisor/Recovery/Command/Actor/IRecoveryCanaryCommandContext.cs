using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.Actor;

/// <summary>Exposes only the canary command's state and projection capabilities.</summary>
public interface IRecoveryCanaryCommandContext : ICommandActorContext<RecoveryCanaryCommandActor>
{
    /// <summary>The canary event-source repository.</summary>
    IEventSourceActorStateRepository<RecoveryCanaryCommandState> StateRepository { get; }

    /// <summary>The canary event projector.</summary>
    IEventProjector<RecoveryCanaryCommandActor> EventProjector { get; }
}
