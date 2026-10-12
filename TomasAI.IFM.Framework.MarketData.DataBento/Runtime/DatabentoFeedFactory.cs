namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class DatabentoFeedFactory : IDatabentoFeedFactory
{
    private readonly LatestPriceAdmissionControl _latestPriceAdmissionControl;

    /// <summary>Initializes a new DatabentoFeedFactory instance.</summary>
    public DatabentoFeedFactory()
        : this(LatestPriceAdmissionControl.Shared)
    {
    }

    /// <summary>Initializes a new DatabentoFeedFactory instance.</summary>
    /// <param name="latestPriceAdmissionControl">The latest price admission control.</param>
    internal DatabentoFeedFactory(
        LatestPriceAdmissionControl latestPriceAdmissionControl)
    {
        _latestPriceAdmissionControl = latestPriceAdmissionControl;
    }

    /// <summary>Creates a ticker feed using the specified feed options.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    /// <returns>The create ticker feed result.</returns>
    public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) =>
        new SyntheticTickerFeed(FeedOptionsValidator.ValidateAndSnapshot(options));

    /// <summary>Creates an option-chain feed using the specified feed options.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    /// <returns>The create option chain feed result.</returns>
    public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options)
    {
        var snapshot = FeedOptionsValidator.ValidateAndSnapshot(options);
        return new SyntheticOptionChainFeed(snapshot);
    }

    /// <summary>Creates the contract-reference query client for the configured dataset.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    /// <returns>The create market data queries result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation names the public request that owns the invalid property rather than an inaccessible nested field.")]
    public IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Dataset, nameof(options));
        return new DatabentoMarketDataQueries(options.Dataset);
    }

    /// <summary>Creates the latest-price client with the configured admission limits.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    /// <returns>The create latest price client result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation names the public request that owns the invalid property rather than an inaccessible nested field.")]
    public IDatabentoLatestPriceClient CreateLatestPriceClient(
        DatabentoFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Dataset, nameof(options));
        return new DatabentoLatestPriceClient(
            options.Dataset,
            _latestPriceAdmissionControl);
    }
}
