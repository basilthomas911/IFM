using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Reports successful persistence of a published futures trade-session bar.</summary>
[MessagePackObject]
public sealed record FuturesTradeSessionBarPublishedCompleteEvent
    : ICompleteEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the completion verb.</summary>
    public const string Verb = "PublishedComplete";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(2)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the persisted completed bar.</summary>
    [Key(8)] public FuturesTradeSessionBarReadModel Bar { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesTradeSessionBarPublishedCompleteEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
