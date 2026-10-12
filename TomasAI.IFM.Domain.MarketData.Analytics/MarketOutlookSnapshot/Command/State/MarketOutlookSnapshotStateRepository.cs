using TomasAI.IFM.Application.Storage.MarketDataDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;

public sealed class MarketOutlookSnapshotStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<MarketOutlookSnapshotCommandActor> eventProjector,
    ILogger<MarketOutlookSnapshotStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState>
{
    public ValueTask<MarketOutlookSnapshotCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    public async ValueTask<MarketOutlookSnapshotCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<
                MarketOutlookSnapshotCommandState,
                MarketOutlookSnapshotInsertedEvent>(command, cancellationToken)
            .ConfigureAwait(false);

    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        MarketOutlookSnapshotCommandState state,
        ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        MarketOutlookSnapshotCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var database = context.Container.Resolve<IDbContextFactory>().MarketDataDb;
        if (!string.Equals(database.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context,state,command,cancellationToken).ConfigureAwait(false);
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
            throw new InvalidOperationException("Analytic source events and MarketDataDb must share one PostgreSQL database/security identity.");
        var owner = context.Container.Resolve<IPostgresEventTransaction>();
        var pending = new DomainEventCollection(state.Events);
        await owner.ExecuteAsync(async (transaction,token) =>
        {
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                await transaction.AppendAsync(command.StreamId,command.CommandId,source,version++,token).ConfigureAwait(false);
                _ = await PostgresMarketSignalStore.TryProjectAsync(transaction,source,token).ConfigureAwait(false);
            }
            return true;
        },cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await DenormalizeEventsAsync(context,pending).ConfigureAwait(false);
    }

    protected override async ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        foreach (var domainEvent in domainEvents)
            await eventProjector.ProcessDomainEventAsync(domainEvent).ConfigureAwait(false);
    }
}
