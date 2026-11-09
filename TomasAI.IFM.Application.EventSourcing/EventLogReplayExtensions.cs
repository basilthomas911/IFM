using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
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
            => MessageOrigins.Restore(EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData), row.Origin);

        public IEvent ToDomainEvent(IEventTypeResolver resolver)
            => MessageOrigins.Restore(EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData, resolver), row.Origin);
    }

    extension(EventStreamReadModel row)
    {
        public IEvent ToDomainEvent()
            => MessageOrigins.Restore(EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData), row.Origin);

        public IEvent ToDomainEvent(IEventTypeResolver resolver)
            => MessageOrigins.Restore(EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData, resolver), row.Origin);
    }
}
