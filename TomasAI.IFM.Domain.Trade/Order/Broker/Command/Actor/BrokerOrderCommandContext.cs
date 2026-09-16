using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;

public interface IBrokerOrderCommandContext : ICommandActorContext<BrokerOrderCommandActor>
{
    IDbContextFactory DbFactory { get; }
    IEventSourceActorDbContext DbEventSource { get; }
    IDurableReplayQueue DurableReplayQueue { get; }
    IBlackboardService BlackboardService { get; }
    IActorService ActorService { get; }
    ITradeBroker TradeBroker { get; }
    IBrokerOrderReadStore ReadStore { get; }
    IBrokerAccountReadStore BrokerAccountStore { get; }
    BrokerOrderObservationBridge ObservationBridge { get; }
    IEventProjector<BrokerOrderCommandActor> EventProjector { get; }
    IEventSourceActorStateRepository<BrokerOrderCommandState> StateRepository { get; }
    ILogger<BrokerOrderCommandActor> Logger { get; }
}

public sealed class BrokerOrderCommandContext : CommandActorContext,
    ICommandActorContext<BrokerOrderCommandActor>, IBrokerOrderCommandContext
{
    private readonly Lazy<IEventSourceActorDbContext> _eventSource;
    private readonly Lazy<IDurableReplayQueue> _replay;
    private readonly Lazy<IActorService> _actors;
    private readonly Lazy<ITradeBroker> _broker;
    private readonly Lazy<IBrokerOrderReadStore> _readStore;
    private readonly Lazy<IBrokerAccountReadStore> _brokerAccountStore;
    private readonly Lazy<BrokerOrderObservationBridge> _observationBridge;
    private readonly Lazy<IEventProjector<BrokerOrderCommandActor>> _projector;
    private readonly Lazy<IEventSourceActorStateRepository<BrokerOrderCommandState>> _repository;

    public BrokerOrderCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        IBlackboardService blackboardService, ILogger<BrokerOrderCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, BrokerOrderCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboardService;
        Logger = logger;
        _eventSource = ResolveOnce<IEventSourceActorDbContext>();
        _replay = ResolveOnce<IDurableReplayQueue>();
        _actors = ResolveOnce<IActorService>();
        _broker = ResolveOnce<ITradeBroker>();
        _readStore = ResolveOnce<IBrokerOrderReadStore>();
        _brokerAccountStore = ResolveOnce<IBrokerAccountReadStore>();
        _observationBridge = ResolveOnce<BrokerOrderObservationBridge>();
        _projector = ResolveOnce<IEventProjector<BrokerOrderCommandActor>>();
        _repository = ResolveOnce<IEventSourceActorStateRepository<BrokerOrderCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<BrokerOrderCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => _eventSource.Value;
    public IDurableReplayQueue DurableReplayQueue => _replay.Value;
    public IActorService ActorService => _actors.Value;
    public ITradeBroker TradeBroker => _broker.Value;
    public IBrokerOrderReadStore ReadStore => _readStore.Value;
    public IBrokerAccountReadStore BrokerAccountStore => _brokerAccountStore.Value;
    public BrokerOrderObservationBridge ObservationBridge => _observationBridge.Value;
    public IEventProjector<BrokerOrderCommandActor> EventProjector => _projector.Value;
    public IEventSourceActorStateRepository<BrokerOrderCommandState> StateRepository => _repository.Value;
    private Lazy<T> ResolveOnce<T>() where T : class => new(() => Container.Resolve<T>());
}
