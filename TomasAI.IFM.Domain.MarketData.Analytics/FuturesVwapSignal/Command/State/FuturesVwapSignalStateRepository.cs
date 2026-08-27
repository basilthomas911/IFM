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
        await LoadStateFromSnapshotLastNRangeAsync<FuturesVwapSignalCommandState,
            FuturesVwapSignalUpdatedEvent, FuturesVwapSignalUpdatedEvent>(
                command, 256, cancellationToken);
    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context,
        FuturesVwapSignalCommandState state, ICommand command) =>
        SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        FuturesVwapSignalCommandState state, ICommand command,
        CancellationToken cancellationToken) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask DenormalizeEventsAsync(
        ICommandActorContext context, DomainEventCollection domainEvents) =>
        await eventProjector.DomainEventsProjectionAsync(domainEvents);
}
