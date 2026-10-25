using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;

/// <summary>
/// Provides a repository for managing the immutable download state using event sourcing and actor-based persistence.
/// </summary>
/// <param name="actorContext">
/// Provides the state factory, event-source database context, actor service, logger, and DownloadLog event projector.
/// </param>
public sealed class DownloadLogStateRepository(
    ICommandActorContext<DownloadLogCommandActor> actorContext,
    IPostgresEventTransaction? transactions = null)
    : BaseEventSourceActorRepository(
        actorContext.StateFactory,
        actorContext.DbEventSource,
        actorContext.ActorService,
        actorContext.Logger),
      IEventSourceActorStateRepository<DownloadLogCommandState>
{
    readonly IEventProjector<DownloadLogCommandActor> _eventProjector =
        IsArgumentNull.Set(actorContext.EventProjector);

    /// <summary>
    /// load download state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<DownloadLogCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<DownloadLogCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<DownloadLogCommandState, MarketDataDownloadLogInsertedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// save download state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, DownloadLogCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, DownloadLogCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Events.Count == 0) return;
        var database = actorContext.DbFactory.MarketDataDb;
        if (string.Equals(database.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            if (!string.Equals(EventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
                PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
                throw new InvalidOperationException("DownloadLog and EventSourceDb must share the same PostgreSQL database/security identity.");
            var owner = transactions ?? throw new InvalidOperationException("DownloadLog requires the PostgreSQL event transaction service.");
            var pending = new DomainEventCollection(state.Events);
            await owner.ExecuteAsync(async (transaction, token) =>
            {
                long version = 0;
                foreach (var source in pending)
                {
                    if (source is not MarketDataDownloadLogInsertedEvent inserted)
                        throw new InvalidOperationException("Unexpected DownloadLog source event.");
                    await transaction.AppendAsync(command.StreamId,command.CommandId,source,version++,token).ConfigureAwait(false);
                    await database.ProjectDownloadLogAsync(transaction,inserted.Outcome,inserted.CommandId,inserted.PayloadSha256,token).ConfigureAwait(false);
                }
                return true;
            },cancellationToken).ConfigureAwait(false);
            state.AcceptChanges();
            await DenormalizeEventsAsync(context,pending).ConfigureAwait(false);
            return;
        }
        // A terminal attempt can commit only once, including competing host instances.
        var events = await SaveStateEventsAsync(state, command, 0L, cancellationToken).ConfigureAwait(false);
        await DenormalizeEventsAsync(context, events).ConfigureAwait(false);
    }

    /// <summary>
    /// Denormalize events to update read models or projections based on the domain events.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => _eventProjector.DomainEventsProjectionAsync(domainEvents);
}
