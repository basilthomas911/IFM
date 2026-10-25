using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Event published when futures tick data streaming has been stopped successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTickDataStreamingStoppedCompleteEvent : ICompleteEvent<FuturesTickDataStreamingId>
{
    [IgnoreMember] public const string Actor = "FuturesTickDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStoppedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesTickDataStreamingId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateTime StoppedOn { get; init; }
    [Key(9)] public string StoppedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesTickDataStreamingStoppedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesTickDataStreamingStoppedCompleteEvent(
        ActorSubject subject,
        FuturesTickDataStreamingId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime stoppedOn,
        string stoppedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        StoppedOn = stoppedOn;
        StoppedBy = stoppedBy ?? string.Empty;
    }
}
