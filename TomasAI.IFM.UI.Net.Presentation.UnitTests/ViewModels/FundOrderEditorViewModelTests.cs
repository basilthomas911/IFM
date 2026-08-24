using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class FundOrderEditorViewModelTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 11);

    [Fact]
    public async Task LoadOperation_PublishesIdentifierAndEnrichedReference()
    {
        var subject = CreateSubject();

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.OrderId.Should().Be(501);
        subject.ViewModel.SelectedBaseContractId.Should().Be("ESZ26");
        subject.ViewModel.FuturesEodData.Should().NotBeNull();
        subject.ViewModel.Reference.Should().StartWith("ESZ26 @")
            .And.EndWith("=> Up:High:Rising:Rising");
        subject.ViewModel.CanSave.Should().BeTrue();
        subject.ViewModel.FundOrder.OrderId.Should().Be(501);
        subject.ViewModel.FundOrder.BaseContractId.Should().Be("ESZ26");
    }

    [Fact]
    public async Task SafeContractSelection_RefreshesTheSelectedContractOnly()
    {
        var subject = CreateSubject();
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.SelectBaseContract(-1).Should().BeFalse();
        subject.ViewModel.SelectBaseContract(1).Should().BeTrue();
        await subject.ViewModel.RefreshReferenceOperation.ExecuteAsync();

        subject.ViewModel.SelectedBaseContractId.Should().Be("NQZ26");
        subject.ViewModel.Reference.Should().StartWith("NQZ26 @");
        await subject.MarketDataApi.Received(1).GetFuturesEodDataAsync("NQZ26", ValueDate);
    }

    [Fact]
    public async Task DateChanges_UpdateReferenceAndSaveValidation()
    {
        var subject = CreateSubject();
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.SetTradeDate(new DateOnly(2026, 8, 20));
        subject.ViewModel.SetMaturityDate(new DateOnly(2026, 8, 19));

        subject.ViewModel.FundOrder.TradeDate.Should().Be(new DateOnly(2026, 8, 20));
        subject.ViewModel.FundOrder.MaturityDate.Should().Be(new DateOnly(2026, 8, 19));
        subject.ViewModel.CanSave.Should().BeFalse();

        subject.ViewModel.SetMaturityDate(new DateOnly(2026, 9, 18));

        subject.ViewModel.CanSave.Should().BeTrue();
        subject.ViewModel.FundOrder.MaturityDate.Should().Be(new DateOnly(2026, 9, 18));
    }

    [Fact]
    public async Task OperatorReferenceOverride_IsTrimmedAndPublishedInTheOrderPayload()
    {
        var subject = CreateSubject();
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.SetReference("  G2-UNITTEST-Order  ");

        subject.ViewModel.Reference.Should().Be("G2-UNITTEST-Order");
        subject.ViewModel.FundOrder.Reference.Should().Be("G2-UNITTEST-Order");
        subject.ViewModel.CanSave.Should().BeTrue();
    }

    [Fact]
    public async Task LoadOperation_IsSingleFlightAndBlocksSelectionWhileRunning()
    {
        var completion = new TaskCompletionSource<ServiceResult<ScalarReadModel<int>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var subject = CreateSubject(orderIdResult: completion.Task);

        var first = subject.ViewModel.LoadOperation.ExecuteAsync();
        var second = subject.ViewModel.LoadOperation.ExecuteAsync();

        first.Should().BeSameAs(second);
        subject.ViewModel.IsBusy.Should().BeTrue();
        subject.ViewModel.SelectBaseContract(1).Should().BeFalse();
        subject.ViewModel.CanSave.Should().BeFalse();

        completion.SetResult(new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(501)));
        await first;

        subject.ViewModel.IsBusy.Should().BeFalse();
        await subject.ReferenceApi.Received(1).GetNextSeedIdAsync("OrderId");
    }

    [Fact]
    public async Task CodedFailure_IsObservableAndPublicSurfaceHasNoCallbacks()
    {
        var subject = CreateSubject(
            orderIdResult: Task.FromResult<ServiceResult<ScalarReadModel<int>>>(
                new ServiceFailed<ScalarReadModel<int>>(919, "order id unavailable")));

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.LoadOperation.ExecuteAsync())
            .Should().ThrowAsync<UiOperationException>();

        exception.Which.ErrorCode.Should().Be(919);
        subject.ViewModel.LastError!.ErrorCode.Should().Be(919);
        subject.ViewModel.CanSave.Should().BeFalse();
        typeof(FundOrderEditorViewModel)
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
            .Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();
    }

    static Subject CreateSubject(
        Task<ServiceResult<ScalarReadModel<int>>>? orderIdResult = null)
    {
        orderIdResult ??= Task.FromResult<ServiceResult<ScalarReadModel<int>>>(
            new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(501)));
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        referenceApi.GetNextSeedIdAsync("OrderId").Returns(_ => orderIdResult);

        var marketDataApi = Substitute.For<IMarketDataFeedQueryApi>();
        marketDataApi.GetFuturesEodDataAsync(Arg.Any<string>(), ValueDate)
            .Returns(call => Task.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
                new ServiceOk<FuturesEodDataV2ReadModel>(Eod(call.ArgAt<string>(0)))));

        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.FeedQueries
            .Returns(new MarketDataFeedQueryService(marketDataApi));
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero));
        var viewModel = new FundOrderEditorViewModel(
            appRoot,
            ValueDate,
            [Contract("ESZ26", "ES"), Contract("NQZ26", "NQ")],
            17,
            UiServiceFactory.CreateReference(referenceApi),
            timeProvider);
        return new Subject(viewModel, referenceApi, marketDataApi);
    }

    static FuturesContractV2ReadModel Contract(string contractId, string symbol)
        => new(
            contractId,
            contractId,
            symbol,
            contractId,
            "FUT",
            "USD",
            "CME",
            "50",
            new DateOnly(2026, 12, 18),
            true);

    static FuturesEodDataV2ReadModel Eod(string contractId)
        => new(
            contractId,
            ValueDate,
            contractId[..2],
            6400,
            6420,
            6380,
            6410,
            1000,
            marketDirection: MarketDirectionType.Up,
            marketVolatility: MarketVolatilityType.High,
            priceDirection: PriceDirectionType.Rising,
            priceVolatility: PriceVolatilityType.Rising);

    sealed record Subject(
        FundOrderEditorViewModel ViewModel,
        IReferenceQueryApi ReferenceApi,
        IMarketDataFeedQueryApi MarketDataApi);
}
