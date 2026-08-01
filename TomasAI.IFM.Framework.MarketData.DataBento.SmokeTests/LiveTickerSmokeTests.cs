namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

public sealed class LiveTickerSmokeTests
{
    [Fact]
    public void CurrentEsFutureAuthenticatesResolvesAndStarts()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var baseOptions = LiveTestGate.CreateOptions();
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(baseOptions);
        var now = LiveTestGate.UtcNowNanoseconds();
        var currentFuture = queries
            .GetContractDetails("ES", TimeSpan.FromSeconds(90))
            .First(detail =>
                detail.ContractKind == ContractKind.Future
                && detail.ExpirationTimestampNanoseconds > now);
        var options = LiveTestGate.CreateLiveOptions();

        using var feed = new DatabentoFeedFactory().CreateTickerFeed(options);
        feed.Subscribe(
        [
            new TickerSubscription(
                currentFuture.RawSymbol,
                DatabentoInputSymbology.RawSymbol,
                MarketDataKinds.Quote)
        ], TimeSpan.FromSeconds(5));
        feed.Start(TimeSpan.FromSeconds(45));
        try
        {
            var registration = Assert.Single(feed.GetInstruments());
            Assert.Equal(currentFuture.RawSymbol, registration.RequestedSymbol);
            Assert.Equal(currentFuture.Instrument, registration.Instrument);
            Assert.Equal(FeedState.Running, feed.GetHealth().State);
        }
        finally
        {
            feed.Stop(TimeSpan.FromSeconds(10));
        }
    }
}
