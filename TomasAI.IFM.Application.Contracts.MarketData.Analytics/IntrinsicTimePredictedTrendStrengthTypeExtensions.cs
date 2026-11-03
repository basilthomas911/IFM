namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{

    public static class IntrinsicTimePredictedTrendStrengthTypeExtensions
    {

        extension(IntrinsicTimePredictedTrendStrengthType value)
        {
            public string ToStringFast() => value switch
            {
                IntrinsicTimePredictedTrendStrengthType.Low => nameof(IntrinsicTimePredictedTrendStrengthType.Low),
                IntrinsicTimePredictedTrendStrengthType.High => nameof(IntrinsicTimePredictedTrendStrengthType.High),
                IntrinsicTimePredictedTrendStrengthType.Normal => nameof(IntrinsicTimePredictedTrendStrengthType.Normal),
                _ => value.ToString()
            };
        }
    }
}
