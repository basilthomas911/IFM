using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Event published when a Futures ITI signal has been generated successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalGeneratedCompleteEvent : ICompleteEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "GeneratedComplete";
    [IgnoreMember] public const int ErrorCode = 19002;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesItiSignalV2ReadModel? FuturesItiSignal { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }
    /// <summary>The source VIX futures price used to generate this signal.</summary>
    [Key(11)] public double VixFuturesPrice { get; init; }
    /// <summary>Deprecated compatibility field retained at its established MessagePack key.</summary>
    [Key(12)] public bool DeriveLongerPeriods { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public FuturesItiSignalGeneratedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesItiSignalGeneratedCompleteEvent(
        ActorSubject subject,
        FuturesItiSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesItiSignalV2ReadModel? futuresItiSignal,
        DateTime createdOn,
        string createdBy,
        double vixFuturesPrice,
        bool deriveLongerPeriods)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesItiSignal = futuresItiSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        VixFuturesPrice = vixFuturesPrice;
        DeriveLongerPeriods = deriveLongerPeriods;
    }
}
