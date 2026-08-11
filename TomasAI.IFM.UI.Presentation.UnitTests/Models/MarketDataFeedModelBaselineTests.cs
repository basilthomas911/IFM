using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Presentation.UnitTests.Models;

public class MarketDataFeedModelBaselineTests
{
    readonly IMarketDataFeedCommandApi _commandApi = Substitute.For<IMarketDataFeedCommandApi>();
    readonly IMarketDataQueryApi _marketDataQueryApi = Substitute.For<IMarketDataQueryApi>();
    readonly IMarketDataFeedQueryApi _feedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
    readonly IFuturesEodDataUIEventConsumer _eodConsumer = Substitute.For<IFuturesEodDataUIEventConsumer>();
    readonly IFuturesTradeSignalUIEventConsumer _tradeSignalConsumer = Substitute.For<IFuturesTradeSignalUIEventConsumer>();
    readonly IFuturesOptionTickDataUIEventConsumer _optionTickConsumer = Substitute.For<IFuturesOptionTickDataUIEventConsumer>();
    readonly IMarketDataFeedResetUIEventConsumer _resetConsumer = Substitute.For<IMarketDataFeedResetUIEventConsumer>();
    readonly IFuturesBarDataUIEventConsumer _barConsumer = Substitute.For<IFuturesBarDataUIEventConsumer>();

    [Fact]
    public async Task StartDataFeed_ForwardsContractsAndValueDateExactlyOnce()
    {
        var model = CreateModel();
        ICollection<FuturesContractV2ReadModel> contracts = [];
        var valueDate = new DateOnly(2026, 8, 11);
        _commandApi.StartMarketDataFeedAsync(contracts, valueDate)
            .Returns(Task.FromResult<ServiceResult<Guid>>(
                new ServiceOk<Guid>(Guid.NewGuid())));

        await model.StartDataFeedAsync(contracts, valueDate);

        await _commandApi.Received(1).StartMarketDataFeedAsync(contracts, valueDate);
    }

    [Fact]
    public async Task StartDataFeed_PreservesServiceFailureForTheViewModelBoundary()
    {
        var model = CreateModel();
        ICollection<FuturesContractV2ReadModel> contracts = [];
        var valueDate = new DateOnly(2026, 8, 11);
        (int Code, string Message)? error = null;
        model.OnError((code, message) => error = (code, message));
        _commandApi.StartMarketDataFeedAsync(contracts, valueDate)
            .Returns(Task.FromResult<ServiceResult<Guid>>(
                new ServiceFailed<Guid>(7301, "Feed start failed.")));

        await model.StartDataFeedAsync(contracts, valueDate);

        error.Should().Be((7301, "Feed start failed."));
    }

    [Fact]
    public async Task OptionTickListener_StartAndStopOwnTheConsumerCalls()
    {
        var model = CreateModel();
        Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> listener = _ => ValueTask.CompletedTask;
        _optionTickConsumer.StartAsync(listener).Returns(ValueTask.CompletedTask);
        _optionTickConsumer.StopAsync().Returns(ValueTask.CompletedTask);

        await model.StartFuturesOptionTickDataListenerAsync(listener);
        await model.StopFuturesOptionTickDataListenerAsync();

        await _optionTickConsumer.Received(1).StartAsync(listener);
        await _optionTickConsumer.Received(1).StopAsync();
    }

    MarketDataFeedCommandModel CreateModel()
        => new(
            _commandApi,
            _marketDataQueryApi,
            _feedQueryApi,
            _eodConsumer,
            _tradeSignalConsumer,
            _optionTickConsumer,
            _resetConsumer,
            _barConsumer);
}
