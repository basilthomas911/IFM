using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class IronCondorMonitorViewModelTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 11);

    [Fact]
    public async Task State_IsObservableCallbackFreeAndSafeBeforeLoading()
    {
        var viewModel = CreateViewModel();

        typeof(INotifyPropertyChanged).IsAssignableFrom(viewModel.GetType()).Should().BeTrue();
        viewModel.FuturesEodHistory.Should().BeEmpty();
        viewModel.TradeInfo.Should().BeEmpty();
        viewModel.TradeHistory.Should().BeEmpty();
        viewModel.TradePlans.Should().BeEmpty();
        viewModel.IsLiveFeedEnabled.Should().BeFalse();
        viewModel.LiveStreamMetrics.FuturesEod.IsOpen.Should().BeFalse();
        viewModel.LiveStreamMetrics.TradePosition.IsOpen.Should().BeFalse();
        viewModel.LiveStreamMetrics.TradePlan.IsOpen.Should().BeFalse();
        viewModel.LiveStreamMetrics.FuturesOptionTicks.Should().BeEmpty();
        viewModel.LiveStreamMetrics.SpreadBars.IsOpen.Should().BeFalse();
        await viewModel.LoadOptionTradeSpreadBarData(-1);

        var callbackMembers = viewModel.GetType()
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            });
        callbackMembers.Should().BeEmpty();

        var callbackParameters = viewModel.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .Where(parameter => typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        callbackParameters.Should().BeEmpty();

        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task UiDispatchMetrics_RecordLastAndMaximumLatency()
    {
        var viewModel = CreateViewModel();

        viewModel.RecordUiDispatch(TimeSpan.FromMilliseconds(8), TimeSpan.FromMilliseconds(3));
        viewModel.RecordUiDispatch(TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(5));

        viewModel.UiDispatchMetrics.DispatchCount.Should().Be(2);
        viewModel.UiDispatchMetrics.LastDispatchDelay.Should().Be(TimeSpan.FromMilliseconds(2));
        viewModel.UiDispatchMetrics.MaximumDispatchDelay.Should().Be(TimeSpan.FromMilliseconds(8));
        viewModel.UiDispatchMetrics.LastRenderDuration.Should().Be(TimeSpan.FromMilliseconds(5));
        viewModel.UiDispatchMetrics.MaximumRenderDuration.Should().Be(TimeSpan.FromMilliseconds(5));
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task InitialTradeFailure_PublishesCodedErrorAndReturnsNoTrade()
    {
        var tradeApi = Substitute.For<ITradeQueryApi>();
        tradeApi.GetOptionTradeAsync(101, 7).Returns(
            new ServiceFailed<OptionTradeReadModel>(731, "trade projection unavailable"));
        var appRoot = CreateAppRoot();
        appRoot.Services.TradeQueries.Returns(new TradeQueryService(tradeApi));
        var viewModel = CreateViewModel(appRoot);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        var trade = await viewModel.LoadIronCondorTrade();
        var firstErrorSequence = viewModel.LastError!.Sequence;
        var retry = await viewModel.LoadIronCondorTrade();

        trade.Should().BeNull();
        retry.Should().BeNull();
        viewModel.LastError!.ErrorCode.Should().Be(731);
        viewModel.LastError.Message.Should().Be("trade projection unavailable");
        viewModel.LastError.Sequence.Should().BeGreaterThan(firstErrorSequence);
        changed.Count(property => property == nameof(viewModel.LastError)).Should().Be(2);
        viewModel.IsLoaded.Should().BeFalse();
        await viewModel.DisposeAsync();
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public async Task HistoricalViewer_LoadsSelectedTrade_WhenLegacyOrderDoesNotEmbedItsCompositions()
    {
        var optionTrade = new OptionTradeReadModel
        {
            OrderId = 101,
            TradeId = 7,
            TradeDate = ValueDate,
            TradeType = TradeType.ShortIronCondor,
        };
        var tradeApi = Substitute.For<ITradeQueryApi>();
        tradeApi.GetOptionTradeAsync(101, 7).Returns(new ServiceOk<OptionTradeReadModel>(optionTrade));
        var appRoot = CreateAppRoot();
        appRoot.Services.TradeQueries.Returns(new TradeQueryService(tradeApi));
        var viewModel = CreateViewModel(appRoot, historicalReadOnly: true, embedTradeInOrder: false);

        var result = await viewModel.LoadIronCondorTrade();

        result.Should().NotBeNull();
        result!.EntityId.Should().Be(optionTrade.EntityId);
        viewModel.IsHistoricalReadOnly.Should().BeTrue();
        await tradeApi.Received(1).GetOptionTradeAsync(101, 7);
        await viewModel.DisposeAsync();
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public async Task HistoricalViewer_FencesListenersLiveFeedsAndTradeDbWrites()
    {
        var viewModel = CreateViewModel(historicalReadOnly: true);

        await viewModel.EnableMarketDataFeedResetListener();
        var enableFeed = () => viewModel.EnableLiveFeedAsync();
        var writeSpread = () => viewModel.InsertOptionTradeSpreadData(0m, (null!, null!));

        (await enableFeed.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*read-only*live feeds and TradeDb mutations are disabled*");
        (await writeSpread.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*read-only*live feeds and TradeDb mutations are disabled*");
        viewModel.IsLiveFeedEnabled.Should().BeFalse();
        await viewModel.DisposeAsync();
    }

    static IronCondorViewModel CreateViewModel(
        IAppRoot? appRoot = null,
        bool historicalReadOnly = false,
        bool embedTradeInOrder = true)
    {
        var trade = Trade();
        var order = Order();
        if (embedTradeInOrder)
            order.Add(trade);
        return new IronCondorViewModel(
            appRoot ?? CreateAppRoot(),
            Fund(),
            order,
            trade,
            ValueDate,
            [Contract()],
            historicalReadOnly: historicalReadOnly);
    }

    static IAppRoot CreateAppRoot()
    {
        var appRoot = Substitute.For<IAppRoot>();
        var eventModel = new CommandResponseEventService(Substitute.For<ICommandResponseUIEventConsumer>());
        eventModel.SetSiteId(Guid.NewGuid());
        appRoot.Services.CommandResponses.Returns(eventModel);
        return appRoot;
    }

    static FundReadModel Fund()
        => new(17, "Paper", "Paper trading", 100_000m, false, DateTime.UtcNow, "test");

    static FundOrderReadModel Order()
        => new(
            17,
            101,
            ValueDate.ToDateTime(TimeOnly.MinValue),
            TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open,
            "ESZ26",
            ValueDate,
            new DateOnly(2026, 9, 18),
            "Paper iron condor",
            DateTime.UtcNow,
            "test",
            null,
            "test");

    static FundOrderTradeReadModel Trade()
        => new(
            17,
            101,
            7,
            TradeType.ShortIronCondor,
            ValueDate,
            new DateOnly(2026, 9, 18),
            TradeState.OrderFilled,
            TradeAction.Sell,
            "P:4500:4550 X C:5000:5050",
            true,
            "ES",
            DateTime.UtcNow,
            "test",
            null,
            "test");

    static FuturesContractV3ReadModel Contract()
        => new(
            "ESZ26",
            "ESZ26",
            "ES",
            "ESZ26",
            "FUT",
            "USD",
            "CME",
            "50",
            new DateOnly(2026, 12, 18),
            true);
}
