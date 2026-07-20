
namespace TomasAI.IFM.Shared.MarketDataAnalytics
{
    public enum IntrinsicTimeModeType
    {
        Trending,
        TrendExtremeChanged,
        TrendDirectionChanged,
        TrendReversalChanged,
        PredictedIntervalChanged,
        HoldTradeChanged,
        InTradeChanged
    }

    public static class IntrinsicTimeModeTypeExtensions
    {
        public static string ToStringFast(this IntrinsicTimeModeType value) => value switch
        {
            IntrinsicTimeModeType.Trending => nameof(IntrinsicTimeModeType.Trending),
            IntrinsicTimeModeType.TrendExtremeChanged => nameof(IntrinsicTimeModeType.TrendExtremeChanged),
            IntrinsicTimeModeType.TrendDirectionChanged => nameof(IntrinsicTimeModeType.TrendDirectionChanged),
            IntrinsicTimeModeType.TrendReversalChanged => nameof(IntrinsicTimeModeType.TrendReversalChanged),
            IntrinsicTimeModeType.PredictedIntervalChanged => nameof(IntrinsicTimeModeType.PredictedIntervalChanged),
            IntrinsicTimeModeType.HoldTradeChanged => nameof(IntrinsicTimeModeType.HoldTradeChanged),
            IntrinsicTimeModeType.InTradeChanged => nameof(IntrinsicTimeModeType.InTradeChanged),
            _ => value.ToString()
        };
    }
}
