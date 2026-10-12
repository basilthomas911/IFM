using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Verified migration facts for financial history whose original events omitted posting facts.</summary>
[MessagePackObject]
public sealed record FinancialProjectionBaselineEvent : IEvent
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = "FinancialProjectionBaseline";
    [Key(5)] public string EventSource { get; init; } = "FinancialProjectionBaseline";
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public long SourceCut { get; init; }
    [Key(8)] public int FinancialSchemaVersion { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(9)] public FinancialBaselineTable[] Tables { get; init; } = [];
    [Key(10)] public int SchemaVersion { get; init; } = 1;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(FinancialProjectionBaselineEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
