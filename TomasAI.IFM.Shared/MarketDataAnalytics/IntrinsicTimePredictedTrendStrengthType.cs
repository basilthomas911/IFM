namespace TomasAI.IFM.Shared.MarketDataAnalytics
{
    public enum IntrinsicTimePredictedTrendStrengthType
    {
        Low,
        High,
        Normal
    }

    public static class IntrinsicTimePredictedTrendStrengthTypeExtensions
    {
        public static string ToStringFast(this IntrinsicTimePredictedTrendStrengthType value) => value switch
        {
            IntrinsicTimePredictedTrendStrengthType.Low => nameof(IntrinsicTimePredictedTrendStrengthType.Low),
            IntrinsicTimePredictedTrendStrengthType.High => nameof(IntrinsicTimePredictedTrendStrengthType.High),
            IntrinsicTimePredictedTrendStrengthType.Normal => nameof(IntrinsicTimePredictedTrendStrengthType.Normal),
            _ => value.ToString()
        };
    }
}
