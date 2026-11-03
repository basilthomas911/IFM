using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.EventSourcing;
/// <summary>Replay behavior belongs to Application; read models remain neutral data carriers.</summary>
public static class EventLogReplayExtensions
{

    extension(IEnumerable<EventStreamReadModel>? rows)
    {
        public DomainEventCollection ToDomainEvents()
            => new(rows is null ? [] : rows.Where(row => row is not null).Select(row => row.ToDomainEvent()));
    }

    extension(EventLogReadModel row)
    {
        public IEvent ToDomainEvent()
            => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData);

        public IEvent ToDomainEvent(IEventTypeResolver resolver)
            => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData, resolver);
    }

    extension(EventStreamReadModel row)
    {
        public IEvent ToDomainEvent()
            => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData);

        public IEvent ToDomainEvent(IEventTypeResolver resolver)
            => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData, resolver);
    }
}
