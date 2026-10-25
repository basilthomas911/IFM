using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

/// <summary>Reports completion of download-log event processing.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketDataDownloadLogInsertedCompleteEvent : ICompleteEvent<DownloadLogId>
{
    public const string Actor = "DownloadLogEvent";
    public const string Verb = "InsertedComplete";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public DownloadLogId EntityId { get; init; } = default!;
    [Key(3)] public long EventId { get; init; } = default;
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "";
    [Key(7)] public DateTime ReceivedOn { get; init; } = default;
    [Key(8)] public MarketDataDownloadOutcome Outcome { get; init; } = default!;
    [Key(9)] public string PayloadSha256 { get; init; } = "";
    [IgnoreMember] public string UserName => "MarketDataImport";
    [IgnoreMember] public string EventName => nameof(MarketDataDownloadLogInsertedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
