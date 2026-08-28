using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;

/// <summary>Defines readonly data services owned by the workflow Query actor.</summary>
public interface IIntrinsicTimeStrategyWorkflowQueryContext
    : IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor>
{
    /// <summary>Gets the application database-context factory.</summary>
    IDbContextFactory DbFactory { get; }

    /// <summary>Gets the monotonic active-workflow projection cache.</summary>
    IIntrinsicTimeStrategyWorkflowProjectionCache ProjectionCache { get; }

    /// <summary>Gets the authoritative workflow snapshot repository for read-only operational inspection.</summary>
    IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository { get; }

    /// <summary>Gets the clock used to derive deadline observations.</summary>
    TimeProvider TimeProvider { get; }

    /// <summary>Gets the query actor logger.</summary>
    ILogger<IntrinsicTimeStrategyWorkflowQueryActor> Logger { get; }
}

/// <summary>Provides the closed-generic workflow Query actor context.</summary>
public sealed class IntrinsicTimeStrategyWorkflowQueryContext
    : QueryActorContext,
      IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor>,
      IIntrinsicTimeStrategyWorkflowQueryContext
{
    readonly Lazy<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>> _stateRepository;

    /// <summary>Initializes the workflow query context.</summary>
    public IntrinsicTimeStrategyWorkflowQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<IntrinsicTimeStrategyWorkflowQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, IntrinsicTimeStrategyWorkflowQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        ProjectionCache = IntrinsicTimeStrategyWorkflowProjectionCache.Shared;
        TimeProvider = TimeProvider.System;
        Logger = IsArgumentNull.Set(logger);
        _stateRepository = new(() => IsArgumentNull.Set(
            Container.Resolve<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>())!);
    }

    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }

    /// <inheritdoc />
    public IIntrinsicTimeStrategyWorkflowProjectionCache ProjectionCache { get; }

    /// <inheritdoc />
    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> StateRepository
        => _stateRepository.Value;

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }

    /// <inheritdoc />
    public ILogger<IntrinsicTimeStrategyWorkflowQueryActor> Logger { get; }
}
