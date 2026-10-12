using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
public sealed class FuturesIronCondorTradePositionCommandContext : CommandActorContext, IIronCondorPositionCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> d;
    readonly Lazy<IDurableReplayQueue> r;
    readonly Lazy<IActorService> a;
    readonly Lazy<IEventProjector<FuturesIronCondorTradePositionCommandActor>> p;
    readonly Lazy<IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>> s;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesIronCondorTradePositionCommandContext(IActorSupervisor v, IDbContextFactory f, IBlackboardService b, ILogger<FuturesIronCondorTradePositionCommandActor> l) : base(v, new(ActorType.Command, FuturesIronCondorTradePositionCommandActor.ActorName))
    {
        DbFactory = f;
        BlackboardService = b;
        Logger = l;
        d = R<IEventSourceActorDbContext>();
        r = R<IDurableReplayQueue>();
        a = R<IActorService>();
        p = R<IEventProjector<FuturesIronCondorTradePositionCommandActor>>();
        s = R<IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<FuturesIronCondorTradePositionCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => d.Value;
    public IDurableReplayQueue DurableReplayQueue => r.Value;
    public IActorService ActorService => a.Value;
    public IEventProjector<FuturesIronCondorTradePositionCommandActor> EventProjector => p.Value;
    public IResidentEventSourceActorStateRepository<IronCondorPositionCommandState> StateRepository => s.Value;

    Lazy<T> R<T>()
        where T : class => new(() => Container.Resolve<T>());
}
