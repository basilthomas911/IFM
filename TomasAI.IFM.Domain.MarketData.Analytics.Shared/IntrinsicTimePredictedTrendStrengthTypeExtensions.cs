namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{

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
