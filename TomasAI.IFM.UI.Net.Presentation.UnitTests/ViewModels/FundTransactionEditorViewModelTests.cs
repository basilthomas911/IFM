using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class FundTransactionEditorViewModelTests
{
    [Fact]
    public async Task LoadFundsOperation_PublishesFundsAndSafeSelection()
    {
        var fund = CreateFund();
        var (viewModel, api) = CreateSubject();
        api.GetFundsAsync().Returns(Task.FromResult<ServiceResult<FundReadModel[]>>(
            new ServiceOk<FundReadModel[]>([fund])));

        await viewModel.LoadFundsOperation.ExecuteAsync();

        viewModel.Funds.Should().Equal(fund);
        viewModel.GetFundId(0).Should().Be(fund.FundId);
        viewModel.GetFundId(-1).Should().Be(-1);
    }

    [Fact]
    public async Task LoadFundsOperation_ExcludesUnnamedLegacyFunds()
    {
        var named = CreateFund();
        var unnamed = named with { FundId = 8, Name = " " };
        var (viewModel, api) = CreateSubject();
        api.GetFundsAsync().Returns(Task.FromResult<ServiceResult<FundReadModel[]>>(
            new ServiceOk<FundReadModel[]>([named, unnamed])));

        await viewModel.LoadFundsOperation.ExecuteAsync();

        viewModel.Funds.Should().Equal(named);
        viewModel.GetFundId(0).Should().Be(named.FundId);
    }

    [Fact]
    public async Task LoadFundDetailsOperation_PublishesOneConsistentSnapshot()
    {
        var transaction = CreateTransaction();
        var report = new FundPnlReportReadModel(.6, 30m, .4, -15m, 2, 1, 1.2, 100m, .1, 5m);
        var (viewModel, api) = CreateSubject();
        api.GetFundTransactionsAsync(7, Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(
            Task.FromResult<ServiceResult<FundTransactionReadModel[]>>(
                new ServiceOk<FundTransactionReadModel[]>([transaction])));
        api.GetFundBalanceAsync(7).Returns(Task.FromResult<ServiceResult<FundBalanceReadModel>>(
            new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(1234m))));
        api.GetFundPnlReportAsync(7, Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(
            Task.FromResult<ServiceResult<FundPnlReportReadModel>>(
                new ServiceOk<FundPnlReportReadModel>(report)));
        viewModel.SetFundDetailsFilter(7, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        await viewModel.LoadFundDetailsOperation.ExecuteAsync();
        viewModel.SelectTransaction(0);

        viewModel.FundTransactions.Should().Equal(transaction);
        viewModel.FundBalance.Should().Be(1234m);
        viewModel.FundPnlReport.Should().BeSameAs(report);
        viewModel.TransactionComment.Should().Be(transaction.Description);
    }

    [Fact]
    public async Task LoadFundsOperation_PreservesCodedModelFailure()
    {
        var (viewModel, api) = CreateSubject();
        api.GetFundsAsync().Returns(Task.FromResult<ServiceResult<FundReadModel[]>>(
            new ServiceFailed<FundReadModel[]>(802, "funds unavailable")));

        var exception = await FluentActions.Awaiting(
                () => viewModel.LoadFundsOperation.ExecuteAsync())
            .Should().ThrowAsync<ModelOperationException>();

        exception.Which.ErrorCode.Should().Be(802);
        viewModel.LoadFundsOperation.LastFailure.Should().BeSameAs(exception.Which);
    }

    static (FundTransactionEditorViewModel ViewModel, IFundQueryApi Api) CreateSubject()
    {
        var api = Substitute.For<IFundQueryApi>();
        var model = new FundQueryModel(api);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<FundQueryModel>().Returns(model);
        return (new FundTransactionEditorViewModel(appRoot), api);
    }

    static FundReadModel CreateFund()
        => new(7, "Paper", "Paper trading", 1000m, false, DateTime.UtcNow, "test");

    static FundTransactionReadModel CreateTransaction()
        => new(
            1,
            DateTime.UtcNow,
            FundTransactionType.OpeningTrade,
            7,
            10,
            20,
            default,
            new DateOnly(2026, 8, 11),
            default,
            "opened position",
            25m,
            1025m);
}
