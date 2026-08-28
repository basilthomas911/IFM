using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;

/// <summary>Loads and commits trade-session bar signal state through the ACID event log.</summary>
public sealed class FuturesTradeSessionBarSignalStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<FuturesTradeSessionBarSignalCommandActor> eventProjector,
    ILogger<FuturesTradeSessionBarSignalStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<FuturesTradeSessionBarSignalCommandState>
{
    readonly IEventProjector<FuturesTradeSessionBarSignalCommandActor> eventProjector =
        eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));

    /// <summary>Reconstructs the publisher state for the command entity stream.</summary>
    public ValueTask<FuturesTradeSessionBarSignalCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Reconstructs the publisher state for the command entity stream.</summary>
    public async ValueTask<FuturesTradeSessionBarSignalCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateAsync<FuturesTradeSessionBarSignalCommandState>(command, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Commits pending publisher events and queues their projections.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesTradeSessionBarSignalCommandState state,
        ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Commits pending publisher events and queues their projections.</summary>
    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        FuturesTradeSessionBarSignalCommandState state,
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
