using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public class FuturesIntradaySignalActivationTests
{
    static readonly TimeFrameType[] ExpectedTimeFrames =
    [
        TimeFrameType.FifteenSeconds,
        TimeFrameType.OneMinute,
        TimeFrameType.FiveMinutes,
        TimeFrameType.FifteenMinutes,
        TimeFrameType.OneHour,
        TimeFrameType.FourHours
    ];

    [Fact]
    public void Profile_ContainsExactFramesAndConventionalParameters()
    {
        var activations = FuturesIntradaySignalActivationProfile.Create(
            "ESZ26",
            new DateOnly(2026, 8, 14));

        activations.Select(activation => activation.TimeFrame).Should().Equal(ExpectedTimeFrames);
        activations.Should().AllSatisfy(activation =>
        {
            activation.Rsi.PeriodLength.Should().Be(13);
            activation.Atr.PeriodLength.Should().Be(14);
            activation.Adx.PeriodLength.Should().Be(14);
            activation.Macd.SignalEmaPeriod.Should().Be(9);
            activation.Macd.FastEmaPeriod.Should().Be(12);
            activation.Macd.SlowEmaPeriod.Should().Be(26);
        });
    }

    [Fact]
    public void TdiSupport_MatchesEveryAndOnlyConfiguredRsiFrame()
    {
        Enum.GetValues<TimeFrameType>()
            .Where(FuturesTdiConfiguration.IsSupportedIntraday)
            .Should().Equal(ExpectedTimeFrames);
    }

    [Fact]
    public async Task EveryConfiguredFrame_StartsARealTimerRegistrationForEverySignalType()
    {
        var activations = FuturesIntradaySignalActivationProfile.Create(
            "ESZ26",
            new DateOnly(2026, 8, 14));

        try
        {
            foreach (var activation in activations)
            {
                new FuturesRsiSignalStartedEvent { EntityId = activation.Rsi }
                    .StartTimer(_ => ValueTask.CompletedTask).Should().BeTrue();
                new FuturesAtrSignalStartedEvent { EntityId = activation.Atr }
                    .StartTimer(_ => ValueTask.CompletedTask).Should().BeTrue();
                new FuturesAdxSignalStartedEvent { EntityId = activation.Adx }
                    .StartTimer(_ => ValueTask.CompletedTask).Should().BeTrue();
                new FuturesMacdSignalStartedEvent { EntityId = activation.Macd }
                    .StartTimer(_ => ValueTask.CompletedTask).Should().BeTrue();
            }
        }
        finally
        {
            await FuturesRsiSignalTimer.StopAllAsync();
            await FuturesAtrSignalTimer.StopAllAsync();
            await FuturesAdxSignalTimer.StopAllAsync();
            await FuturesMacdSignalTimer.StopAllAsync();
        }
    }
}
