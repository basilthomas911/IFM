using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Carries one transient futures trade observation to the ATR realtime actor.</summary>
[MessagePackObject]
public sealed record FuturesAtrSignalSampledRealtimeEvent : IEvent<FuturesAtrSignalEntityId>
{
    public const string Actor = "FuturesAtrSignal";
    public const string Verb = "Sampled";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesAtrSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public decimal FuturesPrice { get; init; }
    [Key(9)] public long SourceSequence { get; init; }
    [Key(10)] public DateTime SourceEventTimestamp { get; init; }
    /// <summary>Gets the immutable shared observation that triggered this compatibility sample.</summary>
    [Key(11)] public FuturesTradeSessionBarReadModel? Observation { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesAtrSignalSampledRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
