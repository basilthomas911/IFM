using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoLatestPriceClient
{
    LatestPriceResult64 GetLatestPrice(
        LatestPriceRequest request,
        TimeSpan timeout);
}
