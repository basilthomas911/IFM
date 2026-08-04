namespace TomasAI.IFM.Shared.EventSourcing
{
    public enum EventType
    {
        DomainEvent,
        ServiceEvent,
        ErrorEvent,
        CompletedEvent,
        ServiceApiEvent
    }
}
