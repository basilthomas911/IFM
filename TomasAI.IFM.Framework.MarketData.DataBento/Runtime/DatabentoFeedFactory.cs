namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class DatabentoFeedFactory : IDatabentoFeedFactory
{
    public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) =>
        new SyntheticTickerFeed(FeedOptionsValidator.ValidateAndSnapshot(options));

    public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options)
    {
        var snapshot = FeedOptionsValidator.ValidateAndSnapshot(options);
        return new SyntheticOptionChainFeed(snapshot);
    }

    public IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Dataset);
        return new DatabentoMarketDataQueries(options.Dataset);
    }
}
