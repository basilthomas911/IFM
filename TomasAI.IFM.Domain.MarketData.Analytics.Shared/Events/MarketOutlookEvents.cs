using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Internal asynchronous component update retained by the coordinator.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookComponentChangedRealtimeEvent : IEvent<MarketOutlookEntityId>
{
    [IgnoreMember] public const string Actor = "MarketOutlook";
    [IgnoreMember] public const string Verb = "ComponentChanged";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }
    [Key(9)] public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }
    [Key(10)] public FuturesItiSignalV2ReadModel? FuturesItiSignal { get; init; }
    [Key(11)] public decimal VixFuturesPrice { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(MarketOutlookComponentChangedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>Internal EOD clock event that requests one composite publication.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookEodUpdatedRealtimeEvent : IEvent<MarketOutlookEntityId>
{
    [IgnoreMember] public const string Actor = "MarketOutlook";
    [IgnoreMember] public const string Verb = "EodUpdated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; } = new();

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(MarketOutlookEodUpdatedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>Only frontend notification used to refresh the complete Market Outlook.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookUpdatedNotifyEvent : IEvent<MarketOutlookEntityId>
{
    [IgnoreMember] public const string Actor = "MarketOutlookNotification";
    [IgnoreMember] public const string Verb = "Updated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public MarketOutlookSnapshotReadModel MarketOutlook { get; init; } = new();

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(MarketOutlookUpdatedNotifyEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    [IgnoreMember] public bool IsValid => CommandId != Guid.Empty && MarketOutlook.IsValid;
}
