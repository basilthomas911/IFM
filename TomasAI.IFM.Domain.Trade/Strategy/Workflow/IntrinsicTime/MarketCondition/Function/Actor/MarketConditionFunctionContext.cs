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

public sealed class MarketConditionFunctionContext : FunctionActorContext,
    IFunctionActorContext<MarketConditionFunctionActor>, IMarketConditionFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>> _repository;
    readonly Lazy<IFunctionProjector<MarketConditionAssessmentCompletedEvent>> _projector;
    readonly Lazy<IMarketConditionAssessmentSnapshotProvider> _snapshots;
    public MarketConditionFunctionContext(IActorSupervisor supervisor,
        ILogger<MarketConditionFunctionActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Function, MarketConditionFunctionActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger); TimeProvider = TimeProvider.System;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>>());
        _projector = new(() => Container.Resolve<IFunctionProjector<MarketConditionAssessmentCompletedEvent>>());
        _snapshots = new(() => Container.Resolve<IMarketConditionAssessmentSnapshotProvider>());
    }
    public IMarketConditionAssessmentCalculator CalculationModel { get; } = new MarketConditionAssessmentCalculator();
    public ILogger<MarketConditionFunctionActor> Logger { get; }
    public TimeProvider TimeProvider { get; }
    public IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand> StateRepository => _repository.Value;
    public IFunctionProjector<MarketConditionAssessmentCompletedEvent> FunctionProjector => _projector.Value;
    public IMarketConditionAssessmentSnapshotProvider SnapshotProvider => _snapshots.Value;
}
