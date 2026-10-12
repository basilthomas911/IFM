using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
public interface IOrderCompositionFunctionContext : IFunctionActorContext<OrderCompositionFunctionActor>
{
    /// <summary>Application market boundary; accepted execution continues to use its frozen snapshot.</summary>
    TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi MarketDataApi => throw new NotSupportedException();

    IOrderComposer CalculationModel { get; }

    TimeProvider TimeProvider { get; }

    ILogger<OrderCompositionFunctionActor> Logger { get; }

    IEventSourceFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand> StateRepository { get; }

    IFunctionProjector<OrderCompositionFunctionCompletedEvent> FunctionProjector { get; }
}
