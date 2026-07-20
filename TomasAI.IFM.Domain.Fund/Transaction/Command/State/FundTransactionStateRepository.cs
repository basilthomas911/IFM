using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.State;

/// <summary>
/// Provides a repository for managing the state of fund transaction commands using event sourcing and actor-based
/// persistence.
/// </summary>
/// <remarks>This repository enables loading and saving of fund transaction command states by leveraging event
/// sourcing patterns. It is intended for use in distributed systems where reliable state management and event replay
/// are required.</remarks>
/// <param name="stateFactory">The factory used to create actor state instances for event sourcing operations.</param>
/// <param name="dbEventSource">The database context for accessing event source data related to fund transactions.</param>
/// <param name="actorService">The actor service responsible for managing actor lifecycles and communication.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the repository.</param>
public class FundTransactionStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IActorService actorService,
    ILogger<FundTransactionStateRepository> logger) 
    : BaseEventSourceActorRepository(stateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FundTransactionCommandState>
{
    /// <summary>
    /// Asynchronously loads the persisted state for the specified fund transaction command.
    /// </summary>
    /// <param name="command">The command for which to load the associated state. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded state for the specified
    /// fund transaction command.</returns>
    public async ValueTask<FundTransactionCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync<FundTransactionCommandState>(command);

    /// <summary>
    /// Asynchronously saves the specified fund transaction command state using the provided command.
    /// </summary>
    /// <param name="context">The command actor context providing contextual information for the save operation. Cannot be null.</param>
    /// <param name="state">The fund transaction command state to be persisted. Cannot be null.</param>
    /// <param name="command">The command associated with the state to be saved. Cannot be null.</param>
    /// <returns>A value task that represents the asynchronous save operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FundTransactionCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the fund transaction query state
    /// asynchronously.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.FundDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FundTransactionEvent e => await UpdateReadModelAsync<FundTransactionEvent, FundTransactionCreatedCompleteEvent, FundTransactionCreatedFailEvent, FundTransactionEntityId>(
                    context, e, () => InsertFundTransactionAsync(e.FundTransaction)),
                FundTransactionsEvent e => await UpdateReadModelAsync<FundTransactionsEvent, FundTransactionsCompleteEvent, FundTransactionsFailEvent, FundTransactionEntityId>(
                    context, e, () => InsertFundTransactionsAsync(e.FundTransactions)),
                EndOfDayFundTransactionProcessedEvent e => await UpdateReadModelAsync<EndOfDayFundTransactionProcessedEvent, EndOfDayFundTransactionProcessedCompleteEvent, EndOfDayFundTransactionProcessedFailEvent, FundTransactionEntityId>(
                    context, e, () => InsertFundTransactionAsync(e.FundTransaction)),
                _ => false
            };
        }

        async ValueTask InsertFundTransactionAsync(FundTransactionReadModel fundTransaction)
            => await db.InsertFundTransactionAsync(fundTransaction);

        async ValueTask InsertFundTransactionsAsync(ICollection<FundTransactionReadModel> fundTransactions)
            => await db.InsertFundTransactionsAsync(fundTransactions);
    }

}

