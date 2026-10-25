using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Reports successful projection of a futures-session VWAP transition.</summary>
[MessagePackObject]
public sealed record FuturesVwapSignalUpdatedCompleteEvent : ICompleteEvent<FuturesVwapSignalEntityId>
{
    public const string Verb = "SignalUpdatedComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesVwapCheckpoint FuturesVwapCheckpoint { get; init; } = new();
    [Key(9)] public FuturesVwapSignalReadModel FuturesVwapSignal { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesVwapSignalUpdatedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
