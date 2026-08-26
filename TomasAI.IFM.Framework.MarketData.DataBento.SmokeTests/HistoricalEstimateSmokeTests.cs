using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Framework.MarketData.DataBento.Historical;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

/// <summary>Provides an opt-in, non-feed Databento Historical API preflight.</summary>
public sealed class HistoricalEstimateSmokeTests
{
    /// <summary>Estimates one minute without submitting a billable batch job or starting a live feed.</summary>
    [Fact]
    public async Task TinyHistoricalEstimateCompletesWithoutStartingLiveFeed()
    {
        if (!LiveTestGate.IsEnabled()) return;
        LiveTestGate.AssertCredential();
        var provider = new DatabentoHistoricalProvider(
            new DatabentoHistoricalProviderOptions
            {
                UseSyntheticProvider = false,
                TimeoutMilliseconds = 30_000,
                MaximumBatchRecords = 16
            },
            TimeProvider.System);
        var start = new DateTimeOffset(2026, 8, 24, 14, 0, 0, TimeSpan.Zero);

        var estimate = await provider.EstimateAsync(new HistoricalProviderRequest
        {
            Dataset = "GLBX.MDP3",
            Symbols = ["ES.c.0"],
            Schema = HistoricalDataSchema.OhlcvOneMinute,
            Symbology = HistoricalSymbology.Continuous,
            StartUtc = start,
            EndUtc = start.AddMinutes(1),
            RecordLimit = 10,
            RequestHash = "MDSI2-LIVE-PREFLIGHT"
        }, CancellationToken.None);

        Assert.True(estimate.EstimatedBytes >= 0);
        Assert.True(estimate.EstimatedRecords >= 0);
        Assert.True(estimate.EstimatedCostUsd >= 0);
    }
}
