using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

/// <summary>Persists the accepted limit calculation and its financial provenance; projection populates the legacy tables.</summary>
[MessagePackObject]
public sealed record IronCondorMonitoringInitializedEvent : IEvent<StrategyPositionId>
{
    public const string Verb = "IronCondorMonitoringInitialized";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public StrategyPositionId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public IronCondorMonitoringInitialization MonitoringInitialization { get; init; } = new();
    [Key(9)] public TradeLimitReadModel TradeLimits { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(10)] public TradeTypeLimitReadModel[] SpreadLimits { get; init; } = [];
    [IgnoreMember] public string UserName => "IronCondorMonitoring";
    [IgnoreMember] public string EventName => nameof(IronCondorMonitoringInitializedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
