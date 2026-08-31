using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

/// <summary>Creates the public RSI signal produced by a Wilder state transition.</summary>
public static class FuturesRsiWilderSignalFactory
{
    /// <summary>Creates an RSI read model with shared observation lineage.</summary>
    public static FuturesRsiSignalReadModel Create(
        FuturesTradeSessionBarReadModel observation,
        int periodLength,
        FuturesRsiWilderResult result)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(result);
        var configurationId = periodLength switch
        {
            13 => FuturesRsiConfigurations.TdiRsi13,
            14 => FuturesRsiConfigurations.RegimeRsi14,
            _ => $"rsi-{periodLength}-wilder-v1"
        };
        return new(
            observation.ContractId,
            observation.ValueDate,
            observation.TimeFrame,
            periodLength,
            TimeOnly.FromDateTime(observation.IntervalEndUtc.UtcDateTime),
            observation.Close,
            result.PriceChange,
            result.PriceGain,
            result.PriceLoss,
            result.AverageGain ?? 0m,
            result.AverageLoss ?? 0m,
            result.RelativeStrength ?? 0d,
            result.Checkpoint.CurrentRsi ?? -1d,
            0d,
            result.Slope ?? 0d,
            observation.LastSourceSequence,
            observation.LastMarketEventUtc.UtcDateTime)
        {
            Metadata = MarketAnalyticsSignalMetadataFactory.Create(
                observation, MarketAnalyticsSignalKind.Rsi, configurationId, "rsi-wilder-v1") with
            {
                IsValid = result.IsWarm,
                ValidationIssues = result.IsWarm
                    ? []
                    : [MarketSignalValidationIssue.InvalidCalculation]
            },
            PreviousRsi = result.PreviousRsi,
            RegimeSlope = result.Slope,
            IsWarm = result.IsWarm
        };
    }
}
