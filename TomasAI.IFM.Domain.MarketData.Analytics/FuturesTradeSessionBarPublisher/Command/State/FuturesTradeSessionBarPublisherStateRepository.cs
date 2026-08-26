using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.State;

/// <summary>Loads and commits trade-session bar publisher state through the ACID event log.</summary>
public sealed class FuturesTradeSessionBarPublisherStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<FuturesTradeSessionBarPublisherCommandActor> eventProjector,
    ILogger<FuturesTradeSessionBarPublisherStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<FuturesTradeSessionBarPublisherCommandState>
{
    readonly IEventProjector<FuturesTradeSessionBarPublisherCommandActor> eventProjector =
        eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));

    /// <summary>Reconstructs the publisher state for the command entity stream.</summary>
    public ValueTask<FuturesTradeSessionBarPublisherCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Reconstructs the publisher state for the command entity stream.</summary>
    public async ValueTask<FuturesTradeSessionBarPublisherCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateAsync<FuturesTradeSessionBarPublisherCommandState>(command, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Commits pending publisher events and queues their projections.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesTradeSessionBarPublisherCommandState state,
        ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Commits pending publisher events and queues their projections.</summary>
    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesTradeSessionBarPublisherCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
        => eventProjector.DomainEventsProjectionAsync(domainEvents);
}
