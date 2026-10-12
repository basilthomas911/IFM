using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
public interface IMarketConditionFunctionContext : IFunctionActorContext<MarketConditionFunctionActor>
{
    IMarketConditionAssessmentCalculator CalculationModel { get; }

    TimeProvider TimeProvider { get; }

    ILogger<MarketConditionFunctionActor> Logger { get; }

    IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand> StateRepository { get; }

    IFunctionProjector<MarketConditionAssessmentCompletedEvent> FunctionProjector { get; }

    IMarketConditionAssessmentSnapshotProvider SnapshotProvider { get; }
}
