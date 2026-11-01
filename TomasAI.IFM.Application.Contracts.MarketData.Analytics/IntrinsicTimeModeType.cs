
namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
    public enum IntrinsicTimeModeType
    {
        Trending,
        TrendExtremeChanged,
        TrendDirectionChanged,
        TrendReversalChanged,
        PredictedIntervalChanged,
        HoldTradeChanged,
        InTradeChanged,
        /// <summary>A band-qualified new extreme with calculated BandLevel greater than one.</summary>
        TrendOvershootExtended = 7,
        /// <summary>A band-qualified retracement with calculated BandLevel greater than one.</summary>
        TrendOvershootReversal = 8
    }
}
