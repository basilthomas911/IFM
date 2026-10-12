using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Completed event produced when a futures option contract removal has been processed for denormalization.
/// Carries the removed contract id and metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractRemovedCompleteEvent : ICompleteEvent<FuturesOptionContractEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "RemovedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public string? ContractId { get; init; }
    [Key(9)] public DateTime DeletedOn { get; init; }
    [Key(10)] public string? DeletedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesOptionContractRemovedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractRemovedCompleteEvent(
        ActorSubject subject,
        FuturesOptionContractEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        string? contractId,
        DateTime deletedOn,
        string? deletedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ContractId = contractId;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy;
    }
}
