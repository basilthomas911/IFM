using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

/// <summary>Reports failure of download-log event processing.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketDataDownloadLogInsertedFailEvent : IErrorEvent<DownloadLogId>
{
    public const string Actor = "DownloadLogEvent";
    public const string Verb = "InsertedFail";
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
    [Key(10)] public DateTime ErrorDate { get; init; } = default;
    [Key(11)] public int ErrorCode { get; init; } = 6050;
    [Key(12)] public string ErrorMessage { get; init; } = "";
    [Key(13)] public ErrorType ErrorType { get; init; } = ErrorType.Command;
    [Key(14)] public string ErrorData { get; init; } = "";
    [Key(15)] public string CommandName { get; init; } = "";
    [Key(16)] public string CommandData { get; init; } = "";
    [IgnoreMember] public string UserName => "MarketDataImport";
    [IgnoreMember] public string EventName => nameof(MarketDataDownloadLogInsertedFailEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
