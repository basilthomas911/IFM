using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function.Actor;

public sealed class VerticalSpreadExitOrderCompositionFunctionContext(
    IActorSupervisor supervisor, ILogger<VerticalSpreadExitOrderCompositionFunctionActor> logger)
    : ExitOrderCompositionFunctionContext<VerticalSpreadExitOrderCompositionFunctionActor>(
        supervisor, VerticalSpreadExitOrderCompositionFunctionActor.ActorName, logger);
