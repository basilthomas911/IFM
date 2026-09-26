using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Internal asynchronous component update retained by the coordinator.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookComponentChangedRealtimeEvent : IEvent<MarketOutlookEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public MarketOutlookComponentChangedRealtimeEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="futuresRsiSignal">The FuturesRsiSignal field.</param>
    /// <param name="futuresTdiSignal">The FuturesTdiSignal field.</param>
    /// <param name="futuresItiSignal">The FuturesItiSignal field.</param>
    /// <param name="vixFuturesPrice">The VixFuturesPrice field.</param>
    /// <param name="futuresEmaSignal">The FuturesEmaSignal field.</param>
    /// <param name="futuresBbSignal">The FuturesBbSignal field.</param>
    /// <param name="futuresTradeSignal">The FuturesTradeSignal field.</param>
    /// <param name="futuresVwapSignal">The FuturesVwapSignal field.</param>
    /// <param name="futuresAdxSignal">The FuturesAdxSignal field.</param>
    /// <param name="futuresAtrSignal">The FuturesAtrSignal field.</param>
    /// <param name="futuresMacdSignal">The FuturesMacdSignal field.</param>
    [SerializationConstructor]
    public MarketOutlookComponentChangedRealtimeEvent(ActorSubject subject, Guid id, MarketOutlookEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, FuturesRsiSignalReadModel? futuresRsiSignal, FuturesTdiSignalReadModel? futuresTdiSignal, FuturesItiSignalV2ReadModel? futuresItiSignal, decimal vixFuturesPrice, FuturesEmaSignalReadModel? futuresEmaSignal, FuturesBbSignalReadModel? futuresBbSignal, FuturesTradeSignalV2ReadModel? futuresTradeSignal, FuturesVwapSignalReadModel? futuresVwapSignal, FuturesAdxSignalReadModel? futuresAdxSignal, FuturesAtrSignalReadModel? futuresAtrSignal, FuturesMacdSignalReadModel? futuresMacdSignal)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        FuturesRsiSignal = futuresRsiSignal;
        FuturesTdiSignal = futuresTdiSignal;
        FuturesItiSignal = futuresItiSignal;
        VixFuturesPrice = vixFuturesPrice;
        FuturesEmaSignal = futuresEmaSignal;
        FuturesBbSignal = futuresBbSignal;
        FuturesTradeSignal = futuresTradeSignal;
        FuturesVwapSignal = futuresVwapSignal;
        FuturesAdxSignal = futuresAdxSignal;
        FuturesAtrSignal = futuresAtrSignal;
        FuturesMacdSignal = futuresMacdSignal;
    }
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
    [Key(12)] public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }
    [Key(13)] public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }
    [Key(14)] public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    [Key(15)] public FuturesVwapSignalReadModel? FuturesVwapSignal { get; init; }
    [Key(16)] public FuturesAdxSignalReadModel? FuturesAdxSignal { get; init; }
    [Key(17)] public FuturesAtrSignalReadModel? FuturesAtrSignal { get; init; }
    [Key(18)] public FuturesMacdSignalReadModel? FuturesMacdSignal { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(MarketOutlookComponentChangedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
