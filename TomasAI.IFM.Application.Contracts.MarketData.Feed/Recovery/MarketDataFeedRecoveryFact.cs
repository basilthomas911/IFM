using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>Stored source fact. Payload is preserved byte-for-byte for conflict detection and replay.</summary>
[Union(0,typeof(MarketDataFeedRecoveryStartedEvent))]
[Union(1,typeof(MarketDataFeedDatasetResetStartedEvent))]
[Union(2,typeof(MarketDataFeedDatasetResetCompletedEvent))]
[Union(3,typeof(MarketDataFeedConnectionRestoredEvent))]
[Union(4,typeof(MarketDataFeedRecoveryCompletedEvent))]
[Union(5,typeof(MarketDataFeedRecoveryFailedEvent))]
[Union(6,typeof(MarketDataFeedMonitoringRestoredEvent))]
[Union(7,typeof(MarketDataFeedRecoveryInterruptedEvent))]
[MessagePackObject]
public abstract record MarketDataFeedRecoveryFact : IEvent<MarketDataFeedRecoveryId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketDataFeedRecoveryId EntityId { get; init; } = new(Guid.Empty);
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "NativeRecovery";
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public string Payload { get; init; } = "";
    [Key(9)] public string Digest { get; init; } = "";
    [IgnoreMember] public string UserName => "NativeRecovery";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
