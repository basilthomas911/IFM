using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;

/// <summary>
/// Carries the server clock through the publisher mailbox so interval closure is serialized with trades.
/// </summary>
public sealed record FuturesTradeSessionBarSignalBarrierRealtimeEvent
    : IEvent<FuturesTradeSessionBarAccumulatorEntityId>
{
    /// <summary>Gets the private actor verb.</summary>
    internal const string Verb = "Barrier";

    /// <inheritdoc />
    public ActorSubject Subject { get; init; }

    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <inheritdoc />
    public FuturesTradeSessionBarAccumulatorEntityId EntityId { get; init; }

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
    public string EventName => nameof(FuturesTradeSessionBarSignalBarrierRealtimeEvent);

    /// <inheritdoc />
    public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates one private server-owned clock event.</summary>
    internal static FuturesTradeSessionBarSignalBarrierRealtimeEvent Create(
        DateTimeOffset barrierUtc,
        FuturesTradeSessionBarAccumulatorEntityId entityId) => new()
    {
        Subject = new ActorSubject(
            ActorType.Realtime,
            FuturesTradeSessionBarSignalRealtimeActor.ActorName,
            Verb,
            entityId.Format()),
        Id = Guid.NewGuid(),
        EntityId = entityId,
        AggregateId = entityId.Format(),
        EventSource = nameof(FuturesTradeSessionBarSignalRealtimeActor),
        ReceivedOn = barrierUtc.UtcDateTime,
        BarrierUtc = barrierUtc.ToUniversalTime()
    };
}
