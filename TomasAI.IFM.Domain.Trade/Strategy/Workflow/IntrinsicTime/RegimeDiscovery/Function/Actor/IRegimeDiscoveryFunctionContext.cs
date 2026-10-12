using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
/// <summary>Defines dependencies owned by the completed-only Regime Discovery Function actor.</summary>
public interface IRegimeDiscoveryFunctionContext : IFunctionActorContext<RegimeDiscoveryFunctionActor>
{
    IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand> StateRepository { get; }

    IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent> FunctionProjector { get; }

    IRegimeDiscoveryMarketSignalSnapshotProvider SnapshotProvider { get; }

    IRegimeDiscoveryCalculationModel CalculationModel { get; }

    RegimeDiscoveryExecutionMode ExecutionMode { get; }

    TimeProvider TimeProvider { get; }

    ILogger<RegimeDiscoveryFunctionActor> Logger { get; }
}
