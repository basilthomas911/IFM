using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.State;

/// <summary>Loads and saves event-sourced VX term-structure state.</summary>
public sealed class FuturesVxTermStructureSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IEventProjector<FuturesVxTermStructureSignalCommandActor> eventProjector,
    ILogger<FuturesVxTermStructureSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger),
      IEventSourceActorStateRepository<FuturesVxTermStructureSignalCommandState>
{
    /// <inheritdoc />
    public ValueTask<FuturesVxTermStructureSignalCommandState> LoadStateAsync(ICommand command) =>
        LoadStateAsync(command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask<FuturesVxTermStructureSignalCommandState> LoadStateAsync(
        ICommand command, CancellationToken cancellationToken) =>
        await LoadStateFromSnapshotLastNRangeAsync<FuturesVxTermStructureSignalCommandState,
            FuturesVxTermStructureSignalUpdatedEvent, FuturesVxTermStructureSignalUpdatedEvent>(
                command, 256, cancellationToken);
    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context,
        FuturesVxTermStructureSignalCommandState state, ICommand command) =>
        SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        FuturesVxTermStructureSignalCommandState state, ICommand command,
        CancellationToken cancellationToken) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask DenormalizeEventsAsync(
        ICommandActorContext context, DomainEventCollection domainEvents) =>
        await eventProjector.DomainEventsProjectionAsync(domainEvents);
}
