using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function.Actor;

public sealed class FuturesExitOrderCompositionFunctionContext(
    IActorSupervisor supervisor, ILogger<FuturesExitOrderCompositionFunctionActor> logger)
    : ExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor>(
        supervisor, FuturesExitOrderCompositionFunctionActor.ActorName, logger);
