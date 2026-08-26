using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State;

/// <summary>Loads and saves event-sourced Bollinger command state.</summary>
public sealed class FuturesBbSignalStateRepository(IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource, IActorService actorService,
    IEventProjector<FuturesBbSignalCommandActor> eventProjector,
    ILogger<FuturesBbSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger),
      IEventSourceActorStateRepository<FuturesBbSignalCommandState>
{
    /// <inheritdoc />
    public ValueTask<FuturesBbSignalCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask<FuturesBbSignalCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken) =>
        await LoadStateFromSnapshotLastNRangeAsync<FuturesBbSignalCommandState,
            FuturesBbSignalGeneratedEvent, FuturesBbSignalGeneratedEvent>(command, 64, cancellationToken);
    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context, FuturesBbSignalCommandState state, ICommand command) =>
        SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesBbSignalCommandState state,
        ICommand command, CancellationToken cancellationToken) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) =>
        await eventProjector.DomainEventsProjectionAsync(domainEvents);
}
