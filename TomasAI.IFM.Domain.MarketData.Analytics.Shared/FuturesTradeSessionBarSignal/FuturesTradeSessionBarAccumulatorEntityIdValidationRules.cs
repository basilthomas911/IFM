using System.Globalization;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Validates a futures trade-session bar accumulator identity.</summary>
public sealed class FuturesTradeSessionBarAccumulatorEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesTradeSessionBarAccumulatorEntityId>
{
    static readonly Validator Rules = new();

    /// <inheritdoc />
    public ValidationError[] Execute(FuturesTradeSessionBarAccumulatorEntityId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesTradeSessionBarAccumulatorEntityId>
    {
        public Validator() => RuleFor(value => value.ValueDate)
            .Must(value => value != DateOnly.MinValue && value != DateOnly.MaxValue)
            .WithMessage("ValueDate is invalid.");
    }
}
