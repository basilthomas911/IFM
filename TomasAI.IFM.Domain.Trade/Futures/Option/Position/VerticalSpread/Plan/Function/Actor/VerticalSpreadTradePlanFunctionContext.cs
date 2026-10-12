using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;
public sealed class VerticalSpreadTradePlanFunctionContext : FunctionActorContext, IVerticalSpreadTradePlanFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand>> _repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public VerticalSpreadTradePlanFunctionContext(IActorSupervisor supervisor, ILogger<VerticalSpreadTradePlanFunctionActor> logger) : base(supervisor, new(ActorType.Function, VerticalSpreadTradePlanFunctionActor.ActorName))
    {
        Logger = logger;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand>>());
    }

    public VerticalSpreadTradePlanAlgorithm Algorithm { get; } = new();
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<VerticalSpreadTradePlanFunctionActor> Logger { get; }
    public IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand> StateRepository => _repository.Value;
}
