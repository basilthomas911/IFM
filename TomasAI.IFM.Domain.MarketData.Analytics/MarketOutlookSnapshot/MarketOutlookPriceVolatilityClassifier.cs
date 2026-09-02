using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;

/// <summary>
/// Classifies the direction of the currently traded VX futures price relative to its
/// value-date session open. It does not infer a value when either input is unavailable.
/// </summary>
internal static class MarketOutlookPriceVolatilityClassifier
{
    internal static PriceVolatilityType Classify(
        decimal? sessionOpenPrice,
        decimal? currentPrice)
    {
        if (sessionOpenPrice is not > 0m || currentPrice is not > 0m)
            return PriceVolatilityType.Unknown;
        return currentPrice.Value.CompareTo(sessionOpenPrice.Value) switch
        {
            > 0 => PriceVolatilityType.Rising,
            < 0 => PriceVolatilityType.Falling,
            _ => PriceVolatilityType.Flat
        };
    }
}
