using TomasAI.IFM.Domain.EventSourcing.MarketData;
using TomasAI.IFM.Domain.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;

public class FuturesBarDataStateRepository(
    ICommandActorContext<FuturesBarDataCommandActor> actorContext)
    : BaseEventSourceActorRepository(
        actorContext.StateFactory,
        actorContext.DbEventSource,
        actorContext.ActorService,
        actorContext.Logger),
      IEventSourceActorStateRepository<FuturesBarDataCommandState>
{
    readonly IEventProjector<FuturesBarDataCommandActor> _eventProjector =
        IsArgumentNull.Set(actorContext.EventProjector);
    /// <summary>
    /// load futures bar data state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FuturesBarDataCommandState> LoadStateAsync(ICommand command)
        => await (LoadStateFromSnapshotLastNRangeAsync<
            FuturesBarDataCommandState,
            FuturesBarDataStreamingStartedEvent,
            FuturesBarDataInsertedEvent>(command, 0)).ConfigureAwait(false);

    /// <summary>
    /// save futures bar data state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesBarDataCommandState state, ICommand command)
       => await SaveStateAsync(context,state,command,CancellationToken.None).ConfigureAwait(false);

    /// <summary>Commits accepted source facts and required read rows on the same PostgreSQL transaction.</summary>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesBarDataCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var database = actorContext.DbFactory.MarketDataDb;
        if (!string.Equals(database.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context,state,command,cancellationToken).ConfigureAwait(false);
            return;
        }
        if (state.Events.Count == 0) return;
        if (!string.Equals(EventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(EventSourceDb.ConnectionString))
            throw new InvalidOperationException("Feed source facts and observations must share one PostgreSQL database/security identity.");
        var pending = new DomainEventCollection(state.Events);
        var committed = await context.Container.Resolve<IPostgresEventTransaction>().ExecuteAsync(async (transaction,token) =>
        {
            var accepted = new DomainEventCollection();
            var version = state.CommittedStreamVersion;
            foreach (var source in pending)
            {
                IEvent fact = source;
                
                await transaction.AppendAsync(command.StreamId,command.CommandId,fact,version++,token).ConfigureAwait(false);
                _ = await AcceptedMarketSignalProjection.TryProjectAsync(transaction,fact,token).ConfigureAwait(false);
                accepted.Add(fact);
            }
            return accepted;
        },cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await DenormalizeEventsAsync(context,committed).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures bar data query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and either posts the event
    /// or updates the read model accordingly. Streaming events are posted directly, while insert and delete events
    /// update the read model via <see cref="IMarketDataDbContext"/>.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => await _eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
}
