using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record BrokerAccountingIntentCapturedEvent : IEvent
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = "FinancialOperations";
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public int PortfolioId { get; init; }
    [Key(8)] public Guid OperationId { get; init; }
    [Key(9)] public string EvidenceHash { get; init; } = string.Empty;
    [Key(10)] public short PayloadFormat { get; init; }
    [Key(11)] public int PayloadVersion { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(12)] public byte[] CommandBytes { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(13)] public byte[] CommandSha256 { get; init; } = [];
    [Key(14)] public int SchemaVersion { get; init; } = 1;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(BrokerAccountingIntentCapturedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
