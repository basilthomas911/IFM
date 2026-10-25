using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.EventSourcing;
/// <summary>Replay behavior belongs to Application; read models remain neutral data carriers.</summary>
public static class EventLogReplayExtensions
{
    public static DomainEventCollection ToDomainEvents(this IEnumerable<EventStreamReadModel>? rows)
        => new(rows is null ? [] : rows.Where(row => row is not null).Select(row => row.ToDomainEvent()));
    public static IEvent ToDomainEvent(this EventLogReadModel row)
        => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData);
    public static IEvent ToDomainEvent(this EventLogReadModel row, IEventTypeResolver resolver)
        => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData, resolver);
    public static IEvent ToDomainEvent(this EventStreamReadModel row)
        => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData);
    public static IEvent ToDomainEvent(this EventStreamReadModel row, IEventTypeResolver resolver)
        => EventLogBinaryReader.Read(row.EventTypeName, row.EventVersion, row.EventData, resolver);
}
