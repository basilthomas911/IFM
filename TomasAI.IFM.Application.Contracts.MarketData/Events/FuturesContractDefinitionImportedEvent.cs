using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>Records the futures row accepted from the published definition catalog.</summary>
[MessagePackObject]
public sealed record FuturesContractDefinitionImportedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public FuturesContractV3ReadModel Contract { get; init; } = null!;
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesContractDefinitionImportedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
