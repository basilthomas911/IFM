using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>Captures all catalog rows and the pointer changed by an atomic rollover replacement.</summary>
[MessagePackObject]
public sealed record FuturesContractRolloverSetReplacedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public FuturesContractRolloverReadModel Rollover { get; init; } = null!;
    [Key(8)] public FuturesContractV3ReadModel[] ChangedContracts { get; init; } = [];
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesContractRolloverSetReplacedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
