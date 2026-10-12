using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
public interface IPositionExitRiskFunctionContext<TActor> : IFunctionActorContext<TActor> where TActor : IActor
{
    IPortfolioOrderCompositionApi Portfolio { get; }

    TimeProvider TimeProvider { get; }

    ILogger<TActor> Logger { get; }

    IEventSourceFunctionStateRepository<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand> StateRepository { get; }
}
