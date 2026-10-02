using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class FourHourDatabentoSeedReplayTests
{
    [Fact]
    public void Replay_bar_uses_event_time_for_ohlc_and_a_matching_observation_identity()
    {
        var start = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero);
        var window = new MarketSessionBounds(new DateOnly(2026, 10, 2),
            start, start.AddMinutes(5));
        var bucket = new FourHourDatabentoSeedReplay.BarBucket();
        bucket.Add(102m, 2, start.AddMinutes(2), 12);
        bucket.Add(101m, 3, start.AddMinutes(1), 11);
        bucket.Add(103m, 4, start.AddMinutes(3), 13);
        var bar = bucket.ToObservation(MarketSeriesIdentity.ForContract("ES-SEED"),
            "ES-SEED", window, window.EndUtc);

        Assert.Equal(101m, bar.Open);
        Assert.Equal(103m, bar.Close);
        Assert.Equal(101m, bar.Low);
        Assert.Equal(103m, bar.High);
        Assert.Equal(9m, bar.Volume);
        Assert.Equal(3, bar.TradeCount);
        Assert.Equal(11, bar.FirstSourceSequence);
        Assert.Equal(13, bar.LastSourceSequence);
        Assert.Empty(new FuturesTradeSessionBarReadModelValidationRules().Execute(bar));
    }
}
