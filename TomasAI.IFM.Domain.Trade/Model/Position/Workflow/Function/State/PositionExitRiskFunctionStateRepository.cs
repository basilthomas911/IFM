using TomasAI.IFM.Domain.Trade.Persistence;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
/// <summary>Loads and saves event-sourced position exit-risk function state.</summary>
/// <param name = "states">The state factory used during rehydration.</param>
/// <param name = "eventSource">The event-source database context.</param>
/// <param name = "actors">The actor runtime service.</param>
/// <param name = "logger">The repository logger.</param>
public sealed class PositionExitRiskFunctionStateRepository(IEventSourceActorStateFactory states, IEventSourceActorDbContext eventSource, IActorService actors, ILogger<PositionExitRiskFunctionStateRepository> logger, IDbContextFactory? dbFactory = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(states, eventSource, actors, logger), IEventSourceFunctionStateRepository<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand>
{
    /// <summary>Loads the latest completion snapshot and prepares it for the current request.</summary>
    /// <param name = "request">The current position exit-risk request.</param>
    /// <param name = "cancellationToken">Cancels state loading.</param>
    /// <returns>The rehydrated and prepared function state.</returns>
    public async ValueTask<PositionExitRiskFunctionState> LoadStateAsync(EvaluatePositionExitRiskCommand request, CancellationToken cancellationToken = default) => (await LoadStateFromSnapshotAsync<PositionExitRiskFunctionState, PositionExitRiskCompletedEvent>(request, cancellationToken).ConfigureAwait(false)).Prepare(request);
    /// <summary>Saves the newly completed function events.</summary>
    /// <param name = "context">The function actor context.</param>
    /// <param name = "state">The completed state to persist.</param>
    /// <param name = "request">The command associated with the completion.</param>
    /// <param name = "cancellationToken">Cancels persistence.</param>
    /// <returns>A task that completes after the event transaction commits.</returns>
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, PositionExitRiskFunctionState state, EvaluatePositionExitRiskCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.Equals(dbFactory?.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await ExitWorkflowAtomicPersistence.CommitAsync(request, new DomainEventCollection(state.Events), state.CommittedStreamVersion,
                EventSourceDb, dbFactory, transactions, cancellationToken).ConfigureAwait(false);
            state.AcceptChanges();
            return;
        }
        _ = await SaveStateEventsAsync(state, request, state.LastPersistedEventId, cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
