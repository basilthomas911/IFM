using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

public class FuturesRsiSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    IEventProjector<FuturesRsiSignalCommandActor> eventProjector,
    ILogger<FuturesRsiSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesRsiSignalCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesRsiSignalCommandState.</returns>
    public async ValueTask<FuturesRsiSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<FuturesRsiSignalCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => command switch
        {
            ICommand<FuturesRsiDailySignalEntityId> dailyCommand
                => await LoadStateAsync<
                    FuturesRsiSignalCommandState,
                    FuturesRsiDailySignalGeneratedEvent>(command, dailyCommand.EntityId.PeriodLength, cancellationToken),
            ICommand<FuturesRsiSignalEntityId> rsiCommand
                => await LoadStateFromSnapshotLastNRangeAsync<
                    FuturesRsiSignalCommandState,
                    FuturesRsiSignalStartedEvent,
                    FuturesRsiSignalGeneratedEvent>(command, StateWindow(rsiCommand.EntityId), cancellationToken),
            _ => await LoadStateFromSnapshotLastNRangeAsync<
                FuturesRsiSignalCommandState,
                FuturesRsiSignalStartedEvent,
                FuturesRsiSignalGeneratedEvent>(command, 0, cancellationToken)
        };

    static int StateWindow(FuturesRsiSignalEntityId entityId)
    {
        var configuration = FuturesTdiConfiguration.Standard;
        return entityId.PeriodLength == configuration.RsiPeriod
               && FuturesTdiConfiguration.IsSupportedIntraday(entityId.TimePeriod)
            ? entityId.PeriodLength + configuration.RequiredRsiSamples
            : entityId.PeriodLength;
    }

    /// <summary>
    /// Saves futures RSI signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesRsiSignalCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesRsiSignalCommandState state, ICommand command, CancellationToken cancellationToken)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Denormalizes domain events related to futures RSI signals and updates the read model in the database.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => await eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
}
