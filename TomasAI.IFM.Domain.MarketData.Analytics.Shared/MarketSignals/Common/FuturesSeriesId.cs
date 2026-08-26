using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

/// <summary>
/// Identifies one roll-aware futures continuation series independently of any provider symbol.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesSeriesId
{
    /// <summary>Gets the canonical futures root symbol.</summary>
    [Key(0)]
    public string RootSymbol { get; init; }

    /// <summary>Gets the rule that selects the active contract during a roll.</summary>
    [Key(1)]
    public string RollRuleId { get; init; }

    /// <summary>Gets the rule used to adjust prices across contract boundaries.</summary>
    [Key(2)]
    public string AdjustmentRuleId { get; init; }

    /// <summary>Gets the revision of the continuation definition.</summary>
    [Key(3)]
    public ushort Revision { get; init; }

    /// <summary>Initializes an empty value for serialization.</summary>
    public FuturesSeriesId()
    {
        RootSymbol = string.Empty;
        RollRuleId = string.Empty;
        AdjustmentRuleId = string.Empty;
    }

    /// <summary>Initializes a roll-aware futures continuation identity.</summary>
    /// <param name="rootSymbol">Canonical futures root symbol.</param>
    /// <param name="rollRuleId">Active-contract selection rule.</param>
    /// <param name="adjustmentRuleId">Cross-contract price-adjustment rule.</param>
    /// <param name="revision">Continuation-definition revision.</param>
    [SerializationConstructor]
    public FuturesSeriesId(
        string rootSymbol,
        string rollRuleId,
        string adjustmentRuleId,
        ushort revision)
    {
        RootSymbol = rootSymbol ?? string.Empty;
        RollRuleId = rollRuleId ?? string.Empty;
        AdjustmentRuleId = adjustmentRuleId ?? string.Empty;
        Revision = revision;
    }

    /// <summary>Formats the provider-neutral continuation identity.</summary>
    /// <returns>A stable, escaped identity string.</returns>
    public string Format() => string.Join(
        '|',
        Uri.EscapeDataString(RootSymbol),
        Uri.EscapeDataString(RollRuleId),
        Uri.EscapeDataString(AdjustmentRuleId),
        Revision);

    /// <summary>Parses a formatted continuation identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <returns>The parsed continuation identity.</returns>
    /// <exception cref="FormatException">Thrown when the value is malformed.</exception>
    public static FuturesSeriesId Parse(string value) =>
        TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException("FuturesSeriesId is malformed.");

    /// <summary>Attempts to parse a formatted continuation identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <param name="seriesId">Receives the parsed identity.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out FuturesSeriesId seriesId)
    {
        seriesId = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('|');
        if (parts.Length != 4 || !ushort.TryParse(parts[3], out var revision)) return false;
        try
        {
            seriesId = new(
                Uri.UnescapeDataString(parts[0]),
                Uri.UnescapeDataString(parts[1]),
                Uri.UnescapeDataString(parts[2]),
                revision);
            return new FuturesSeriesIdValidationRules().Execute(seriesId).Length == 0;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>Returns the stable formatted identity.</summary>
    public override string ToString() => Format();
}

/// <summary>Validates a roll-aware futures continuation identity.</summary>
public sealed class FuturesSeriesIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesSeriesId>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied continuation identity.</summary>
    /// <param name="value">Identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesSeriesId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesSeriesId>
    {
        public Validator()
        {
            RuleFor(x => x.RootSymbol).NotEmpty();
            RuleFor(x => x.RollRuleId).NotEmpty();
            RuleFor(x => x.AdjustmentRuleId).NotEmpty();
            RuleFor(x => x.Revision).GreaterThan((ushort)0);
        }
    }
}
