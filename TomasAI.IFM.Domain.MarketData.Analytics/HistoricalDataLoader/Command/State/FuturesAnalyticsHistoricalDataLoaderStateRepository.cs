using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;

/// <summary>
/// Loads and persists the authoritative historical data-load command state through the ACID event log.
/// </summary>
/// <param name="stateFactory">Factory used to create an empty command-state instance for replay.</param>
/// <param name="eventSource">PostgreSQL event-source context.</param>
/// <param name="actorService">Actor infrastructure used by the base repository.</param>
/// <param name="eventProjector">Projector that dispatches committed data load requests.</param>
/// <param name="logger">Repository logger.</param>
public sealed class FuturesAnalyticsHistoricalDataLoaderStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor> eventProjector,
    ILogger<FuturesAnalyticsHistoricalDataLoaderStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<FuturesAnalyticsHistoricalDataLoaderCommandState>
{
    readonly IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor> _eventProjector =
        eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));

    /// <summary>Reconstructs data load command state by replaying its complete entity stream.</summary>
    /// <param name="command">Command whose identity selects the event stream.</param>
    /// <returns>The reconstructed command state.</returns>
    public ValueTask<FuturesAnalyticsHistoricalDataLoaderCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Reconstructs data load command state by replaying its complete entity stream.</summary>
    /// <param name="command">Command whose identity selects the event stream.</param>
    /// <param name="cancellationToken">Cancellation token honored while reading the event stream.</param>
    /// <returns>The reconstructed command state.</returns>
    public async ValueTask<FuturesAnalyticsHistoricalDataLoaderCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateAsync<FuturesAnalyticsHistoricalDataLoaderCommandState>(command, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Commits pending data load-request events and dispatches them after persistence.</summary>
    /// <param name="context">Command actor context associated with the save.</param>
    /// <param name="state">Command state containing pending events.</param>
    /// <param name="command">Command that produced the pending events.</param>
    /// <returns>A task representing the save operation.</returns>
    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesAnalyticsHistoricalDataLoaderCommandState state,
        ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Commits pending data load-request events and dispatches them after persistence.</summary>
    /// <param name="context">Command actor context associated with the save.</param>
    /// <param name="state">Command state containing pending events.</param>
    /// <param name="command">Command that produced the pending events.</param>
    /// <param name="cancellationToken">Cancellation token honored until the event-log commit completes.</param>
    /// <returns>A task representing the save operation.</returns>
    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesAnalyticsHistoricalDataLoaderCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Queues committed data load-request events for application execution.</summary>
    /// <param name="context">Command actor context associated with the committed batch.</param>
    /// <param name="domainEvents">Committed events awaiting projection.</param>
    /// <returns>A task representing projector queueing.</returns>
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
        => _eventProjector.DomainEventsProjectionAsync(domainEvents);
}
