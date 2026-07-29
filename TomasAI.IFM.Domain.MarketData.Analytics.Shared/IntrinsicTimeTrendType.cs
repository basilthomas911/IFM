namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
    public enum IntrinsicTimeTrendType
    {
        UpTrend,
        DownTrend
    }

    public static class IntrinsicTimeTrendTypeExtensions
    {
        public static string ToStringFast(this IntrinsicTimeTrendType value) => value switch
        {
            IntrinsicTimeTrendType.UpTrend => nameof(IntrinsicTimeTrendType.UpTrend),
            IntrinsicTimeTrendType.DownTrend => nameof(IntrinsicTimeTrendType.DownTrend),
            _ => value.ToString()
        };
    }
}
