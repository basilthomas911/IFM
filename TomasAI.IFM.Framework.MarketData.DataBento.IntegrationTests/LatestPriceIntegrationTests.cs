namespace TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests;

public sealed class LatestPriceIntegrationTests
{
    [Fact]
    public void InvalidLatestPriceSelectorsAreRejectedAfterConnectionWasVerified()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        _ = LiveTestGate.CreateConnectedQueries();
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");
        var client = new DatabentoFeedFactory().CreateLatestPriceClient(options);

        var crossDataset = Assert.Throws<ArgumentException>(() =>
            client.GetLatestPrice(new LatestPriceRequest
            {
                Dataset = "OPRA.PILLAR",
                Symbol = "ES",
                PricePolicy = LatestPricePolicy.LastTrade,
                FreshnessPolicy = LatestPriceFreshnessPolicy.NextObserved
            }, TimeSpan.FromSeconds(1)));
        Assert.Contains("does not match", crossDataset.Message);

        var invalidPolicy = Assert.Throws<ArgumentException>(() =>
            client.GetLatestPrice(new LatestPriceRequest
            {
                Dataset = "GLBX.MDP3",
                Symbol = "ES",
                PricePolicy = (LatestPricePolicy)byte.MaxValue,
                FreshnessPolicy = LatestPriceFreshnessPolicy.NextObserved
            }, TimeSpan.FromSeconds(1)));
        Assert.Contains("selection policy", invalidPolicy.Message);

        var invalidLookback = Assert.Throws<ArgumentException>(() =>
            client.GetLatestPrice(new LatestPriceRequest
            {
                Dataset = "GLBX.MDP3",
                Symbol = "ES",
                PricePolicy = LatestPricePolicy.Bid,
                FreshnessPolicy = LatestPriceFreshnessPolicy.NextObserved,
                ReplayLookback = TimeSpan.FromMinutes(1)
            }, TimeSpan.FromSeconds(1)));
        Assert.Contains("zero replay lookback", invalidLookback.Message);
    }
}
