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
    IEventSourceFunctionStateRepository<MarketConditionFunctionState, ExecuteMarketConditionPipelineCommand> StateRepository { get; }
    IFunctionProjector<MarketConditionPipelineCompletedEvent> FunctionProjector { get; }
    IMarketConditionSnapshotProvider SnapshotProvider { get; }
    MarketConditionCalculationModel CalculationModel { get; }
    TimeProvider TimeProvider { get; }
    ILogger<MarketConditionFunctionActor> Logger { get; }
}

public sealed class MarketConditionFunctionContext : FunctionActorContext,
    IFunctionActorContext<MarketConditionFunctionActor>, IMarketConditionFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<MarketConditionFunctionState,
        ExecuteMarketConditionPipelineCommand>> _state;
    readonly Lazy<IFunctionProjector<MarketConditionPipelineCompletedEvent>> _projector;
    readonly Lazy<IMarketConditionSnapshotProvider> _snapshot;
    public MarketConditionFunctionContext(IActorSupervisor supervisor,
        ILogger<MarketConditionFunctionActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Function, MarketConditionFunctionActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger); TimeProvider = TimeProvider.System;
        CalculationModel = new MarketConditionCalculationModel();
        _state = ResolveOnce<IEventSourceFunctionStateRepository<MarketConditionFunctionState,
            ExecuteMarketConditionPipelineCommand>>();
        _projector = ResolveOnce<IFunctionProjector<MarketConditionPipelineCompletedEvent>>();
        _snapshot = ResolveOnce<IMarketConditionSnapshotProvider>();
    }
    public ILogger<MarketConditionFunctionActor> Logger { get; }
    public TimeProvider TimeProvider { get; }
    public MarketConditionCalculationModel CalculationModel { get; }
    public IEventSourceFunctionStateRepository<MarketConditionFunctionState,
        ExecuteMarketConditionPipelineCommand> StateRepository => _state.Value;
    public IFunctionProjector<MarketConditionPipelineCompletedEvent> FunctionProjector => _projector.Value;
    public IMarketConditionSnapshotProvider SnapshotProvider => _snapshot.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}
