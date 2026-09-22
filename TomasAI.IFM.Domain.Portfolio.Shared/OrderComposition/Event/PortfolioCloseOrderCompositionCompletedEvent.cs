using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
public sealed record PortfolioCloseOrderCompositionCompletedEvent :
    ICompleteEvent<FinancialExecutionId>, IFinancialCompletedEvent
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public Guid OperationId { get; init; }
    [Key(6)] public int PortfolioId { get; init; }
    [Key(7)] public Guid CorrelationId { get; init; }
    [Key(8)] public Guid CausationId { get; init; }
    [Key(9)] public DateTime CommittedAtUtc { get; init; }
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public PortfolioCloseOrderCompositionReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; }
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = EvaluatePortfolioCloseOrderCompositionCommand.Actor;
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "Portfolio";
    [IgnoreMember] public string EventName => nameof(PortfolioCloseOrderCompositionCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
