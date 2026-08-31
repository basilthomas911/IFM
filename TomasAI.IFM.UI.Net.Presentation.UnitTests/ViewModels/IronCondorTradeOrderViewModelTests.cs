using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class IronCondorTradeOrderViewModelTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 11);

    [Fact]
    public void State_IsObservableAndPublicSurfaceHasNoCallbackAdapters()
    {
        var viewModel = CreateViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        viewModel.FundBalance = 125_000m;
        viewModel.OrderPrice = 4.25m;
        viewModel.LocalSymbol = "ESU6 P4500";
        viewModel.SetOrderAction(OrderActionType.Close);

        viewModel.FundBalance.Should().Be(125_000m);
        viewModel.OrderPrice.Should().Be(4.25m);
        viewModel.LocalSymbol.Should().Be("ESU6 P4500");
        viewModel.OrderActionType.Should().Be(OrderActionType.Close);
        viewModel.LiveStreamMetrics.FuturesOptionTicks.Should().BeEmpty();
        changed.Should().Contain([
            nameof(viewModel.FundBalance),
            nameof(viewModel.OrderPrice),
            nameof(viewModel.LocalSymbol),
            nameof(viewModel.OrderActionType)]);
        typeof(INotifyPropertyChanged).IsAssignableFrom(viewModel.GetType()).Should().BeTrue();

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
    }

    [Fact]
    public async Task LoadFailure_PublishesCodedErrorAndResetsLoadingState()
    {
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        referenceApi.GetDefaultFuturesContractDefinitionsAsync().Returns(
            new ServiceFailed<DefaultFuturesContractDefinitionsReadModel>(744, "reference query unavailable"));
        var appRoot = Substitute.For<IAppRoot>();
        var viewModel = CreateViewModel(
            appRoot,
            referenceDataService: UiServiceFactory.CreateReference(referenceApi));

        var exception = await FluentActions.Awaiting(viewModel.LoadIronCondorTradeOrders)
            .Should().ThrowAsync<UiOperationException>();

        exception.Which.ErrorCode.Should().Be(744);
        viewModel.LastError!.ErrorCode.Should().Be(744);
        viewModel.LastError.Message.Should().Be("reference query unavailable");
        viewModel.IsLoading.Should().BeFalse();
        viewModel.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void Constructor_RejectsUnsupportedTradeType()
    {
        var action = () => CreateViewModel(tradeType: TradeType.PutCreditSpread);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid trade type*");
    }

    [Fact]
    public void NewTradeWithOrdinaryReference_CreatesFourUnsetOptionLegs()
    {
        var viewModel = CreateViewModel(reference: "G2-TEST-Trade");
        var createTrade = typeof(IronCondorTradeOrderViewModel).GetMethod(
            "CreateIronCondorTrade",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var trade = (OptionTradeReadModel)createTrade.Invoke(viewModel, [TradeStatus.Open])!;

        trade.OptionLegs.Should().HaveCount(4);
        trade.OptionLegs.Should().OnlyContain(leg => leg.ContractId == string.Empty);
        trade.OptionLegs.Select(leg => (leg.OptionLegType, leg.OptionLegAction))
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task HistoricalReadOnly_LoadsHydratedTradeWithoutCurrentServices_AndFencesCommands()
    {
        var seed = CreateViewModel();
        var createTrade = typeof(IronCondorTradeOrderViewModel).GetMethod(
            "CreateIronCondorTrade",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var historicalTrade = (OptionTradeReadModel)createTrade.Invoke(seed, [TradeStatus.Open])!;
        var viewModel = CreateViewModel(
            historicalReadOnly: true,
            historicalTrade: historicalTrade,
            historicalFundBalance: 250_000m);

        await viewModel.LoadIronCondorTradeOrders();

        viewModel.IsHistoricalReadOnly.Should().BeTrue();
        viewModel.IsLoaded.Should().BeTrue();
        viewModel.IronCondorTrade.Should().BeSameAs(historicalTrade);
        viewModel.FundBalance.Should().Be(250_000m);
        viewModel.OptionLegs.Should().HaveCount(4);

        await FluentActions.Awaiting(viewModel.SetFundMaxProfit)
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Historical trade orders are read-only.");
        await FluentActions.Awaiting(() => viewModel.RemoveTradeFromFundOrder(new FundOrderTradeId(17, 101, 7)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Historical trade orders are read-only.");
        await FluentActions.Awaiting(viewModel.TurnLiveFeedOn)
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Historical trade orders are read-only.");
    }

    [Fact]
    public void HistoricalReadOnly_RequiresMatchingHydratedTradeDbIdentity()
    {
        var missingTrade = () => CreateViewModel(historicalReadOnly: true);

        missingTrade.Should().Throw<ArgumentNullException>()
            .WithParameterName("historicalTrade");

        var mismatchedTrade = new OptionTradeReadModel
        {
            OrderId = 999,
            TradeId = 888,
            TradeType = TradeType.ShortIronCondor,
        };
        var mismatchedIdentity = () => CreateViewModel(
            historicalReadOnly: true,
            historicalTrade: mismatchedTrade);

        mismatchedIdentity.Should().Throw<ArgumentException>()
            .WithParameterName("historicalTrade")
            .WithMessage("*does not match composition 101:7*");
    }

    static IronCondorTradeOrderViewModel CreateViewModel(
        IAppRoot? appRoot = null,
        TradeType tradeType = TradeType.ShortIronCondor,
        string reference = "P:4500:4550 X C:5000:5050",
        IReferenceDataService? referenceDataService = null,
        bool historicalReadOnly = false,
        OptionTradeReadModel? historicalTrade = null,
        decimal historicalFundBalance = 0m)
        => new(
            appRoot ?? Substitute.For<IAppRoot>(),
            ValueDate,
            17,
            Contract(),
            Order(),
            Trade(tradeType, reference),
            OrderActionType.Open,
            referenceDataService ?? Substitute.For<IReferenceDataService>(),
            historicalReadOnly: historicalReadOnly,
            historicalTrade: historicalTrade,
            historicalFundBalance: historicalFundBalance);

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

    static FundOrderTradeReadModel Trade(TradeType tradeType, string reference)
        => new(
            17,
            101,
            7,
            tradeType,
            ValueDate,
            new DateOnly(2026, 9, 18),
            TradeState.NewTrade,
            TradeAction.Sell,
            reference,
            true,
            "ES",
            DateTime.UtcNow,
            "test",
            null,
            "test");

    static FuturesContractV2ReadModel Contract()
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
