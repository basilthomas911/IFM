using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;

public class FuturesAdxSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<FuturesAdxSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesAdxSignalCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesAdxSignalCommandState.</returns>
    public async ValueTask<FuturesAdxSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<FuturesAdxSignalCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => command switch
        {
            ICommand<FuturesAdxDailySignalEntityId> dailyCommand
                => await LoadStateAsync<FuturesAdxSignalCommandState, FuturesAdxDailySignalGeneratedEvent>(
                    command,
                    dailyCommand.EntityId.PeriodLength, cancellationToken),
            ICommand<FuturesAdxSignalEntityId> adxCommand
                => await LoadStateFromSnapshotLastNRangeAsync<
                    FuturesAdxSignalCommandState,
                    FuturesAdxSignalStartedEvent,
                    FuturesAdxSignalGeneratedEvent>(command, adxCommand.EntityId.PeriodLength, cancellationToken),
            _ => throw new ArgumentException($"Unsupported command type: {command.GetType().Name}", nameof(command))
        };

    /// <summary>
    /// Saves futures ADX signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesAdxSignalCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesAdxSignalCommandState state, ICommand command, CancellationToken cancellationToken)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures ADX signal
    /// read model asynchronously.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.MarketDataDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FuturesAdxSignalGeneratedEvent e => await UpdateReadModelAsync<FuturesAdxSignalGeneratedEvent, FuturesAdxSignalGeneratedCompleteEvent, FuturesAdxSignalGeneratedFailEvent, FuturesAdxSignalEntityId>(
                    context, e, () => InsertFuturesAdxSignalAsync(db, e.FuturesAdxSignal)),
                FuturesAdxDailySignalGeneratedEvent e => await UpdateReadModelAsync<FuturesAdxDailySignalGeneratedEvent, FuturesAdxDailySignalGeneratedCompleteEvent, FuturesAdxDailySignalGeneratedFailEvent, FuturesAdxDailySignalEntityId>(
                    context, e, () => InsertFuturesAdxSignalAsync(db, e.FuturesAdxSignal)),
                FuturesAdxSignalStartedEvent e => await PostEventAsync<FuturesAdxSignalStartedEvent, FuturesAdxSignalEntityId>(context, e),
                FuturesAdxSignalStoppedEvent e => await PostEventAsync<FuturesAdxSignalStoppedEvent, FuturesAdxSignalEntityId>(context, e),
                _ => false
            };
        }

        static async ValueTask InsertFuturesAdxSignalAsync(IMarketDataDbContext db, FuturesAdxSignalReadModel futuresAdxSignal)
            => await db.InsertFuturesAdxSignalAsync(futuresAdxSignal);
    }
}


