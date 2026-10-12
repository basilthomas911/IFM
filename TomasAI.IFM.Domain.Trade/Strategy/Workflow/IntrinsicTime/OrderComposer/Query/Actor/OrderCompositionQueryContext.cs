using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
public sealed class OrderCompositionQueryContext : QueryActorContext, IOrderCompositionQueryContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public OrderCompositionQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<OrderCompositionQueryActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Query, OrderCompositionQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository => Container.Resolve<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
    public IPortfolioQueryApi PortfolioQueries => Container.Resolve<IPortfolioQueryApi>();
    public IDbContextFactory DbFactory { get; }
    public ILogger<OrderCompositionQueryActor> Logger { get; }
}
