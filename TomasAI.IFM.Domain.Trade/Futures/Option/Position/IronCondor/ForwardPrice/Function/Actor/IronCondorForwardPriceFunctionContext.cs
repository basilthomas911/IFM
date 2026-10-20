using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.Actor;
public sealed class IronCondorForwardPriceFunctionContext : FunctionActorContext, IIronCondorForwardPriceFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<IronCondorForwardPriceFunctionState, CalculateIronCondorForwardPriceCommand>> _repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public IronCondorForwardPriceFunctionContext(IActorSupervisor supervisor, ILogger<IronCondorForwardPriceFunctionActor> logger) : base(supervisor, new(ActorType.Function, IronCondorForwardPriceFunctionActor.ActorName))
    {
        Logger = logger;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<IronCondorForwardPriceFunctionState, CalculateIronCondorForwardPriceCommand>>());
    }

    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<IronCondorForwardPriceFunctionActor> Logger { get; }
    public IEventSourceFunctionStateRepository<IronCondorForwardPriceFunctionState, CalculateIronCondorForwardPriceCommand> StateRepository => _repository.Value;
}
