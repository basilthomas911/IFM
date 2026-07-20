using StackExchange.Redis;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

/// <summary>
/// Defines a contract for interacting with the event sourcing database for actor state management.
/// </summary>
/// <remarks>This interface provides methods for managing event streams, including saving, deleting, and loading
/// events, as well as performing map-reduce operations on event streams. It is designed to support event-sourced
/// systems where actor states are reconstructed from event streams.</remarks>
public interface IEventSourceActorDbContext
{
    Task DeleteEventLogAsync(long eventVersion);
    Task DeleteEventLogsAsync(long[] eventVersions);
    Task DeleteEventLogByStreamIdAsync(long streamId);
    Task DeleteEventStreamByIdAsync(long eventStreamId);
    Task<long> GetEventStreamIdAsync(string eventStream);
    Task<EventStreamIdReadModel?> GetEventStreamIdFromDbAsync(string eventStream);
    Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent;
    Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData);
    Task UpdateCommandLogAsync(Guid commandId, DateTime updateTimestamp, CommandStatus commandStatus);

    Task<DomainEventCollection> SaveEventsAsync( string eventStream, Guid commandId, DomainEventCollection domainEvents, Func<DomainEventCollection, ValueTask> func);

    ValueTask MapReduceActorEventStreamAsync<TState>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction)
    where TState : IActorState<TState>;

    ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(long eventStreamId, int lastNRange, Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState> where TEvent : IEvent;

    ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState> where TSnapshot : IEvent;

    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState>(long eventStreamId) 
        where TState : IActorState<TState>;

    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TEvent>(long eventStreamId, int lastNRange)
        where TState : IActorState<TState> where TEvent : IEvent;

    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TSnapshot>(long eventStreamId)
        where TState : IActorState<TState> where TSnapshot : IEvent;

}
