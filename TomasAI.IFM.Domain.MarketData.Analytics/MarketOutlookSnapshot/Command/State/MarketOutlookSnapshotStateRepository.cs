using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;

public sealed class MarketOutlookSnapshotStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<MarketOutlookSnapshotCommandActor> eventProjector,
    ILogger<MarketOutlookSnapshotStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState>
{
    public ValueTask<MarketOutlookSnapshotCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    public async ValueTask<MarketOutlookSnapshotCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<
                MarketOutlookSnapshotCommandState,
                MarketOutlookSnapshotInsertedEvent>(command, cancellationToken)
            .ConfigureAwait(false);

    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        MarketOutlookSnapshotCommandState state,
        ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        MarketOutlookSnapshotCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken)
            .ConfigureAwait(false);

    protected override async ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
    {
        foreach (var domainEvent in domainEvents)
            await eventProjector.ProcessDomainEventAsync(domainEvent).ConfigureAwait(false);
    }
}
