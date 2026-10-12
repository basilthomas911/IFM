using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Event published when a futures contract add operation has been completed successfully.
/// Carries the added contract and metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractAddedCompleteEvent : ICompleteEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "AddedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesContractId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesContractV3ReadModel? Contract { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string? CreatedBy { get; init; }

    [IgnoreMember]
    public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [IgnoreMember]
    public string EventName => GetType().Name;

    [IgnoreMember]
    public EventType EventType => EventType.DomainEvent;

    public FuturesContractAddedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractAddedCompleteEvent(
        ActorSubject subject,
        FuturesContractId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractV3ReadModel? contract,
        DateTime createdOn,
        string? createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        Contract = contract;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

}
