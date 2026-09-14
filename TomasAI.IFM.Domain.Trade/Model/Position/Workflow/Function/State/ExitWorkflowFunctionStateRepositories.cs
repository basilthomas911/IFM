using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;

/// <summary>Loads and saves event-sourced exit-order composition function state.</summary>
/// <param name="states">The state factory used during rehydration.</param>
/// <param name="eventSource">The event-source database context.</param>
/// <param name="actors">The actor runtime service.</param>
/// <param name="logger">The repository logger.</param>
public sealed class ExitOrderCompositionFunctionStateRepository(
    IEventSourceActorStateFactory states, IEventSourceActorDbContext eventSource,
    IActorService actors, ILogger<ExitOrderCompositionFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(states, eventSource, actors, logger),
        IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState, ComposeExitOrderCommand>
{
    /// <summary>Loads the latest completion snapshot and prepares it for the current request.</summary>
    /// <param name="request">The current composition request.</param>
    /// <param name="cancellationToken">Cancels state loading.</param>
    /// <returns>The rehydrated and prepared function state.</returns>
    public async ValueTask<ExitOrderCompositionFunctionState> LoadStateAsync(
        ComposeExitOrderCommand request, CancellationToken cancellationToken = default) =>
        (await LoadStateFromSnapshotAsync<ExitOrderCompositionFunctionState,
            ExitOrderCompositionCompletedEvent>(request, cancellationToken).ConfigureAwait(false)).Prepare(request);
    /// <summary>Saves the newly completed function events.</summary>
    /// <param name="context">The function actor context.</param>
    /// <param name="state">The completed state to persist.</param>
    /// <param name="request">The command associated with the completion.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>A task that completes after the event transaction commits.</returns>
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        ExitOrderCompositionFunctionState state, ComposeExitOrderCommand request,
        CancellationToken cancellationToken = default) =>
        _ = await SaveStateEventsAsync(state, request, state.LastPersistedEventId, cancellationToken)
            .ConfigureAwait(false);
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection events) => ValueTask.CompletedTask;
}

/// <summary>Loads and saves event-sourced position exit-risk function state.</summary>
/// <param name="states">The state factory used during rehydration.</param>
/// <param name="eventSource">The event-source database context.</param>
/// <param name="actors">The actor runtime service.</param>
/// <param name="logger">The repository logger.</param>
public sealed class PositionExitRiskFunctionStateRepository(
    IEventSourceActorStateFactory states, IEventSourceActorDbContext eventSource,
    IActorService actors, ILogger<PositionExitRiskFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(states, eventSource, actors, logger),
        IEventSourceFunctionStateRepository<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand>
{
    /// <summary>Loads the latest completion snapshot and prepares it for the current request.</summary>
    /// <param name="request">The current position exit-risk request.</param>
    /// <param name="cancellationToken">Cancels state loading.</param>
    /// <returns>The rehydrated and prepared function state.</returns>
    public async ValueTask<PositionExitRiskFunctionState> LoadStateAsync(
        EvaluatePositionExitRiskCommand request, CancellationToken cancellationToken = default) =>
        (await LoadStateFromSnapshotAsync<PositionExitRiskFunctionState,
            PositionExitRiskCompletedEvent>(request, cancellationToken).ConfigureAwait(false)).Prepare(request);
    /// <summary>Saves the newly completed function events.</summary>
    /// <param name="context">The function actor context.</param>
    /// <param name="state">The completed state to persist.</param>
    /// <param name="request">The command associated with the completion.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>A task that completes after the event transaction commits.</returns>
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        PositionExitRiskFunctionState state, EvaluatePositionExitRiskCommand request,
        CancellationToken cancellationToken = default) =>
        _ = await SaveStateEventsAsync(state, request, state.LastPersistedEventId, cancellationToken)
            .ConfigureAwait(false);
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection events) => ValueTask.CompletedTask;
}
