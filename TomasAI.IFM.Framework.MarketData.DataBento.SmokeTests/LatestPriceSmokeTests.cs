using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

public sealed class LatestPriceSmokeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(LatestPricePolicy.LastTrade)]
    [InlineData(LatestPricePolicy.QuoteMidpoint)]
    [InlineData(LatestPricePolicy.Bid)]
    [InlineData(LatestPricePolicy.Ask)]
    public void CurrentEsFutureReturnsAQualifyingLatestPrice(
        LatestPricePolicy policy)
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var factory = new DatabentoFeedFactory();
        var options = LiveTestGate.CreateOptions();
        var queries = factory.CreateMarketDataQueries(options);
        var now = LiveTestGate.UtcNowNanoseconds();
        var currentFuture = queries
            .GetContractDetails("ES", TimeSpan.FromSeconds(90))
            .Where(detail =>
                detail.ContractKind == ContractKind.Future
                && detail.ExpirationTimestampNanoseconds > now
                && (detail.ActivationTimestampNanoseconds is null
                    || detail.ActivationTimestampNanoseconds <= now))
            .OrderBy(detail => detail.ExpirationTimestampNanoseconds)
            .First();
        var client = factory.CreateLatestPriceClient(options);

        var result = client.GetLatestPrice(new LatestPriceRequest
        {
            Dataset = options.Dataset,
            Symbol = currentFuture.RawSymbol,
            InputSymbology = DatabentoInputSymbology.RawSymbol,
            PricePolicy = policy,
            FreshnessPolicy = LatestPriceFreshnessPolicy.ReplayLookbackThenLive,
            ReplayLookback = TimeSpan.FromHours(1)
        }, TimeSpan.FromSeconds(90));

        Assert.Equal(currentFuture.Instrument.InstrumentId, result.InstrumentId);
        Assert.Equal(policy, result.SelectedPolicy);
        Assert.NotEqual(0, result.SelectedPrice);
        Assert.True(result.UsedReplay || result.IsLive);
        output.WriteLine(
            "{0} {1}: price={2}, replay={3}, live={4}",
            currentFuture.RawSymbol,
            policy,
            result.SelectedPrice,
            result.UsedReplay,
            result.IsLive);
    }
}
