using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
public sealed class IronCondorTradePlanFunctionContext : FunctionActorContext, IIronCondorTradePlanFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand>> _repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public IronCondorTradePlanFunctionContext(IActorSupervisor supervisor, ILogger<IronCondorTradePlanFunctionActor> logger) : base(supervisor, new(ActorType.Function, IronCondorTradePlanFunctionActor.ActorName))
    {
        Logger = logger;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand>>());
    }

    public IronCondorTradePlanAlgorithm Algorithm { get; } = new();
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<IronCondorTradePlanFunctionActor> Logger { get; }
    public IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand> StateRepository => _repository.Value;
}
