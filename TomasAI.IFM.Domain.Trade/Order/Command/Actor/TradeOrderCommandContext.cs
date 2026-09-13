using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Actor;

public interface ITradeOrderCommandContext : ICommandActorContext<TradeOrderCommandActor>
{
    IDbContextFactory DbFactory { get; }
    IEventSourceActorDbContext DbEventSource { get; }
    IDurableReplayQueue DurableReplayQueue { get; }
    IBlackboardService BlackboardService { get; }
    IActorService ActorService { get; }
    IEventProjector<TradeOrderCommandActor> EventProjector { get; }
    IEventSourceActorStateRepository<TradeOrderCommandState> StateRepository { get; }
    ILogger<TradeOrderCommandActor> Logger { get; }
}

public sealed class TradeOrderCommandContext : CommandActorContext,
    ICommandActorContext<TradeOrderCommandActor>, ITradeOrderCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> dbEventSource;
    readonly Lazy<IDurableReplayQueue> durableReplayQueue;
    readonly Lazy<IActorService> actorService;
    readonly Lazy<IEventProjector<TradeOrderCommandActor>> eventProjector;
    readonly Lazy<IEventSourceActorStateRepository<TradeOrderCommandState>> stateRepository;

    public TradeOrderCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        IBlackboardService blackboardService, ILogger<TradeOrderCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, TradeOrderCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboardService;
        Logger = logger;
        dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        durableReplayQueue = ResolveOnce<IDurableReplayQueue>();
        actorService = ResolveOnce<IActorService>();
        eventProjector = ResolveOnce<IEventProjector<TradeOrderCommandActor>>();
        stateRepository = ResolveOnce<IEventSourceActorStateRepository<TradeOrderCommandState>>();
    }
    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<TradeOrderCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => dbEventSource.Value;
    public IDurableReplayQueue DurableReplayQueue => durableReplayQueue.Value;
    public IActorService ActorService => actorService.Value;
    public IEventProjector<TradeOrderCommandActor> EventProjector => eventProjector.Value;
    public IEventSourceActorStateRepository<TradeOrderCommandState> StateRepository => stateRepository.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => Container.Resolve<T>());
}
