namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Pairs a source event with its resolved persistent event-name identity.</summary>
public sealed record EventLogAppendEntry
{
    public EventLogAppendEntry(int eventNameId, IEvent domainEvent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventNameId);
        ArgumentNullException.ThrowIfNull(domainEvent);
        EventNameId = eventNameId;
        DomainEvent = domainEvent;
    }

    public int EventNameId { get; }
    public IEvent DomainEvent { get; }
}
