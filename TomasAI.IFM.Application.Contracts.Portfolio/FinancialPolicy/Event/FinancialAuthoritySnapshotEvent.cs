using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Accepted authority effects of a Portfolio/Fund source event, appended in the same transaction.</summary>
[MessagePackObject]
public sealed record FinancialAuthoritySnapshotEvent : IEvent
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = "PortfolioAuthorityFence";
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public int PortfolioId { get; init; }
    [Key(8)] public Guid SourceEventId { get; init; }
    [Key(9)] public FinancialBookConfiguration Book { get; init; } = new();
    [Key(10)] public long PreviousFinancialRevision { get; init; }
    [Key(11)] public long FinancialRevision { get; init; }
    [Key(12)] public long AuthorityEpoch { get; init; }
    [Key(13)] public string OperatingState { get; init; } = string.Empty;
    [Key(14)] public string MigrationState { get; init; } = string.Empty;
    [Key(15)] public int SchemaVersion { get; init; } = 1;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(FinancialAuthoritySnapshotEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
