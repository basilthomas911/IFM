using System.Collections.ObjectModel;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;

/// <summary>Provides the only shared address catalog for Intrinsic Time Strategy pipeline actors.</summary>
public static class IntrinsicTimeStrategyPipelineRoutes
{
    static readonly ReadOnlyCollection<IntrinsicTimeStrategyPipelineRoute> RegisteredRoutes =
        Array.AsReadOnly<IntrinsicTimeStrategyPipelineRoute>(
    [
        new(
            StrategyWorkflowStage.RegimeDiscovery,
            new ActorMailboxId(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Unknown, ExecuteRegimeDiscoveryPipelineCommand.Actor),
            BoundedContextName.RegimeDiscoveryPipelineBoundedContext),
        new(
            StrategyWorkflowStage.MarketCondition,
            new ActorMailboxId(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor),
            new ActorMailboxId(ActorType.Unknown, ExecuteMarketConditionAssessmentCommand.Actor),
            BoundedContextName.MarketConditionPipelineBoundedContext),
        new(
            StrategyWorkflowStage.TradeSelection,
            new ActorMailboxId(ActorType.Function, ExecuteTradeSelectionPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Unknown, ExecuteTradeSelectionPipelineCommand.Actor),
            BoundedContextName.TradeSelectionPipelineBoundedContext),
        new(
            StrategyWorkflowStage.OrderComposition,
            new ActorMailboxId(ActorType.Function, ExecuteOrderCompositionPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Unknown, ExecuteOrderCompositionPipelineCommand.Actor),
            BoundedContextName.OrderCompositionPipelineBoundedContext),
        new(
            StrategyWorkflowStage.RiskManagement,
            new ActorMailboxId(ActorType.Function, ExecuteRiskManagementPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Unknown, ExecuteRiskManagementPipelineCommand.Actor),
            BoundedContextName.RiskManagementPipelineBoundedContext)
    ]);

    /// <summary>Gets all pipeline routes in workflow execution order.</summary>
    public static IReadOnlyList<IntrinsicTimeStrategyPipelineRoute> All => RegisteredRoutes;

    /// <summary>Gets the committed actor route for a workflow pipeline stage.</summary>
    /// <param name="stage">Workflow pipeline stage.</param>
    /// <returns>The immutable pipeline actor route.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the stage has no pipeline route.</exception>
    public static IntrinsicTimeStrategyPipelineRoute Get(StrategyWorkflowStage stage)
        => RegisteredRoutes.FirstOrDefault(route => route.Stage == stage) is { Stage: not StrategyWorkflowStage.None } route
            ? route
            : throw new ArgumentOutOfRangeException(nameof(stage), stage, "The workflow stage has no pipeline route.");
}
