using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>
/// Identifies the coordinator stream that closes observations for one market series and timeframe.
/// </summary>
[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct FuturesTradeSessionBarEntityId : IActorEntityId
{
    /// <summary>Gets the specific-contract or continuation identity.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }

    /// <summary>Gets the observation timeframe.</summary>
    [Key(1)] public TimeFrameType TimeFrame { get; init; }

    /// <summary>Initializes a coordinator stream identity.</summary>
    /// <param name="marketSeriesIdentity">Specific-contract or continuation identity.</param>
    /// <param name="timeFrame">Observation timeframe.</param>
    [SerializationConstructor]
    public FuturesTradeSessionBarEntityId(
        MarketSeriesIdentity marketSeriesIdentity,
        TimeFrameType timeFrame)
    {
        MarketSeriesIdentity = marketSeriesIdentity;
        TimeFrame = timeFrame;
    }

    /// <summary>Formats the stable actor-routing identity.</summary>
    /// <returns>An escaped market-series identity followed by its timeframe.</returns>
    public string Format() =>
        $"{Uri.EscapeDataString(MarketSeriesIdentity.Format())}|{TimeFrame}";

    /// <summary>Parses a formatted actor-routing identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <returns>The parsed entity identity.</returns>
    /// <exception cref="FormatException">Thrown when the value is malformed.</exception>
    public static FuturesTradeSessionBarEntityId Parse(string value) =>
        TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException("FuturesTradeSessionBarEntityId is malformed.");

    /// <summary>Attempts to parse a formatted actor-routing identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <param name="entityId">Receives the parsed identity.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out FuturesTradeSessionBarEntityId entityId)
    {
        entityId = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var separator = value.LastIndexOf('|');
        if (separator <= 0
            || !Enum.TryParse<TimeFrameType>(value[(separator + 1)..], out var timeFrame)
            || !MarketSeriesIdentity.TryParse(
                Uri.UnescapeDataString(value[..separator]),
                out var seriesIdentity))
            return false;
        entityId = new(seriesIdentity, timeFrame);
        return new FuturesTradeSessionBarEntityIdValidationRules().Execute(entityId).Length == 0;
    }

    /// <summary>Returns the stable actor-routing identity.</summary>
    public override string ToString() => Format();
}
