using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Reports one completed data load without carrying provider records.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesAnalyticsHistoricalDataLoaderCompletedEvent
    : IEvent<FuturesAnalyticsHistoricalDataLoaderEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesAnalyticsHistoricalDataLoaderCompletedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="manifestId">The ManifestId field.</param>
    /// <param name="validSessionCount">The ValidSessionCount field.</param>
    /// <param name="gapCount">The GapCount field.</param>
    /// <param name="rollCount">The RollCount field.</param>
    /// <param name="requestSha256">The RequestSha256 field.</param>
    /// <param name="completedAtUtc">The CompletedAtUtc field.</param>
    [SerializationConstructor]
    public FuturesAnalyticsHistoricalDataLoaderCompletedEvent(ActorSubject subject, Guid id, FuturesAnalyticsHistoricalDataLoaderEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, Guid manifestId, int validSessionCount, int gapCount, int rollCount, string requestSha256, DateTime completedAtUtc)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        ManifestId = manifestId;
        ValidSessionCount = validSessionCount;
        GapCount = gapCount;
        RollCount = rollCount;
        RequestSha256 = requestSha256;
        CompletedAtUtc = completedAtUtc;
    }
    /// <summary>Gets the Event actor name.</summary>
    public const string Actor = FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Completed";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
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
    /// <summary>Gets the immutable provider manifest identity.</summary>
    [Key(8)] public Guid ManifestId { get; init; }
    /// <summary>Gets the count of valid Daily sessions.</summary>
    [Key(9)] public int ValidSessionCount { get; init; }
    /// <summary>Gets the audited gap count.</summary>
    [Key(10)] public int GapCount { get; init; }
    /// <summary>Gets the audited roll count.</summary>
    [Key(11)] public int RollCount { get; init; }
    /// <summary>Gets the stable request hash.</summary>
    [Key(12)] public string RequestSha256 { get; init; } = string.Empty;
    /// <summary>Gets the UTC completion time.</summary>
    [Key(13)] public DateTime CompletedAtUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesAnalyticsHistoricalDataLoaderCompletedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
