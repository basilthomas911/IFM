using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;

public interface IPortfolioCloseOrderCompositionFunctionContext :
    IFunctionActorContext<PortfolioCloseOrderCompositionFunctionActor>
{
    IEventSourceFunctionStateRepository<PortfolioCloseOrderCompositionFunctionState,
        EvaluatePortfolioCloseOrderCompositionCommand> StateRepository { get; }
    TimeProvider TimeProvider { get; }
    ILogger<PortfolioCloseOrderCompositionFunctionActor> Logger { get; }
}

public sealed class PortfolioCloseOrderCompositionFunctionContext : FunctionActorContext,
    IFunctionActorContext<PortfolioCloseOrderCompositionFunctionActor>,
    IPortfolioCloseOrderCompositionFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<PortfolioCloseOrderCompositionFunctionState,
        EvaluatePortfolioCloseOrderCompositionCommand>> repository;

    public PortfolioCloseOrderCompositionFunctionContext(
        IActorSupervisor supervisor,
        ILogger<PortfolioCloseOrderCompositionFunctionActor> logger)
        : base(supervisor, new(ActorType.Function, PortfolioCloseOrderCompositionFunctionActor.ActorName))
    {
        Logger = logger;
        repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<
            PortfolioCloseOrderCompositionFunctionState, EvaluatePortfolioCloseOrderCompositionCommand>>() ??
            throw new InvalidOperationException("Portfolio close-order repository is not registered."));
    }

    public IEventSourceFunctionStateRepository<PortfolioCloseOrderCompositionFunctionState,
        EvaluatePortfolioCloseOrderCompositionCommand> StateRepository => repository.Value;
    public TimeProvider TimeProvider => global::System.TimeProvider.System;
    public ILogger<PortfolioCloseOrderCompositionFunctionActor> Logger { get; }
}
