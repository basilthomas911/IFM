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
    TimeProvider TimeProvider { get; }
    ILogger<MarketConditionFunctionActor> Logger { get; }
    MarketConditionAssessmentHandler AssessmentHandler { get; }
}

public sealed class MarketConditionFunctionContext : FunctionActorContext,
    IFunctionActorContext<MarketConditionFunctionActor>, IMarketConditionFunctionContext
{
    readonly Lazy<MarketConditionAssessmentHandler> _assessment;
    public MarketConditionFunctionContext(IActorSupervisor supervisor,
        ILogger<MarketConditionFunctionActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Function, MarketConditionFunctionActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger); TimeProvider = TimeProvider.System;
        _assessment = new(() => new MarketConditionAssessmentHandler(
            Container.Resolve<IMarketConditionAssessmentSnapshotProvider>(),
            Container.Resolve<IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>>(),
            Container.Resolve<IFunctionProjector<MarketConditionAssessmentCompletedEvent>>(),
            Container.Resolve<ILogger<MarketConditionAssessmentHandler>>(), TimeProvider));
    }
    public ILogger<MarketConditionFunctionActor> Logger { get; }
    public TimeProvider TimeProvider { get; }
    public MarketConditionAssessmentHandler AssessmentHandler => _assessment.Value;
}
