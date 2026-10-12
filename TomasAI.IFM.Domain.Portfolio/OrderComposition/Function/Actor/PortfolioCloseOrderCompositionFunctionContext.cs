using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class PortfolioCloseOrderCompositionFunctionContext : FunctionActorContext,
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
