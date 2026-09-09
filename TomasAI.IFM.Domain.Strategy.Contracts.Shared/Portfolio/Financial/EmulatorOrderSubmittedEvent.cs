using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Durable acknowledgement from the internal emulator; it is not a live broker acknowledgement.</summary>
[MessagePackObject]
public sealed record EmulatorOrderSubmittedEvent : ICompleteEvent<LedgerPortfolioId>, IFinancialCompletedEvent, IRequireDurableProjection
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime CommittedAtUtc { get; init; } = default;
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public EmulatorOrderReceipt Receipt { get; init; } = new(Guid.Empty,new(),string.Empty,0,default,Guid.Empty);
    [Key(12)] public long EventId { get; init; } = 0;
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = "EmulatorExecutionCommand";
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(EmulatorOrderSubmittedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => new("EmulatorExecutionCommandActor", "EmulatorExecutionProjector", EventProjectorStageType.ApplyProjection);
}
