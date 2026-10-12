using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
public sealed class FuturesTradePositionCommandContext : CommandActorContext, IFuturesPositionCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> dbEventSource;
    readonly Lazy<IDurableReplayQueue> durableReplayQueue;
    readonly Lazy<IActorService> actorService;
    readonly Lazy<IEventProjector<FuturesTradePositionCommandActor>> eventProjector;
    readonly Lazy<IResidentEventSourceActorStateRepository<FuturesPositionCommandState>> stateRepository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesTradePositionCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, IBlackboardService blackboardService, ILogger<FuturesTradePositionCommandActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesTradePositionCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboardService;
        Logger = logger;
        dbEventSource = Resolve<IEventSourceActorDbContext>();
        durableReplayQueue = Resolve<IDurableReplayQueue>();
        actorService = Resolve<IActorService>();
        eventProjector = Resolve<IEventProjector<FuturesTradePositionCommandActor>>();
        stateRepository = Resolve<IResidentEventSourceActorStateRepository<FuturesPositionCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<FuturesTradePositionCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => dbEventSource.Value;
    public IDurableReplayQueue DurableReplayQueue => durableReplayQueue.Value;
    public IActorService ActorService => actorService.Value;
    public IEventProjector<FuturesTradePositionCommandActor> EventProjector => eventProjector.Value;
    public IResidentEventSourceActorStateRepository<FuturesPositionCommandState> StateRepository => stateRepository.Value;

    Lazy<T> Resolve<T>()
        where T : class => new(() => Container.Resolve<T>());
}
