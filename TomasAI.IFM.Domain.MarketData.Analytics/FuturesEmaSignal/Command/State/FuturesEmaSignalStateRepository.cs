using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;

/// <summary>Loads and saves event-sourced EMA command state.</summary>
public sealed class FuturesEmaSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IEventProjector<FuturesEmaSignalCommandActor> eventProjector,
    ILogger<FuturesEmaSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger),
      IEventSourceActorStateRepository<FuturesEmaSignalCommandState>
{
    /// <inheritdoc />
    public ValueTask<FuturesEmaSignalCommandState> LoadStateAsync(ICommand command) =>
        LoadStateAsync(command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask<FuturesEmaSignalCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken) =>
        await LoadStateFromSnapshotLastNRangeAsync<FuturesEmaSignalCommandState,
            FuturesEmaSignalGeneratedEvent, FuturesEmaSignalGeneratedEvent>(command, 256, cancellationToken);
    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context, FuturesEmaSignalCommandState state, ICommand command) =>
        SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesEmaSignalCommandState state,
        ICommand command, CancellationToken cancellationToken) =>
        await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) =>
        await eventProjector.DomainEventsProjectionAsync(domainEvents);
}
