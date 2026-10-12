using TomasAI.IFM.Domain.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
/// <summary>Loads the latest source snapshot and commits PostgreSQL event and plan writes atomically.</summary>
/// <remarks>No history is replayed or cached. The Scylla path is retained only until development data is migrated.</remarks>
public sealed class IronCondorTradePlanFunctionStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, IEventProjector<FuturesIronCondorTradePositionCommandActor> projector, ILogger<IronCondorTradePlanFunctionStateRepository> logger, IDbContextFactory? dbFactory = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand>
{
    /// <summary>Restores only the last full snapshot in the source stream; it never queries Scylla or loads earlier plans.</summary>
    /// <param name = "request">The current position and captured calculation inputs.</param>
    /// <param name = "cancellationToken">Cancels the single-snapshot load.</param>
    /// <returns>A request-local state initialized from at most one persisted snapshot event.</returns>
    public async ValueTask<IronCondorTradePlanFunctionState> LoadStateAsync(UpdateIronCondorTradePlanCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var state = new IronCondorTradePlanFunctionState
        {
            Id = request.Subject.ThreadId
        };
        var streamId = await GetStreamId(request.StreamId, cancellationToken).ConfigureAwait(false);
        await EventSourceDb.MapReduceActorEventStreamAsync<IronCondorTradePlanFunctionState, IronCondorTradePlanUpdatedEvent>(streamId, 1, state.ReplayEvents, cancellationToken).ConfigureAwait(false);
        return state.Prepare(request);
    }

    /// <summary>Commits the snapshot source event and required PostgreSQL rows together; failed updates are logged and dropped.</summary>
    /// <param name = "context">The function context.</param>
    /// <param name = "state">The current request's state and pending snapshot event.</param>
    /// <param name = "request">The originating position update.</param>
    /// <param name = "cancellationToken">Cancels source persistence before commit.</param>
    /// <returns>The atomic source/projection commit followed by optional monitoring publication. Failed writes leave neither source nor projection rows.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A failed post-commit notification is logged and dropped; disposable plan persistence must not replay an older snapshot.")]
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, IronCondorTradePlanFunctionState state, UpdateIronCondorTradePlanCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);
        if (string.Equals(dbFactory?.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            var transactionOwner = transactions ?? throw new InvalidOperationException("Atomic plan persistence requires the PostgreSQL event transaction service.");
            if (!string.Equals(dbFactory.ActorEventSourceDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) || PostgresDatabaseIdentity.Parse(dbFactory.ActorEventSourceDb.ConnectionString) != PostgresDatabaseIdentity.Parse(dbFactory.TradePlanDb.ConnectionString))
                throw new InvalidOperationException("TradePlanDb and EventSourceDb must share one PostgreSQL database and security identity.");
            var pending = new DomainEventCollection(state.Events);
            try
            {
                await transactionOwner.ExecuteOnceAsync(async (transaction, token) =>
                {
                    var version = state.CommittedStreamVersion;
                    foreach (var source in pending)
                    {
                        await transaction.AppendAsync(request.StreamId, request.CommandId, source, version++, token).ConfigureAwait(false);
                        if (source is IronCondorTradePlanUpdatedEvent snapshot)
                            await dbFactory.TradePlanDb.ProjectMaterialAsync(transaction, snapshot.Plan, token).ConfigureAwait(false);
                    }

                    return true;
                }, cancellationToken).ConfigureAwait(false);
                state.AcceptChanges();
            }
            catch (Exception error)
            {
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                    global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write7(logger, error, request.CommandId, request.EntityId.Position.Trade.Format(), request.EntityId.ValueDate, state.CompletedEvent?.Id, state.LatestPlanRevision);
                throw;
            }

            // Optional monitoring publication occurs after commit; the PostgreSQL projector does not write again.
            try
            {
                await projector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                    global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write8(logger, error, request.CommandId);
            }

            return;
        }

        var committed = await SaveStateEventsAsync(state, request, state.CommittedStreamVersion, cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        try
        {
            await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write9(logger, error, nameof(SaveCompletedStateAsync), request.CommandId, request.EntityId.Position.Trade.Format(), request.EntityId.ValueDate, state.CompletedEvent?.EventId, state.LatestPlanRevision);
        }
    }

    /// <inheritdoc/>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
