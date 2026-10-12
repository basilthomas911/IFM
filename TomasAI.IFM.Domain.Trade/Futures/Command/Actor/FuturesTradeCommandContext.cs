using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
public sealed class FuturesTradeCommandContext : CommandActorContext, IFuturesTradeCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> d;
    readonly Lazy<IDurableReplayQueue> r;
    readonly Lazy<IActorService> a;
    readonly Lazy<IEventProjector<FuturesTradeCommandActor>> p;
    readonly Lazy<IEventSourceActorStateRepository<FuturesTradeCommandState>> s;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesTradeCommandContext(IActorSupervisor v, IDbContextFactory f, IBlackboardService b, ILogger<FuturesTradeCommandActor> l) : base(v, new(ActorType.Command, FuturesTradeCommandActor.ActorName))
    {
        DbFactory = f;
        BlackboardService = b;
        Logger = l;
        d = R<IEventSourceActorDbContext>();
        r = R<IDurableReplayQueue>();
        a = R<IActorService>();
        p = R<IEventProjector<FuturesTradeCommandActor>>();
        s = R<IEventSourceActorStateRepository<FuturesTradeCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<FuturesTradeCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => d.Value;
    public IDurableReplayQueue DurableReplayQueue => r.Value;
    public IActorService ActorService => a.Value;
    public IEventProjector<FuturesTradeCommandActor> EventProjector => p.Value;
    public IEventSourceActorStateRepository<FuturesTradeCommandState> StateRepository => s.Value;

    Lazy<T> R<T>()
        where T : class => new(() => Container.Resolve<T>());
}
