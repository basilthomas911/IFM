using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
/// <summary>Provides the closed-generic workflow Query actor context.</summary>
public sealed class IntrinsicTimeStrategyWorkflowQueryContext : QueryActorContext, IIntrinsicTimeStrategyWorkflowQueryContext
{
    readonly Lazy<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>> _stateRepository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    /// <summary>Initializes the workflow query context.</summary>
    public IntrinsicTimeStrategyWorkflowQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<IntrinsicTimeStrategyWorkflowQueryActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Query, IntrinsicTimeStrategyWorkflowQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        ProjectionCache = IntrinsicTimeStrategyWorkflowProjectionCache.Shared;
        TimeProvider = TimeProvider.System;
        Logger = IsArgumentNull.Set(logger);
        _stateRepository = new(() => IsArgumentNull.Set(Container.Resolve<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>())!);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IIntrinsicTimeStrategyWorkflowProjectionCache ProjectionCache { get; }
    /// <inheritdoc/>
    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository => _stateRepository.Value;
    /// <inheritdoc/>
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc/>
    public ILogger<IntrinsicTimeStrategyWorkflowQueryActor> Logger { get; }
}
