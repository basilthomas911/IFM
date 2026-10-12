using TomasAI.IFM.Application.EventProjector.Contracts;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Actor;

/// <summary>Typed singleton context for BrokerAccount commands.</summary>
public sealed class BrokerAccountCommandContext(
    IActorSupervisor supervisor,
    ITradeBroker tradeBroker,
    IEventProjector<BrokerAccountCommandActor> eventProjector,
    BrokerAccountObservationBridge observationBridge,
    IEventSourceActorStateRepository<BrokerAccountCommandState> stateRepository,
    ILogger<BrokerAccountCommandActor> logger)
    : CommandActorContext(supervisor, new(ActorType.Command, BrokerAccountCommandActor.ActorName)),
IBrokerAccountCommandContext
{
    public IEventProjector<BrokerAccountCommandActor> EventProjector { get; } = eventProjector;
    public ITradeBroker TradeBroker { get; } = tradeBroker;
    public BrokerAccountObservationBridge ObservationBridge { get; } = observationBridge;
    public IEventSourceActorStateRepository<BrokerAccountCommandState> StateRepository { get; } = stateRepository;
    public ILogger<BrokerAccountCommandActor> Logger { get; } = logger;
}
