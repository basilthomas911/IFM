using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
public abstract class ExitOrderCompositionFunctionContext<TActor> : FunctionActorContext, IExitOrderCompositionFunctionContext<TActor> where TActor : IActor
{
    readonly Lazy<IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState, ComposeExitOrderCommand>> repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    protected ExitOrderCompositionFunctionContext(IActorSupervisor supervisor, string actorName, ILogger<TActor> logger) : base(supervisor, new(ActorType.Function, actorName))
    {
        Logger = logger;
        repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState, ComposeExitOrderCommand>>());
    }

    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<TActor> Logger { get; }
    public IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState, ComposeExitOrderCommand> StateRepository => repository.Value;
}
