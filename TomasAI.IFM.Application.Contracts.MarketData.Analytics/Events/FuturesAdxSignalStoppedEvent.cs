using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesAdxSignalStoppedEvent : IEvent<FuturesAdxSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesAdxSignalEvent";
    [IgnoreMember] public const string Verb = "Stopped";
    [IgnoreMember] public const int ErrorCode = 19004;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesAdxSignalEntityId EntityId { get; init; } = default!;
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime StoppedOn { get; init; }
    [Key(9)] public string StoppedBy { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
