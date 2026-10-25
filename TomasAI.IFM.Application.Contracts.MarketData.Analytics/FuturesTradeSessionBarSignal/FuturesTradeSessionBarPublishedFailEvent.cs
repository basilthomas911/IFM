using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Reports failure to project a published futures trade-session bar.</summary>
[MessagePackObject]
public sealed record FuturesTradeSessionBarPublishedFailEvent
    : IErrorEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the failure verb.</summary>
    public const string Verb = "PublishedFail";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(2)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(3)] public DateTime ErrorDate { get; init; }
    /// <inheritdoc />
    [Key(4)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(5)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(8)] public int ErrorCode { get; init; }
    /// <inheritdoc />
    [Key(9)] public ErrorType ErrorType { get; init; }
    /// <inheritdoc />
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(11)] public DateTime ReceivedOn { get; init; }
    /// <inheritdoc />
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesTradeSessionBarPublishedFailEvent);
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
