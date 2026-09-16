using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Actor;

/// <summary>Services used by BrokerAccount command handlers.</summary>
public interface IBrokerAccountCommandContext : ICommandActorContext<BrokerAccountCommandActor>
{
    ITradeBroker TradeBroker { get; }
    BrokerAccountObservationBridge ObservationBridge { get; }
    IEventSourceActorStateRepository<BrokerAccountCommandState> StateRepository { get; }
    ILogger<BrokerAccountCommandActor> Logger { get; }
}

/// <summary>Typed singleton context for BrokerAccount commands.</summary>
public sealed class BrokerAccountCommandContext(
    IActorSupervisor supervisor,
    ITradeBroker tradeBroker,
    BrokerAccountObservationBridge observationBridge,
    IEventSourceActorStateRepository<BrokerAccountCommandState> stateRepository,
    ILogger<BrokerAccountCommandActor> logger)
    : CommandActorContext(supervisor, new(ActorType.Command, BrokerAccountCommandActor.ActorName)),
      ICommandActorContext<BrokerAccountCommandActor>, IBrokerAccountCommandContext
{
    public ITradeBroker TradeBroker { get; } = tradeBroker;
    public BrokerAccountObservationBridge ObservationBridge { get; } = observationBridge;
    public IEventSourceActorStateRepository<BrokerAccountCommandState> StateRepository { get; } = stateRepository;
    public ILogger<BrokerAccountCommandActor> Logger { get; } = logger;
}
