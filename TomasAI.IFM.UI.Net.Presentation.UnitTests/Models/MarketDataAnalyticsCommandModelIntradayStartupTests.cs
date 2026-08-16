using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public class MarketDataAnalyticsCommandModelIntradayStartupTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 14);

    [Fact]
    public async Task StartFuturesIntradaySignals_StartsAllTwentyFourConfiguredActorsOnce()
    {
        var commandApi = CreateSuccessfulApi();
        var subject = CreateSubject(commandApi);

        var result = await subject.StartFuturesIntradaySignalsAsync("ESZ26", ValueDate);

        result.AllSucceeded.Should().BeTrue();
        result.RequestedCount.Should().Be(24);
        result.SuccessfulCount.Should().Be(24);
        result.Signals.Select(signal => signal.TimeFrame).Distinct().Should()
            .Equal(FuturesIntradaySignalActivationProfile.TimeFrames);

        foreach (var activation in FuturesIntradaySignalActivationProfile.Create("ESZ26", ValueDate))
        {
            await commandApi.Received(1).StartFuturesRsiSignalAsync(activation.Rsi);
            await commandApi.Received(1).StartFuturesAtrSignalAsync(activation.Atr);
            await commandApi.Received(1).StartFuturesAdxSignalAsync(activation.Adx);
            await commandApi.Received(1).StartFuturesMacdSignalAsync(activation.Macd);
        }
    }

    [Fact]
    public async Task StartFuturesIntradaySignals_ReportsFailureWithoutRetrying()
    {
        var commandApi = CreateSuccessfulApi();
        var failedId = FuturesAdxSignalEntityId.Create(
            "ESZ26",
            ValueDate,
            TimeFrameType.FourHours,
            FuturesIntradaySignalActivationProfile.AdxPeriodLength);
        commandApi.StartFuturesAdxSignalAsync(failedId)
            .Returns(new ServiceFailed<Guid>(8124, "ADX unavailable"));
        var subject = CreateSubject(commandApi);

        var result = await subject.StartFuturesIntradaySignalsAsync("ESZ26", ValueDate);

        result.AllSucceeded.Should().BeFalse();
        result.SuccessfulCount.Should().Be(23);
        result.Failures.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                SignalType = IntradaySignalType.Adx,
                TimeFrame = TimeFrameType.FourHours,
                ErrorCode = 8124,
                ErrorMessage = "ADX unavailable"
            });
        await commandApi.Received(1).StartFuturesAdxSignalAsync(failedId);
    }

    static MarketDataAnalyticsCommandModel CreateSubject(IMarketDataAnalyticsCommandApi commandApi)
        => new(
            commandApi,
            Substitute.For<IFuturesTradeSignalUIEventConsumer>(),
            Substitute.For<IFuturesRsiSignalUIEventConsumer>());

    static IMarketDataAnalyticsCommandApi CreateSuccessfulApi()
    {
        var commandApi = Substitute.For<IMarketDataAnalyticsCommandApi>();
        commandApi.StartFuturesRsiSignalAsync(Arg.Any<FuturesRsiSignalEntityId>())
            .Returns(_ => new ServiceOk<Guid>(Guid.NewGuid()));
        commandApi.StartFuturesAtrSignalAsync(Arg.Any<FuturesAtrSignalEntityId>())
            .Returns(_ => new ServiceOk<Guid>(Guid.NewGuid()));
        commandApi.StartFuturesAdxSignalAsync(Arg.Any<FuturesAdxSignalEntityId>())
            .Returns(_ => new ServiceOk<Guid>(Guid.NewGuid()));
        commandApi.StartFuturesMacdSignalAsync(Arg.Any<FuturesMacdSignalEntityId>())
            .Returns(_ => new ServiceOk<Guid>(Guid.NewGuid()));
        return commandApi;
    }
}
