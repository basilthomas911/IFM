using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.OrderComposer.Function.Actor;

public sealed class IronCondorExitOrderCompositionFunctionContext(
    IActorSupervisor supervisor, ILogger<IronCondorExitOrderCompositionFunctionActor> logger)
    : ExitOrderCompositionFunctionContext<IronCondorExitOrderCompositionFunctionActor>(
        supervisor, IronCondorExitOrderCompositionFunctionActor.ActorName, logger);
