using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Actor;

/// <summary>
/// Carries the server clock through the observation actor mailbox so interval closure is serialized with trades.
/// </summary>
public sealed record FuturesAnalyticsObservationBarrierRealtimeEvent : IEvent<ActorEntityId>
{
    /// <summary>Gets the private actor verb.</summary>
    internal const string Verb = "Barrier";

    /// <summary>Gets the fixed private clock identity.</summary>
    internal static readonly ActorEntityId ClockEntityId = new("server-clock");

    /// <inheritdoc />
    public ActorSubject Subject { get; init; }

    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <inheritdoc />
    public ActorEntityId EntityId { get; init; }

    /// <inheritdoc />
    public long EventId { get; init; }

    /// <inheritdoc />
    public Guid CommandId { get; init; }

    /// <inheritdoc />
    public string AggregateId { get; init; } = string.Empty;

    /// <inheritdoc />
    public string EventSource { get; init; } = string.Empty;

    /// <inheritdoc />
    public DateTime ReceivedOn { get; init; }

    /// <inheritdoc />
    public ushort SchemaVersion { get; init; } = 1;

    /// <summary>Gets the exclusive UTC interval-close barrier.</summary>
    public DateTimeOffset BarrierUtc { get; init; }

    /// <inheritdoc />
    public string UserName => string.Empty;

    /// <inheritdoc />
    public string EventName => nameof(FuturesAnalyticsObservationBarrierRealtimeEvent);

    /// <inheritdoc />
    public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates one private server-owned clock event.</summary>
    internal static FuturesAnalyticsObservationBarrierRealtimeEvent Create(DateTimeOffset barrierUtc) => new()
    {
        Subject = new ActorSubject(
            ActorType.Realtime,
            FuturesAnalyticsObservationRealtimeActor.ActorName,
            Verb,
            ClockEntityId.Format()),
        Id = Guid.NewGuid(),
        EntityId = ClockEntityId,
        AggregateId = ClockEntityId.Format(),
        EventSource = nameof(FuturesAnalyticsObservationRealtimeActor),
        ReceivedOn = barrierUtc.UtcDateTime,
        BarrierUtc = barrierUtc.ToUniversalTime()
    };
}
