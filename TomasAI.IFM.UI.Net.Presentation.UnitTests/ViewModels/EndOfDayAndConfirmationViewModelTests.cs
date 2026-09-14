using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class EndOfDayAndConfirmationViewModelTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 11);

    [Fact]
    public async Task LoadOperation_PublishesOneCoherentSnapshotAndDateChangeInvalidatesIt()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.Snapshot.Should().Be(new EndOfDayProcessSnapshot(
            6400m, 6420m, 6380m, 6410m, 1200, 25m, 100_025m));
        subject.ViewModel.CanRun.Should().BeTrue();
        subject.ViewModel.SetValueDate(ValueDate.AddDays(1));
        subject.ViewModel.Snapshot.Should().BeNull();
        subject.ViewModel.CanRun.Should().BeFalse();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task RunOperation_UsesStrategyPositionCommandAndObservesEndOfDayState()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject(commandId);
        subject.PositionQuery.GetCurrentAsync(
                Arg.Any<StrategyPositionId>(), TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>())
            .Returns(
                new ServiceOk<StrategyPositionSnapshot>(Position(StrategyPositionPhase.MarkToMarket)),
                new ServiceOk<StrategyPositionSnapshot>(Position(StrategyPositionPhase.EndOfDay)));
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        await subject.ViewModel.RunOperation.ExecuteAsync();

        subject.ViewModel.IsCompleted.Should().BeTrue();
        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.LastStatusMessage.Should().Contain("completed");
        await subject.PositionCommand.Received(1).EndOfDayAsync(
            subject.ViewModel.PositionId,
            TradeStrategyKind.IronCondor,
            Arg.Is<DateTime>(value => value.Kind == DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task PositionCommandFailure_IsReportedAndAllowsRetry()
    {
        var subject = CreateSubject();
        subject.PositionCommand.EndOfDayAsync(
                Arg.Any<StrategyPositionId>(), Arg.Any<TradeStrategyKind>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceFailed<Guid>(731, "position projection failed"));
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        await FluentActions.Awaiting(() => subject.ViewModel.RunOperation.ExecuteAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*731*position projection failed*");

        subject.ViewModel.LastError!.Message.Should().Contain("731");
        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.RunOperation.CanExecute.Should().BeTrue();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task LoadFailure_LeavesNoPartialSnapshot()
    {
        var subject = CreateSubject();
        subject.FundApi.GetFundsAsync().Returns(
            new ServiceFailed<FundReadModel[]>(744, "fund query unavailable"));

        await FluentActions.Awaiting(() => subject.ViewModel.LoadOperation.ExecuteAsync())
            .Should().ThrowAsync<UiServiceOperationException>();

        subject.ViewModel.LastError!.ErrorCode.Should().Be(744);
        subject.ViewModel.Snapshot.Should().BeNull();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task LifecycleIsObservableAndDoesNotOwnALegacyEventListener()
    {
        var subject = CreateSubject();

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.ViewModel.CanRun.Should().BeFalse();
        await subject.ViewModel.StopAsync(CancellationToken.None);

        AssertObservableWithoutCallbacks<EndOfDayProcessViewModel>();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public void ConfirmationState_IsObservableSafeAndDisablesUnimplementedBrokerFill()
    {
        var order = new TradeOrderReadModel { OrderDescription = "ES iron condor" };
        var viewModel = new TradeOrderConfirmationViewModel(order);

        viewModel.TradeOrder.Should().BeSameAs(order);
        viewModel.SelectedTradeFillType.Should().Be(TradeFillType.Manual);
        viewModel.CanConfirm.Should().BeTrue();
        viewModel.SelectTradeFillType(-1).Should().BeFalse();
        viewModel.SelectTradeFillType(1).Should().BeTrue();
        viewModel.SelectedTradeFillType.Should().Be(TradeFillType.Broker);
        viewModel.CanConfirm.Should().BeFalse();
        AssertObservableWithoutCallbacks<TradeOrderConfirmationViewModel>();
    }

    static Subject CreateSubject(Guid? commandId = null)
    {
        var fundApi = Substitute.For<IFundQueryApi>();
        fundApi.GetFundsAsync().Returns(new ServiceOk<FundReadModel[]>(
            [new FundReadModel(17, "Paper", "Paper trading", 100_000m, false, DateTime.UtcNow, "test")]));
        var marketDataApi = Substitute.For<IMarketDataFeedQueryApi>();
        marketDataApi.GetFuturesEodDataAsync("ESZ26", ValueDate)
            .Returns(new ServiceOk<FuturesEodDataV2ReadModel>(Eod()));
        var positionCommand = Substitute.For<IStrategyPositionCommandApi>();
        positionCommand.EndOfDayAsync(
                Arg.Any<StrategyPositionId>(), Arg.Any<TradeStrategyKind>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(commandId ?? Guid.NewGuid()));
        var positionQuery = Substitute.For<IStrategyPositionQueryApi>();
        positionQuery.GetCurrentAsync(
                Arg.Any<StrategyPositionId>(), TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<StrategyPositionSnapshot>(Position(StrategyPositionPhase.MarkToMarket)));
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.FundQueries.Returns(new FundQueryService(fundApi));
        appRoot.Services.FeedQueries.Returns(new MarketDataFeedQueryService(marketDataApi));
        appRoot.Services.StrategyPositions.Returns(new StrategyPositionService(positionCommand, positionQuery));
        var parameter = new TradeEndOfDayParameter
        {
            PortfolioId = 11,
            FundId = 17,
            OrderId = 101,
            TradeId = 7,
            TradeType = TradeType.ShortIronCondor,
            StrategyKind = TradeStrategyKind.IronCondor,
            BaseContractId = "ESZ26",
            ValueDate = ValueDate
        };
        return new(new EndOfDayProcessViewModel(appRoot, parameter), fundApi, positionCommand, positionQuery);
    }

    static StrategyPositionSnapshot Position(StrategyPositionPhase phase) => new()
    {
        Id = StrategyPositionId.Create(new TradeEntityId(11, 17, 101, 7), TradeStrategyKind.IronCondor),
        StrategyKind = TradeStrategyKind.IronCondor,
        Phase = phase,
        IsOpen = true,
        UnrealizedPnl = 20m,
        RealizedPnl = 5m,
        AsOfUtc = DateTime.UtcNow
    };

    static FuturesEodDataV2ReadModel Eod() => new(
        "ESZ26", ValueDate, "ES", 6400m, 6420m, 6380m, 6410m, 1200,
        marketDirection: MarketDirectionType.Up,
        marketVolatility: MarketVolatilityType.High,
        priceDirection: PriceDirectionType.Rising,
        priceVolatility: PriceVolatilityType.Rising);

    static void AssertObservableWithoutCallbacks<T>()
    {
        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(T)).Should().BeTrue();
        typeof(T).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            })
            .Should().BeEmpty();
    }

    sealed record Subject(
        EndOfDayProcessViewModel ViewModel,
        IFundQueryApi FundApi,
        IStrategyPositionCommandApi PositionCommand,
        IStrategyPositionQueryApi PositionQuery);
}
