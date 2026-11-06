using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Domain.Trade.Shared.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
/// <summary>Loads completed Function state and saves it without denormalization.</summary>
public sealed class RegimeDiscoveryFunctionStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, ILogger<RegimeDiscoveryFunctionStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), ITransactionalFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand, RegimeDiscoveryPipelineCompletedEvent>, INativeTradeFunctionRepository
{
    public bool UsesAtomicPersistence => string.Equals(databases?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    public async ValueTask<RegimeDiscoveryPipelineCompletedEvent> CommitAsync(IFunctionActorContext context,
        ExecuteRegimeDiscoveryPipelineCommand request, RegimeDiscoveryPipelineCompletedEvent candidate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await TradePipelineAtomicPersistence.CommitAsync(request, candidate, EventSourceDb, databases,
                transactions, cancellationToken).ConfigureAwait(false);
        }
        catch (CommandAuditDuplicateException exception) when (exception.HasCommittedEvents)
        {
            // The command audit verified the original payload before classifying the committed duplicate.
            var state = await LoadStateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!state.Matches(request) || state.CompletedEvent is not { EventId: > 0 } completed
                || completed.CommandId != request.CommandId || completed.EntityId != request.WorkflowEntityId
                || completed.CorrelationId != request.CorrelationId || completed.CausationId != request.CausationId
                || completed.ExpiresAtUtc != request.ExpiresAtUtc || completed.SignalSnapshotId != request.Snapshot.SnapshotId
                || !string.Equals(completed.AggregateId, request.EntityId.Format(), StringComparison.Ordinal))
                throw;

            _ = TradeProjectionFacts.Regime(completed);
            return completed;
        }
    }
    public async ValueTask<RegimeDiscoveryFunctionState> LoadStateAsync(ExecuteRegimeDiscoveryPipelineCommand request, CancellationToken cancellationToken = default) => await LoadStateFromLatestCheckpointAsync<RegimeDiscoveryFunctionState, RegimeDiscoveryPipelineCompletedEvent>(
        request, static fact => fact.EventId > 0, cancellationToken).ConfigureAwait(false);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, RegimeDiscoveryFunctionState state, ExecuteRegimeDiscoveryPipelineCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SaveStateEventsAsync(state, request, expectedStreamVersion: 0, cancellationToken).ConfigureAwait(false);
    }

    // Command repositories still use the denormalizing save path. This Function repository never calls it.
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
