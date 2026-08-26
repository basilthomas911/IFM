using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

/// <summary>
/// Carries one non-durable immutable OHLCV observation to every configured bar-derived analytics actor.
/// </summary>
[MessagePackObject]
public sealed record FuturesAnalyticsObservationClosedRealtimeEvent
    : IEvent<FuturesAnalyticsObservationEntityId>
{
    /// <summary>Gets the realtime actor mailbox name.</summary>
    public const string Actor = "FuturesAnalyticsObservation";

    /// <summary>Gets the realtime event verb.</summary>
    public const string Verb = "Closed";

    /// <summary>Gets the actor-routing subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }

    /// <summary>Gets the unique event identity.</summary>
    [Key(1)] public Guid Id { get; init; }

    /// <summary>Gets the coordinator stream identity.</summary>
    [Key(2)] public FuturesAnalyticsObservationEntityId EntityId { get; init; }

    /// <summary>Gets the event-stream sequence when one is assigned.</summary>
    [Key(3)] public long EventId { get; init; }

    /// <summary>Gets the originating command identity when one is assigned.</summary>
    [Key(4)] public Guid CommandId { get; init; }

    /// <summary>Gets the formatted coordinator aggregate identity.</summary>
    [Key(5)] public string AggregateId { get; init; } = string.Empty;

    /// <summary>Gets the component that closed the observation.</summary>
    [Key(6)] public string EventSource { get; init; } = string.Empty;

    /// <summary>Gets the UTC time at which IFM received the event.</summary>
    [Key(7)] public DateTime ReceivedOn { get; init; }

    /// <summary>Gets the serialized event schema version.</summary>
    [Key(8)] public ushort SchemaVersion { get; init; } = 1;

    /// <summary>Gets the immutable closed OHLCV observation.</summary>
    [Key(9)] public FuturesAnalyticsObservationReadModel Observation { get; init; } = new();

    /// <summary>Gets the ambient user name; realtime market events are system-owned.</summary>
    [IgnoreMember] public string UserName => string.Empty;

    /// <summary>Gets the CLR event name.</summary>
    [IgnoreMember] public string EventName => nameof(FuturesAnalyticsObservationClosedRealtimeEvent);

    /// <summary>Gets the shared event-model classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>Validates a closed futures analytics observation realtime event.</summary>
public sealed class FuturesAnalyticsObservationClosedRealtimeEventValidationRules
    : BaseValidationRules, IValidationRules<FuturesAnalyticsObservationClosedRealtimeEvent>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied realtime event.</summary>
    /// <param name="value">Realtime event to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesAnalyticsObservationClosedRealtimeEvent value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesAnalyticsObservationClosedRealtimeEvent>
    {
        public Validator()
        {
            RuleFor(x => x.Subject).Must(x =>
                x.Is(ActorType.Realtime, FuturesAnalyticsObservationClosedRealtimeEvent.Actor,
                    FuturesAnalyticsObservationClosedRealtimeEvent.Verb));
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.EntityId)
                .Must(x => new FuturesAnalyticsObservationEntityIdValidationRules().Execute(x).Length == 0);
            RuleFor(x => x).Must(x => x.Subject.EntityId == x.EntityId.Format())
                .WithMessage("Subject entity identity must match EntityId.");
            RuleFor(x => x.AggregateId).NotEmpty();
            RuleFor(x => x).Must(x => x.AggregateId == x.EntityId.Format())
                .WithMessage("AggregateId must match EntityId.");
            RuleFor(x => x.EventSource).NotEmpty();
            RuleFor(x => x.ReceivedOn).Must(x => x.Kind == DateTimeKind.Utc);
            RuleFor(x => x.SchemaVersion).GreaterThan((ushort)0);
            RuleFor(x => x.Observation)
                .NotNull()
                .Must(x => new FuturesAnalyticsObservationReadModelValidationRules().Execute(x).Length == 0);
            RuleFor(x => x).Must(x =>
                    x.EntityId.MarketSeriesIdentity == x.Observation.MarketSeriesIdentity
                    && x.EntityId.TimeFrame == x.Observation.TimeFrame)
                .WithMessage("Observation identity must match the routed entity identity.");
        }
    }
}
