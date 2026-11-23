using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
/// <summary>Provides the closed-generic workflow Query actor context.</summary>
public sealed class IntrinsicTimeStrategyWorkflowQueryContext : QueryActorContext, IIntrinsicTimeStrategyWorkflowQueryContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    /// <summary>Initializes the workflow query context.</summary>
    public IntrinsicTimeStrategyWorkflowQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<IntrinsicTimeStrategyWorkflowQueryActor> logger, TimeProvider? timeProvider = null) : base(supervisor, new ActorMailboxId(ActorType.Query, IntrinsicTimeStrategyWorkflowQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        ProjectionCache = IntrinsicTimeStrategyWorkflowProjectionCache.Shared;
        TimeProvider = timeProvider ?? TimeProvider.System;
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }

    public IEnumerable<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeFundAssignment> QueueAssignments =>
        Container.Resolve<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor.IntrinsicTimeStrategyWorkflowOptions>()?.Assignments ?? [];
    /// <inheritdoc/>
    public IIntrinsicTimeStrategyWorkflowProjectionCache ProjectionCache { get; }
    /// <inheritdoc/>
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc/>
    public ILogger<IntrinsicTimeStrategyWorkflowQueryActor> Logger { get; }
}
