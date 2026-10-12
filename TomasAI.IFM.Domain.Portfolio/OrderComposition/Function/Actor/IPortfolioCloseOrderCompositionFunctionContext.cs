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
        EvaluatePortfolioCloseOrderCompositionCommand> StateRepository
    { get; }
    TimeProvider TimeProvider { get; }
    ILogger<PortfolioCloseOrderCompositionFunctionActor> Logger { get; }
}
