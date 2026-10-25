using System.Collections.ObjectModel;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;

/// <summary>Describes the committed execution and result addresses for one strategy pipeline stage.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct IntrinsicTimeStrategyPipelineRoute
{
    /// <summary>Gets the workflow stage implemented by the pipeline.</summary>
    public StrategyWorkflowStage Stage { get; init; }

    /// <summary>Gets the pipeline execution actor address; Regime Discovery uses Function.</summary>
    public ActorMailboxId CommandActor { get; init; }

    /// <summary>Gets the pipeline Realtime result source, or Unknown for a direct Function result.</summary>
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
