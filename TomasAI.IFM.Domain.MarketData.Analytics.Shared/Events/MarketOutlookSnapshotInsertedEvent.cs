using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Full replacement snapshot notification. The current realtime path publishes this event immediately;
/// latest-row persistence proceeds independently for restart hydration.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookSnapshotInsertedEvent : IEvent<MarketOutlookEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public MarketOutlookSnapshotInsertedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="marketOutlook">The MarketOutlook field.</param>
    [SerializationConstructor]
    public MarketOutlookSnapshotInsertedEvent(ActorSubject subject, Guid id, MarketOutlookEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, MarketOutlookReadModel marketOutlook)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        MarketOutlook = marketOutlook;
    }
    [IgnoreMember] public const string Actor = "MarketOutlook";
    [IgnoreMember] public const string Verb = "SnapshotInserted";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public MarketOutlookReadModel MarketOutlook { get; init; } = new();

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(MarketOutlookSnapshotInsertedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    [IgnoreMember] public bool IsValid
    {
        get
        {
            var eod = MarketOutlook.FuturesEodData;
            return CommandId != Guid.Empty
                && EntityId.ContractId == MarketOutlook.ContractId
                && EntityId.ValueDate == MarketOutlook.ValueDate
                && string.Equals(eod.Symbol, "ES", StringComparison.OrdinalIgnoreCase)
                && eod.ContractId == MarketOutlook.ContractId
                && eod.ValueDate == MarketOutlook.ValueDate
                && eod.OpenPrice > 0m
                && eod.HighPrice > 0m
                && eod.LowPrice > 0m
                && eod.ClosePrice > 0m
                && eod.HighPrice >= eod.LowPrice
                && eod.OpenPrice >= eod.LowPrice
                && eod.OpenPrice <= eod.HighPrice
                && eod.ClosePrice >= eod.LowPrice
                && eod.ClosePrice <= eod.HighPrice;
        }
    }
}
