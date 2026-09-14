using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function.Actor;

public sealed class VerticalSpreadPositionExitRiskFunctionContext(
    IActorSupervisor supervisor, IPortfolioOrderCompositionApi portfolio,
    ILogger<VerticalSpreadPositionExitRiskFunctionActor> logger)
    : PositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor>(
        supervisor, VerticalSpreadPositionExitRiskFunctionActor.ActorName, portfolio, logger);
