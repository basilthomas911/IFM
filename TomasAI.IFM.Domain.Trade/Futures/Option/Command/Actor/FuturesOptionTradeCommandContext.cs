using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;

public interface IFuturesOptionTradeCommandContext :
    ICommandActorContext<FuturesOptionTradeCommandActor>
{
    IDbContextFactory DbFactory { get; }
    IEventSourceActorDbContext DbEventSource { get; }
    IDurableReplayQueue DurableReplayQueue { get; }
    IBlackboardService BlackboardService { get; }
    IActorService ActorService { get; }
    IEventProjector<FuturesOptionTradeCommandActor> EventProjector { get; }
    IEventSourceActorStateRepository<FuturesOptionTradeCommandState> StateRepository { get; }
    ILogger<FuturesOptionTradeCommandActor> Logger { get; }
}

public sealed class FuturesOptionTradeCommandContext :
    CommandActorContext,
    ICommandActorContext<FuturesOptionTradeCommandActor>,
    IFuturesOptionTradeCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IDurableReplayQueue> _durableReplayQueue;
    readonly Lazy<IActorService> _actorService;
    readonly Lazy<IEventProjector<FuturesOptionTradeCommandActor>> _eventProjector;
    readonly Lazy<IEventSourceActorStateRepository<FuturesOptionTradeCommandState>> _stateRepository;

    public FuturesOptionTradeCommandContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<FuturesOptionTradeCommandActor> logger)
        : base(
            supervisor,
            new ActorMailboxId(
                ActorType.Command,
                FuturesOptionTradeCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboardService;
        Logger = logger;
        _dbEventSource = Resolve<IEventSourceActorDbContext>();
        _durableReplayQueue = Resolve<IDurableReplayQueue>();
        _actorService = Resolve<IActorService>();
        _eventProjector = Resolve<IEventProjector<FuturesOptionTradeCommandActor>>();
        _stateRepository = Resolve<IEventSourceActorStateRepository<FuturesOptionTradeCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<FuturesOptionTradeCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    public IDurableReplayQueue DurableReplayQueue => _durableReplayQueue.Value;
    public IActorService ActorService => _actorService.Value;
    public IEventProjector<FuturesOptionTradeCommandActor> EventProjector => _eventProjector.Value;
    public IEventSourceActorStateRepository<FuturesOptionTradeCommandState> StateRepository =>
        _stateRepository.Value;

    Lazy<T> Resolve<T>() where T : class =>
        new(() => Container.Resolve<T>());
}
