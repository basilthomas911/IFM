using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Validates a closed futures analytics observation realtime event.</summary>
public sealed class FuturesTradeSessionBarClosedRealtimeEventValidationRules
    : BaseValidationRules, IValidationRules<FuturesTradeSessionBarClosedRealtimeEvent>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied realtime event.</summary>
    /// <param name="value">Realtime event to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesTradeSessionBarClosedRealtimeEvent value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesTradeSessionBarClosedRealtimeEvent>
    {
        public Validator()
        {
            RuleFor(x => x.Subject).Must(x =>
                x.Is(ActorType.Realtime, FuturesTradeSessionBarClosedRealtimeEvent.Actor,
                    FuturesTradeSessionBarClosedRealtimeEvent.Verb));
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.EntityId)
                .Must(x => new FuturesTradeSessionBarEntityIdValidationRules().Execute(x).Length == 0);
            RuleFor(x => x).Must(x => string.Equals(x.Subject.EntityId,x.EntityId.Format(),StringComparison.Ordinal))
                .WithMessage("Subject entity identity must match EntityId.");
            RuleFor(x => x.AggregateId).NotEmpty();
            RuleFor(x => x).Must(x => string.Equals(x.AggregateId,x.EntityId.Format(),StringComparison.Ordinal))
                .WithMessage("AggregateId must match EntityId.");
            RuleFor(x => x.EventSource).NotEmpty();
            RuleFor(x => x.ReceivedOn).Must(x => x.Kind == DateTimeKind.Utc);
            RuleFor(x => x.SchemaVersion).GreaterThan((ushort)0);
            RuleFor(x => x.Observation)
                .NotNull()
                .Must(x => new FuturesTradeSessionBarReadModelValidationRules().Execute(x).Length == 0);
            RuleFor(x => x).Must(x =>
                    x.EntityId.MarketSeriesIdentity == x.Observation.MarketSeriesIdentity
                    && x.EntityId.TimeFrame == x.Observation.TimeFrame)
                .WithMessage("Observation identity must match the routed entity identity.");
        }
    }
}
