using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>Confirms source-event durability, independently of projection/telemetry availability.</summary>
[MessagePackObject]
public sealed record MarketDataFeedRecoveryRecordStoredEvent : ICompleteEvent<MarketDataFeedRecoveryId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketDataFeedRecoveryId EntityId { get; init; } = new(Guid.Empty);
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "NativeRecovery";
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public Guid OperationId { get; init; }
    [Key(9)] public Guid RecordId { get; init; }
    [Key(10)] public string Digest { get; init; } = "";
    [Key(11)] public long Sequence { get; init; }
    [IgnoreMember] public string UserName => "NativeRecovery";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
