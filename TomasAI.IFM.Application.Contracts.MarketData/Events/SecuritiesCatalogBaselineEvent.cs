using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>Records how the two Securities catalogs were initialized from verified PostgreSQL rows.</summary>
[MessagePackObject]
public sealed record SecuritiesCatalogBaselineEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public long LegacyThroughEventId { get; init; }
    [Key(8)] public FuturesContractV3ReadModel[] FuturesWithoutHistory { get; init; } = [];
    [Key(9)] public FuturesOptionContractReadModel[] OptionsWithoutHistory { get; init; } = [];
    [Key(10)] public FuturesContractRolloverReadModel[] RolloverPointers { get; init; } = [];
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(SecuritiesCatalogBaselineEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
