using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;

public interface IPortfolioOrderCompositionFunctionContext:IFunctionActorContext<PortfolioOrderCompositionFunctionActor>
{
    IEventSourceFunctionStateRepository<PortfolioOrderCompositionFunctionState,EvaluatePortfolioOrderCompositionCommand> StateRepository {get;}
    TimeProvider TimeProvider {get;}
    ILogger<PortfolioOrderCompositionFunctionActor> Logger {get;}
}

public sealed class PortfolioOrderCompositionFunctionContext:FunctionActorContext,IPortfolioOrderCompositionFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<PortfolioOrderCompositionFunctionState,EvaluatePortfolioOrderCompositionCommand>> repository;
    public PortfolioOrderCompositionFunctionContext(IActorSupervisor supervisor,ILogger<PortfolioOrderCompositionFunctionActor> logger)
        :base(supervisor,new ActorMailboxId(ActorType.Function,PortfolioOrderCompositionFunctionActor.ActorName))
    {
        Logger=logger;
        repository=new(()=>Container.Resolve<IEventSourceFunctionStateRepository<PortfolioOrderCompositionFunctionState,EvaluatePortfolioOrderCompositionCommand>>()
            ??throw new InvalidOperationException("Portfolio order-composition repository is not registered."));
    }
    public IEventSourceFunctionStateRepository<PortfolioOrderCompositionFunctionState,EvaluatePortfolioOrderCompositionCommand> StateRepository=>repository.Value;
    public TimeProvider TimeProvider=>global::System.TimeProvider.System;
    public ILogger<PortfolioOrderCompositionFunctionActor> Logger {get;}
}
