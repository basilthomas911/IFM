
namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

public static class FuturesTrendTypeExtensions
{

    extension(FuturesTrendType value)
    {
        public string ToStringFast() => value switch
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
}
