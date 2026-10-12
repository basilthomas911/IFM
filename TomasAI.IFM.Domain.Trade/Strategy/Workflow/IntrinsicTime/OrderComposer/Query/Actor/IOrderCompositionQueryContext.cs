using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
public interface IOrderCompositionQueryContext : IQueryActorContext<OrderCompositionQueryActor>
{
    IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository { get; }

    IDbContextFactory DbFactory { get; }

    IPortfolioQueryApi PortfolioQueries { get; }

    ILogger<OrderCompositionQueryActor> Logger { get; }
}
