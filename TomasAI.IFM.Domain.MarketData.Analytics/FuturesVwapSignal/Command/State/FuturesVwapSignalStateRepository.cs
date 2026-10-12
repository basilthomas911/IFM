using TomasAI.IFM.Domain.EventSourcing.MarketData;
using TomasAI.IFM.Domain.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;

/// <summary>Loads and saves event-sourced futures-session VWAP state.</summary>
public sealed class FuturesVwapSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IEventProjector<FuturesVwapSignalCommandActor> eventProjector,
    ILogger<FuturesVwapSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger),
      IEventSourceActorStateRepository<FuturesVwapSignalCommandState>
{
    /// <inheritdoc />
    public ValueTask<FuturesVwapSignalCommandState> LoadStateAsync(ICommand command) =>
        LoadStateAsync(command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask<FuturesVwapSignalCommandState> LoadStateAsync(
        ICommand command, CancellationToken cancellationToken) =>
        await (LoadStateFromSnapshotAsync<FuturesVwapSignalCommandState, FuturesVwapSignalUpdatedEvent>(
            command,
            cancellationToken)).ConfigureAwait(false);
    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context,
        FuturesVwapSignalCommandState state, ICommand command) =>
        SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        FuturesVwapSignalCommandState state, ICommand command,
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
                _ = await AcceptedMarketSignalProjection.TryProjectAsync(transaction,source,token).ConfigureAwait(false);
            }
            return true;
        },cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await DenormalizeEventsAsync(context,pending).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async ValueTask DenormalizeEventsAsync(
        ICommandActorContext context, DomainEventCollection domainEvents) =>
        await (eventProjector.DomainEventsProjectionAsync(domainEvents)).ConfigureAwait(false);
}
