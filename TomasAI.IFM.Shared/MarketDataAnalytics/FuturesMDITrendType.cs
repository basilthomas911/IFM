namespace TomasAI.IFM.Shared.MarketDataAnalytics;

public enum FuturesMDITrendType
{
    RangeBound,
    UpTrending,
    DownTrending
}

public static class FuturesMDITrendTypeExtensions
{
    public static string ToStringFast(this FuturesMDITrendType value) => value switch
    {
        FuturesMDITrendType.RangeBound => nameof(FuturesMDITrendType.RangeBound),
        FuturesMDITrendType.UpTrending => nameof(FuturesMDITrendType.UpTrending),
        FuturesMDITrendType.DownTrending => nameof(FuturesMDITrendType.DownTrending),
        _ => value.ToString()
    };
}
