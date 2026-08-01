namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class DatabentoFeedFactory : IDatabentoFeedFactory
{
    public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) =>
        new SyntheticTickerFeed(FeedOptionsValidator.ValidateAndSnapshot(options));

    public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options) =>
        new SyntheticOptionChainFeed(FeedOptionsValidator.ValidateAndSnapshot(options));
}
