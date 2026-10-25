using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

[MessagePackObject]
public sealed record TradeRealtimeSnapshotChangedEvent : IEvent<TradeEntityId>
{
    public const string Actor = "TradeRealtimeSnapshot";
    public const string Verb = "Changed";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TradeEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = Actor;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public TradeRealtimeSnapshot Snapshot { get; init; } = new();
    [IgnoreMember] public string UserName => "TradeMonitor";
    [IgnoreMember] public string EventName => nameof(TradeRealtimeSnapshotChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public static TradeRealtimeSnapshotChangedEvent From(TradeRealtimeSnapshot snapshot)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Subject = new(ActorType.Notify, Actor, Verb, snapshot.TradeId.Format()),
            EntityId = snapshot.TradeId, Id = id, CommandId = id, EventId = snapshot.SnapshotRevision,
            AggregateId = snapshot.TradeId.Format(), ReceivedOn = snapshot.PublishedAtUtc.UtcDateTime, Snapshot = snapshot
        };
    }
}
