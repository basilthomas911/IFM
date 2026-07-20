using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

public interface IEventSourceDbContext
{
    Task DeleteEventLogAsync(long eventVersion);
    Task DeleteEventLogsAsync(long[] eventVersions);
    ValueTask<long> GetEventStreamIdAsync(string eventStream);
    ValueTask MapReduceEventStreamAsync<TBoundedContext>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction) 
        where TBoundedContext : IBoundedContext;

    ValueTask MapReduceEventStreamAsync<TBoundedContext, TEvent>(long eventStreamId, int lastNRange, Action<IEnumerable<EventStreamReadModel>> reducerAction) 
        where TBoundedContext : IBoundedContext where TEvent : IEvent;

    ValueTask MapReduceEventStreamAsync<TBoundedContext, TSnapshot>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction) 
        where TBoundedContext : IBoundedContext where TSnapshot : IEvent;

    ValueTask<ICollection<EventStreamReadModel>> LoadEventStreamAsync<TBoundedContext>(long eventStreamId) where TBoundedContext : IBoundedContext;
    ValueTask<ICollection<EventStreamReadModel>> LoadEventStreamAsync<TBoundedContext, TEvent>(long eventStreamId, int lastNRange) where TBoundedContext : IBoundedContext where TEvent : IEvent;
    ValueTask<ICollection<EventStreamReadModel>> LoadEventStreamAsync<TBoundedContext, TSnapshot>(long eventStreamId) where TBoundedContext : IBoundedContext where TSnapshot : IEvent;
    Task<DomainEventCollection> SaveEventsAsync( string eventStream, Guid commandId, DomainEventCollection domainEvents, Func<DomainEventCollection, ValueTask> denormalizer);
}
