
namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

public static class FuturesTrendConfirmationTypeExtensions
{

    extension(FuturesTrendConfirmationType value)
    {
        public string ToStringFast() => value switch
        {
            FuturesTrendConfirmationType.RangeBound => nameof(FuturesTrendConfirmationType.RangeBound),
            FuturesTrendConfirmationType.UpTrending => nameof(FuturesTrendConfirmationType.UpTrending),
            FuturesTrendConfirmationType.DownTrending => nameof(FuturesTrendConfirmationType.DownTrending),
            _ => value.ToString()
        };
    }
}
