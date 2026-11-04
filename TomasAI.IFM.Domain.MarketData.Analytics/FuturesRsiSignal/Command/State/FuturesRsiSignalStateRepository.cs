using TomasAI.IFM.Application.EventSourcing.Projections.MarketData;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

public class FuturesRsiSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    IEventProjector<FuturesRsiSignalCommandActor> eventProjector,
    ILogger<FuturesRsiSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesRsiSignalCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesRsiSignalCommandState.</returns>
    public async ValueTask<FuturesRsiSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<FuturesRsiSignalCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
    {
        var window = command switch
        {
            ICommand<FuturesRsiDailySignalEntityId> daily => daily.EntityId.PeriodLength,
            ICommand<FuturesRsiSignalEntityId> rsi => StateWindow(rsi.EntityId),
            _ => 256
        };
        var requestedWindow = FuturesRsiSignalCommandState.NormalizeReplayWindow(window);
        return await LoadStateFromLatestCheckpointAsync<FuturesRsiSignalCommandState, FuturesRsiSignalStateSnapshottedEvent>(
            command, snapshot => snapshot.RetainedWindow >= requestedWindow && snapshot.Signals is not null,
            cancellationToken, state => state.LimitReplayWindow(requestedWindow)).ConfigureAwait(false);
    }

    static int StateWindow(FuturesRsiSignalEntityId entityId)
    {
        var configuration = FuturesTdiConfiguration.Standard;
        return entityId.PeriodLength == configuration.RsiPeriod
               && FuturesTdiConfiguration.IsSupportedIntraday(entityId.TimePeriod)
            ? entityId.PeriodLength + configuration.RequiredRsiSamples
            : entityId.PeriodLength;
    }

    /// <summary>
    /// Saves futures RSI signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesRsiSignalCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesRsiSignalCommandState state, ICommand command, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var database = dbFactory.MarketDataDb;
        if (!string.Equals(database.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await SaveStateWithPeriodicSnapshotAsync(context, state, command, () => state.CaptureSnapshot(command), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
            throw new InvalidOperationException("Analytic source events and MarketDataDb must share one PostgreSQL database/security identity.");
        var owner = context.Container.Resolve<IPostgresEventTransaction>();
        var pending = new DomainEventCollection(state.Events);
        var snapshot = ShouldCapturePeriodicSnapshot(state.CommittedStreamVersion, pending.Count)
            ? state.CaptureSnapshot(command) : null;
        await owner.ExecuteAsync(async (transaction,token) =>
        {
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                await transaction.AppendAsync(command.StreamId,command.CommandId,source,version++,token).ConfigureAwait(false);
                _ = await AcceptedMarketSignalProjection.TryProjectAsync(transaction,source,token).ConfigureAwait(false);
            }
            if (snapshot is not null)
                await transaction.AppendAsync(command.StreamId, command.CommandId, snapshot, version, token).ConfigureAwait(false);
            return true;
        },cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        if (snapshot is not null)
            state.RestoreCommittedStreamVersion(checked(state.CommittedStreamVersion + 1));
        await DenormalizeEventsAsync(context,pending).ConfigureAwait(false);
    }

    /// <summary>
    /// Denormalizes domain events related to futures RSI signals and updates the read model in the database.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => await eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
}
