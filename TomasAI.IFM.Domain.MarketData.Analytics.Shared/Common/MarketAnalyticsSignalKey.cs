using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>
/// Identifies one configured signal stream without conflating contracts and continuation series.
/// </summary>
[MessagePackObject]
public readonly record struct MarketAnalyticsSignalKey
{
    /// <summary>Gets the market series being calculated.</summary>
    [Key(0)]
    public MarketSeriesIdentity MarketSeriesIdentity { get; init; }

    /// <summary>Gets the signal calculation family.</summary>
    [Key(1)]
    public MarketAnalyticsSignalKind SignalKind { get; init; }

    /// <summary>Gets the signal timeframe.</summary>
    [Key(2)]
    public TimeFrameType TimeFrame { get; init; }

    /// <summary>Gets the immutable calculation-configuration identity.</summary>
    [Key(3)]
    public string CalculationConfigurationId { get; init; }

    /// <summary>Initializes an empty value for serialization.</summary>
    public MarketAnalyticsSignalKey() => CalculationConfigurationId = string.Empty;

    /// <summary>Initializes a configured analytics signal key.</summary>
    /// <param name="marketSeriesIdentity">Specific-contract or continuation identity.</param>
    /// <param name="signalKind">Signal calculation family.</param>
    /// <param name="timeFrame">Signal timeframe.</param>
    /// <param name="calculationConfigurationId">Immutable configuration identity.</param>
    [SerializationConstructor]
    public MarketAnalyticsSignalKey(
        MarketSeriesIdentity marketSeriesIdentity,
        MarketAnalyticsSignalKind signalKind,
        TimeFrameType timeFrame,
        string calculationConfigurationId)
    {
        MarketSeriesIdentity = marketSeriesIdentity;
        SignalKind = signalKind;
        TimeFrame = timeFrame;
        CalculationConfigurationId = calculationConfigurationId ?? string.Empty;
    }

    /// <summary>Formats the configured signal key.</summary>
    /// <returns>A stable identity string.</returns>
    public string Format() =>
        $"{Uri.EscapeDataString(MarketSeriesIdentity.Format())}|{SignalKind}|{TimeFrame}|{Uri.EscapeDataString(CalculationConfigurationId)}";

    /// <summary>Returns the stable formatted key.</summary>
    public override string ToString() => Format();
}
