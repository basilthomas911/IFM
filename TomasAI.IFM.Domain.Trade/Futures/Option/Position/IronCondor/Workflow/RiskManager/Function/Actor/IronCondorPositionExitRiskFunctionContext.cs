using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.RiskManager.Function.Actor;

public sealed class IronCondorPositionExitRiskFunctionContext(
    IActorSupervisor supervisor, IPortfolioOrderCompositionApi portfolio,
    ILogger<IronCondorPositionExitRiskFunctionActor> logger)
    : PositionExitRiskFunctionContext<IronCondorPositionExitRiskFunctionActor>(
        supervisor, IronCondorPositionExitRiskFunctionActor.ActorName, portfolio, logger);
