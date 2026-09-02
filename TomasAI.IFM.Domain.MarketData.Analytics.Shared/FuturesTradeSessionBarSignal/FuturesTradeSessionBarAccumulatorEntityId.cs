using System.Globalization;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>
/// Identifies the single ephemeral trade-session bar accumulator for one futures value date.
/// Every contract and the server-clock barrier for the same value date share this identity.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesTradeSessionBarAccumulatorEntityId : IActorEntityId
{
    /// <summary>Gets the futures trading value date owned by the accumulator.</summary>
    [Key(0)] public DateOnly ValueDate { get; init; }

    /// <summary>Initializes the accumulator identity for one futures trading value date.</summary>
    [SerializationConstructor]
    public FuturesTradeSessionBarAccumulatorEntityId(DateOnly valueDate)
        => ValueDate = valueDate;

    /// <summary>Formats the stable actor scheduling identity.</summary>
    public string Format() => ValueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Parses a formatted accumulator identity.</summary>
    public static FuturesTradeSessionBarAccumulatorEntityId Parse(string value) =>
        TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException("FuturesTradeSessionBarAccumulatorEntityId is malformed.");

    /// <summary>Attempts to parse a formatted accumulator identity.</summary>
    public static bool TryParse(
        string? value,
        out FuturesTradeSessionBarAccumulatorEntityId entityId)
    {
        entityId = default;
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var valueDate))
            return false;
        entityId = new(valueDate);
        return new FuturesTradeSessionBarAccumulatorEntityIdValidationRules().Execute(entityId).Length == 0;
    }

    /// <inheritdoc />
    public override string ToString() => Format();
}

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
