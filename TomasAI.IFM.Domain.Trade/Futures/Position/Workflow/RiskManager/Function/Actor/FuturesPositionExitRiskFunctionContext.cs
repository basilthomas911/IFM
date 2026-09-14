using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function.Actor;

public sealed class FuturesPositionExitRiskFunctionContext(
    IActorSupervisor supervisor, IPortfolioOrderCompositionApi portfolio,
    ILogger<FuturesPositionExitRiskFunctionActor> logger)
    : PositionExitRiskFunctionContext<FuturesPositionExitRiskFunctionActor>(
        supervisor, FuturesPositionExitRiskFunctionActor.ActorName, portfolio, logger);
