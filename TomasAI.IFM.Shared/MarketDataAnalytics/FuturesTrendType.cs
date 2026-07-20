
namespace TomasAI.IFM.Shared.MarketDataAnalytics;

public enum FuturesTrendType
{
    RangeBound,
    UpTrend,
    UpTrending,
    DownTrend,
    DownTrending,
    Rising,
    Falling
}

public static class FuturesTrendTypeExtensions
{
    public static string ToStringFast(this FuturesTrendType value) => value switch
    {
        FuturesTrendType.RangeBound => nameof(FuturesTrendType.RangeBound),
        FuturesTrendType.UpTrend => nameof(FuturesTrendType.UpTrend),
        FuturesTrendType.UpTrending => nameof(FuturesTrendType.UpTrending),
        FuturesTrendType.DownTrend => nameof(FuturesTrendType.DownTrend),
        FuturesTrendType.DownTrending => nameof(FuturesTrendType.DownTrending),
        FuturesTrendType.Rising => nameof(FuturesTrendType.Rising),
        FuturesTrendType.Falling => nameof(FuturesTrendType.Falling),
        _ => value.ToString()
    };
}
