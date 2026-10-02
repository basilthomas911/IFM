using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.Actor;

/// <summary>Provides the standard canary command context without lifecycle privileges.</summary>
public sealed class RecoveryCanaryCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new(ActorType.Command, RecoveryCanaryCommandActor.ActorName)),
        IRecoveryCanaryCommandContext
{
    readonly Lazy<IEventSourceActorStateRepository<RecoveryCanaryCommandState>> _stateRepository =
        new(() => supervisor.Container.Resolve<IEventSourceActorStateRepository<RecoveryCanaryCommandState>>());
    readonly Lazy<IEventProjector<RecoveryCanaryCommandActor>> _projector =
        new(() => supervisor.Container.Resolve<IEventProjector<RecoveryCanaryCommandActor>>());

    /// <inheritdoc />
    public IEventSourceActorStateRepository<RecoveryCanaryCommandState> StateRepository =>
        _stateRepository.Value;

    /// <inheritdoc />
    public IEventProjector<RecoveryCanaryCommandActor> EventProjector => _projector.Value;
}

/// <summary>Exposes only the canary command's state and projection capabilities.</summary>
public interface IRecoveryCanaryCommandContext : ICommandActorContext<RecoveryCanaryCommandActor>
{
    /// <summary>The canary event-source repository.</summary>
    IEventSourceActorStateRepository<RecoveryCanaryCommandState> StateRepository { get; }

    /// <summary>The canary event projector.</summary>
    IEventProjector<RecoveryCanaryCommandActor> EventProjector { get; }
}
