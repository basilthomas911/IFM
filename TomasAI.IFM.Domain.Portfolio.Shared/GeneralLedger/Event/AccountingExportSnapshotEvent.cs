using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record AccountingExportSnapshotEvent : IEvent
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = "FinancialOperations";
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public int PortfolioId { get; init; }
    [Key(8)] public AccountingExportRequest? PreparedRequest { get; init; }
    [Key(9)] public AccountingExportReceipt Receipt { get; init; } = null!;
    [Key(10)] public Guid? AttemptId { get; init; }
    [Key(11)] public string Outcome { get; init; } = string.Empty;
    [Key(12)] public int SchemaVersion { get; init; } = 1;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(AccountingExportSnapshotEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
