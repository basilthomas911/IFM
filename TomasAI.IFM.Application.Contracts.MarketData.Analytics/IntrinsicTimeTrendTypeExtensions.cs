namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{

    public static class IntrinsicTimeTrendTypeExtensions
    {

        extension(IntrinsicTimeTrendType value)
        {
            public string ToStringFast() => value switch
            {
                IntrinsicTimeTrendType.UpTrend => nameof(IntrinsicTimeTrendType.UpTrend),
                IntrinsicTimeTrendType.DownTrend => nameof(IntrinsicTimeTrendType.DownTrend),
                _ => value.ToString()
            };
        }
    }
}
