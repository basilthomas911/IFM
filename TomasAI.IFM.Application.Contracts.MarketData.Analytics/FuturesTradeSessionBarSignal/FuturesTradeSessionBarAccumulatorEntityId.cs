using System.Globalization;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>
/// Identifies a trade-session bar mailbox by value date and resolved market series.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesTradeSessionBarAccumulatorEntityId : IActorEntityId
{
    /// <summary>Gets the futures trading value date owned by the accumulator.</summary>
    [Key(0)] public DateOnly ValueDate { get; init; }

    /// <summary>Gets the canonical series key. Empty identifies a legacy date-only route.</summary>
    [Key(1)] public string? SeriesKey { get; init; }

    /// <summary>Initializes the accumulator identity for a trading date and series.</summary>
    [SerializationConstructor]
    public FuturesTradeSessionBarAccumulatorEntityId(DateOnly valueDate, string? seriesKey = null)
        => (ValueDate, SeriesKey) = (valueDate, seriesKey);

    /// <summary>Formats the stable actor scheduling identity.</summary>
    public string Format()
    {
        var date = ValueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return SeriesKey is null ? date : $"{date}|{Uri.EscapeDataString(SeriesKey)}";
    }

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
        var parts = value?.Split('|', 2);
        if (parts is null || !DateOnly.TryParseExact(
                parts[0],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var valueDate))
            return false;
        var seriesKey = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : null;
        if (seriesKey is not null && !Common.MarketSeriesIdentity.TryParse(seriesKey, out _))
            return false;
        entityId = new(valueDate, seriesKey);
        return new FuturesTradeSessionBarAccumulatorEntityIdValidationRules().Execute(entityId).Length == 0;
    }

    /// <inheritdoc />
    public override string ToString() => Format();
}
