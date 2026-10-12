using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;

public interface IPortfolioOrderCompositionFunctionContext : IFunctionActorContext<PortfolioOrderCompositionFunctionActor>
{
    IEventSourceFunctionStateRepository<PortfolioOrderCompositionFunctionState, EvaluatePortfolioOrderCompositionCommand> StateRepository { get; }
    TimeProvider TimeProvider { get; }
    ILogger<PortfolioOrderCompositionFunctionActor> Logger { get; }
}
