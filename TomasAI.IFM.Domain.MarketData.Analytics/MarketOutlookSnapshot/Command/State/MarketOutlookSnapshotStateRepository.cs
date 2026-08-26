using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;

/// <summary>
/// Loads Market Outlook command state through event replay and transactionally persists new transitions.
/// </summary>
public sealed class MarketOutlookSnapshotStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IEventProjector<MarketOutlookSnapshotCommandActor> eventProjector,
    ILogger<MarketOutlookSnapshotStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, dbEventSource, actorService, logger),
      IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState>
{
    readonly IEventProjector<MarketOutlookSnapshotCommandActor> eventProjector =
        eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));

    /// <summary>Loads the aggregate from its latest published checkpoint and following events.</summary>
    public ValueTask<MarketOutlookSnapshotCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Loads the aggregate with cancellation before the durable processing boundary.</summary>
    public async ValueTask<MarketOutlookSnapshotCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<
                MarketOutlookSnapshotCommandState,
                MarketOutlookSnapshotPublishedEvent>(command, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Persists state through the repository compatibility contract.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        MarketOutlookSnapshotCommandState state,
        ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Persists state through the repository compatibility contract with cancellation.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        MarketOutlookSnapshotCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
        => context is ICommandActorContext<MarketOutlookSnapshotCommandActor> typedContext
            ? SaveStateAsync(typedContext, state, command, cancellationToken)
            : throw new InvalidOperationException(
                $"{nameof(MarketOutlookSnapshotStateRepository)} requires {nameof(ICommandActorContext<MarketOutlookSnapshotCommandActor>)}.");

    /// <summary>Persists and denormalizes state using the closed-generic command context.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        MarketOutlookSnapshotCommandState state,
        ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Persists and denormalizes state using the closed-generic context and cancellation.</summary>
    public async ValueTask SaveStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        MarketOutlookSnapshotCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(
                context,
                state,
                command,
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Delegates committed event projection to the command-owned projector.</summary>
    protected override async ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
        => await eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
}
