namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class DatabentoFeedFactory : IDatabentoFeedFactory
{
    private readonly LatestPriceAdmissionControl _latestPriceAdmissionControl;

    public DatabentoFeedFactory()
        : this(LatestPriceAdmissionControl.Shared)
    {
    }

    internal DatabentoFeedFactory(
        LatestPriceAdmissionControl latestPriceAdmissionControl)
    {
        _latestPriceAdmissionControl = latestPriceAdmissionControl;
    }

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

    public IDatabentoLatestPriceClient CreateLatestPriceClient(
        DatabentoFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Dataset);
        return new DatabentoLatestPriceClient(
            options.Dataset,
            _latestPriceAdmissionControl);
    }
}
