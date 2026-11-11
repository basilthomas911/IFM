namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>The caller-qualified source event and the revision it must follow.</summary>
public sealed class EnlistedEventAppend
{
    public EnlistedEventAppend(string stream, Guid commandId, IEvent domainEvent, long expectedStreamVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (commandId == Guid.Empty || expectedStreamVersion < 0)
            throw new ArgumentException("Invalid event identity/version.", nameof(commandId));
        Stream = stream;
        CommandId = commandId;
        DomainEvent = domainEvent;
        ExpectedStreamVersion = expectedStreamVersion;
    }

    public string Stream { get; }
    public Guid CommandId { get; }
    public IEvent DomainEvent { get; }
    public long ExpectedStreamVersion { get; }
}
