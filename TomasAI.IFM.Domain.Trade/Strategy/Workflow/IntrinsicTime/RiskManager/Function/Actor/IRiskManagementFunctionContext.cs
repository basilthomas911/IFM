using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
public interface IRiskManagementFunctionContext : IFunctionActorContext<RiskManagementFunctionActor>
{
    IRiskEvaluator CalculationModel { get; }

    TimeProvider TimeProvider { get; }

    ILogger<RiskManagementFunctionActor> Logger { get; }

    IEventSourceFunctionStateRepository<RiskManagementFunctionState, ExecuteRiskManagementPipelineCommand> StateRepository { get; }

    IFunctionProjector<RiskManagementFunctionCompletedEvent>? FunctionProjector { get; }
}
