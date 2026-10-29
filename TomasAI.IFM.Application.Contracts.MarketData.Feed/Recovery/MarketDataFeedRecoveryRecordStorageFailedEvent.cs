using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>A delivery/storage failure, never a dataset recovery outcome. Retrying remains permitted.</summary>
[MessagePackObject]
public sealed record MarketDataFeedRecoveryRecordStorageFailedEvent : IErrorEvent<MarketDataFeedRecoveryId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketDataFeedRecoveryId EntityId { get; init; } = new(Guid.Empty);
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "NativeRecovery";
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime ErrorDate { get; init; }
    [Key(9)] public int ErrorCode { get; init; } = 5090;
    [Key(10)] public string ErrorMessage { get; init; } = "";
    [Key(11)] public ErrorType ErrorType { get; init; } = ErrorType.Command;
    [Key(12)] public string ErrorData { get; init; } = "";
    [Key(13)] public string CommandName { get; init; } = "RecordMarketDataFeedRecovery";
    [Key(14)] public string CommandData { get; init; } = "";
    [IgnoreMember] public string UserName => "NativeRecovery";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
