
namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

public enum FuturesTrendConfirmationType
{
    RangeBound,
    UpTrending,
    DownTrending,
}

public static class FuturesTrendConfirmationTypeExtensions
{
    public static string ToStringFast(this FuturesTrendConfirmationType value) => value switch
    {
        FuturesTrendConfirmationType.RangeBound => nameof(FuturesTrendConfirmationType.RangeBound),
        FuturesTrendConfirmationType.UpTrending => nameof(FuturesTrendConfirmationType.UpTrending),
        FuturesTrendConfirmationType.DownTrending => nameof(FuturesTrendConfirmationType.DownTrending),
        _ => value.ToString()
    };
}
