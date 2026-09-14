using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Function.Actor;

public interface IExitOrderCompositionFunctionContext<TActor> : IFunctionActorContext<TActor>
    where TActor : IActor
{
    TimeProvider TimeProvider { get; }
    ILogger<TActor> Logger { get; }
    IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState,
        ComposeExitOrderCommand> StateRepository { get; }
}

public abstract class ExitOrderCompositionFunctionContext<TActor> : FunctionActorContext,
    IFunctionActorContext<TActor>, IExitOrderCompositionFunctionContext<TActor>
    where TActor : IActor
{
    readonly Lazy<IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState,
        ComposeExitOrderCommand>> repository;
    protected ExitOrderCompositionFunctionContext(IActorSupervisor supervisor, string actorName,
        ILogger<TActor> logger) : base(supervisor, new(ActorType.Function, actorName))
    {
        Logger = logger;
        repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<
            ExitOrderCompositionFunctionState, ComposeExitOrderCommand>>());
    }
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<TActor> Logger { get; }
    public IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState,
        ComposeExitOrderCommand> StateRepository => repository.Value;
}

public interface IPositionExitRiskFunctionContext<TActor> : IFunctionActorContext<TActor>
    where TActor : IActor
{
    IPortfolioOrderCompositionApi Portfolio { get; }
    TimeProvider TimeProvider { get; }
    ILogger<TActor> Logger { get; }
    IEventSourceFunctionStateRepository<PositionExitRiskFunctionState,
        EvaluatePositionExitRiskCommand> StateRepository { get; }
}

public abstract class PositionExitRiskFunctionContext<TActor> : FunctionActorContext,
    IFunctionActorContext<TActor>, IPositionExitRiskFunctionContext<TActor>
    where TActor : IActor
{
    readonly Lazy<IEventSourceFunctionStateRepository<PositionExitRiskFunctionState,
        EvaluatePositionExitRiskCommand>> repository;
    protected PositionExitRiskFunctionContext(IActorSupervisor supervisor, string actorName,
        IPortfolioOrderCompositionApi portfolio, ILogger<TActor> logger)
        : base(supervisor, new(ActorType.Function, actorName))
    {
        Portfolio = portfolio;
        Logger = logger;
        repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<
            PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand>>());
    }
    public IPortfolioOrderCompositionApi Portfolio { get; }
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<TActor> Logger { get; }
    public IEventSourceFunctionStateRepository<PositionExitRiskFunctionState,
        EvaluatePositionExitRiskCommand> StateRepository => repository.Value;
}
