namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

[Collection(DatabentoSmokeCollection.Name)]
public sealed class LiveTickerSmokeTests(DatabentoSmokeFixture fixture)
{
    [Fact]
    public async Task CurrentEsFutureAuthenticatesResolvesAndStarts()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = fixture.Queries;
        var now = LiveTestGate.UtcNowNanoseconds();
        var currentFuture = queries
            .GetContractDetails("ES.FUT", TimeSpan.FromSeconds(90))
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
        var registration = Assert.Single(feed.GetInstruments());
        var drain = LiveTestGate.DrainUntilCompletedAsync(
            feed.GetReader(registration.Instrument));
        try
        {
            Assert.Equal(currentFuture.RawSymbol, registration.RequestedSymbol);
            Assert.Equal(currentFuture.Instrument, registration.Instrument);
            Assert.Equal(FeedState.Running, feed.GetHealth().State);
        }
        finally
        {
            feed.Stop(TimeSpan.FromSeconds(30));
            await drain.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }
}
