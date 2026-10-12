using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
public interface ITradeSelectionFunctionContext : IFunctionActorContext<TradeSelectionFunctionActor>
{
    ITradeSelectionCalculator CalculationModel { get; }

    IStrategyCatalogCapabilities Capabilities { get; }

    TimeProvider TimeProvider { get; }

    ILogger<TradeSelectionFunctionActor> Logger { get; }

    IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand> StateRepository { get; }

    IFunctionProjector<TradeSelectionFunctionCompletedEvent> FunctionProjector { get; }
}
