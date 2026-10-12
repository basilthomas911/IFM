using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Event published when a market data feed has been reset successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record MarketDataFeedResetCompleteEvent : ICompleteEvent<MarketDataFeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "ResetComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketDataFeedId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(8)] public FuturesContractV3ReadModel[] FuturesContracts { get; init; }
    [Key(9)] public DateOnly ValueDate { get; init; }
    [Key(10)] public DateTime ResetOn { get; init; }
    [Key(11)] public string ResetBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public MarketDataFeedResetCompleteEvent() { }

    [SerializationConstructor]
    public MarketDataFeedResetCompleteEvent(
        ActorSubject subject,
        MarketDataFeedId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractV3ReadModel[] futuresContracts,
        DateOnly valueDate,
        DateTime resetOn,
        string resetBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesContracts = futuresContracts ?? throw new ArgumentNullException(nameof(futuresContracts));
        ValueDate = valueDate;
        ResetOn = resetOn;
        ResetBy = resetBy ?? string.Empty;
    }
}
