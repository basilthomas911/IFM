using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
public abstract class PositionExitRiskFunctionContext<TActor> : FunctionActorContext, IPositionExitRiskFunctionContext<TActor> where TActor : IActor
{
    readonly Lazy<IEventSourceFunctionStateRepository<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand>> repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    protected PositionExitRiskFunctionContext(IActorSupervisor supervisor, string actorName, IPortfolioOrderCompositionApi portfolio, ILogger<TActor> logger) : base(supervisor, new(ActorType.Function, actorName))
    {
        Portfolio = portfolio;
        Logger = logger;
        repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand>>());
    }

    public IPortfolioOrderCompositionApi Portfolio { get; }
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<TActor> Logger { get; }
    public IEventSourceFunctionStateRepository<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand> StateRepository => repository.Value;
}
