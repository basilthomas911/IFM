using System.Collections.ObjectModel;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;

/// <summary>Describes the committed Command and Realtime addresses for one strategy pipeline stage.</summary>
public readonly record struct IntrinsicTimeStrategyPipelineRoute
{
    /// <summary>Gets the workflow stage implemented by the pipeline.</summary>
    public StrategyWorkflowStage Stage { get; init; }

    /// <summary>Gets the pipeline Command actor address.</summary>
    public ActorMailboxId CommandActor { get; init; }

    /// <summary>Gets the pipeline Realtime event-source address.</summary>
    public ActorMailboxId RealtimeActor { get; init; }

    /// <summary>Gets the pipeline bounded-context route.</summary>
    public BoundedContextName BoundedContext { get; init; }

    /// <summary>Initializes an immutable pipeline route.</summary>
    /// <param name="stage">Workflow stage implemented by the pipeline.</param>
    /// <param name="commandActor">Pipeline Command actor address.</param>
    /// <param name="realtimeActor">Pipeline Realtime event-source address.</param>
    /// <param name="boundedContext">Pipeline bounded-context route.</param>
    public IntrinsicTimeStrategyPipelineRoute(
        StrategyWorkflowStage stage,
        ActorMailboxId commandActor,
        ActorMailboxId realtimeActor,
        BoundedContextName boundedContext)
    {
        Stage = stage;
        CommandActor = commandActor;
        RealtimeActor = realtimeActor;
        BoundedContext = boundedContext;
    }
}

/// <summary>Provides the only shared address catalog for Intrinsic Time Strategy pipeline actors.</summary>
public static class IntrinsicTimeStrategyPipelineRoutes
{
    static readonly ReadOnlyCollection<IntrinsicTimeStrategyPipelineRoute> RegisteredRoutes =
        Array.AsReadOnly<IntrinsicTimeStrategyPipelineRoute>(
    [
        new(
            StrategyWorkflowStage.RegimeDiscovery,
            new ActorMailboxId(ActorType.Command, ExecuteRegimeDiscoveryPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Realtime, RegimeDiscoveryPipelineCompletedEvent.Actor),
            BoundedContextName.RegimeDiscoveryPipelineBoundedContext),
        new(
            StrategyWorkflowStage.MarketCondition,
            new ActorMailboxId(ActorType.Command, StartMarketConditionPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Realtime, MarketConditionPipelineProcessingEvent.Actor),
            BoundedContextName.MarketConditionPipelineBoundedContext),
        new(
            StrategyWorkflowStage.TradeSelection,
            new ActorMailboxId(ActorType.Command, StartTradeSelectionPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Realtime, TradeSelectionPipelineProcessingEvent.Actor),
            BoundedContextName.TradeSelectionPipelineBoundedContext),
        new(
            StrategyWorkflowStage.OrderComposition,
            new ActorMailboxId(ActorType.Command, StartOrderCompositionPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Realtime, OrderCompositionPipelineProcessingEvent.Actor),
            BoundedContextName.OrderCompositionPipelineBoundedContext),
        new(
            StrategyWorkflowStage.RiskManagement,
            new ActorMailboxId(ActorType.Command, StartRiskManagementPipelineCommand.Actor),
            new ActorMailboxId(ActorType.Realtime, RiskManagementPipelineProcessingEvent.Actor),
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
