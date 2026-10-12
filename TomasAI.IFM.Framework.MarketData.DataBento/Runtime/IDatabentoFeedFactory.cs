using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoFeedFactory
{
    IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options);
    IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options);
    IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options);
    IDatabentoLatestPriceClient CreateLatestPriceClient(DatabentoFeedOptions options);
}
