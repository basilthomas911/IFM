using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Query.Api;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Query.Api;

public class ActorFundQueryApiTests
{
    static readonly DateOnly StartDate = new(2026, 1, 1);
    static readonly DateOnly EndDate = new(2026, 1, 31);

    [Fact]
    public void ImplementsTheActorQueryContractSeparatelyFromTheExternalAdapter()
    {
        var (api, _) = CreateApi();

        api.Should().BeAssignableTo<IActorFundQueryApi>();
        typeof(ActorFundQueryApi).Namespace.Should().Be("TomasAI.IFM.Domain.Fund.Query.Api");
        typeof(IActorFundQueryApi).GetMethod(nameof(IActorFundQueryApi.GetFundMaxProfitGeneratedAsync))
            .Should().NotBeNull();
        typeof(IFundQueryApi).GetMethod(nameof(IActorFundQueryApi.GetFundMaxProfitGeneratedAsync))
            .Should().BeNull();
    }

    [Fact]
    public async Task DirectQueriesReturnTypedSuccessResults()
    {
        var (api, db) = CreateApi();
        var fund = SampleData.Fund;
        var order = SampleData.FundOrder;
        var trade = SampleData.FundOrderTrade;
        var transaction = SampleData.FundTransaction;
        ICollection<FundOrderAmountReadModel> losses =
            [new FundOrderAmountReadModel(fund.FundId, StartDate, 1, -100m)];
        ICollection<FundOrderAmountReadModel> profits =
            [new FundOrderAmountReadModel(fund.FundId, EndDate, 2, 150m)];

        db.GetFundsAsync().Returns([fund]);
        db.GetFundOrdersAsync().Returns([order]);
        db.GetFundOrderTradesAsync().Returns([trade]);
        db.GetFundTransactionsAsync(fund.FundId, StartDate, EndDate).Returns([transaction]);
        db.GetFundBalanceAsync(fund.FundId).Returns(1_000m);
        db.GetOpeningFundBalanceAsync(fund.FundId, StartDate).Returns(900m);
        db.GetClosingFundBalanceAsync(fund.FundId, EndDate).Returns(1_200m);
        db.GetFundIdFromOrderIdAsync(order.OrderId).Returns(fund.FundId);
        db.GetFundLossOrdersAsync(fund.FundId, StartDate, EndDate).Returns(losses);
        db.GetFundProfitOrdersAsync(fund.FundId, StartDate, EndDate).Returns(profits);
        db.GetFundStartingBalanceAsync(fund.FundId, StartDate).Returns(1_000m);
        db.GetFundEndingBalanceAsync(fund.FundId, EndDate).Returns(1_200m);
        db.GetFundEndingBalanceAsync(fund.FundId, new DateOnly(2026, 12, 31)).Returns(1_300m);
        db.GetFundTradeCommissionAsync(fund.FundId, StartDate, EndDate).Returns(25m);
        db.GetFundDailyBalancesAsync(fund.FundId, StartDate, EndDate)
            .Returns(Array.Empty<FundDailyBalanceReadModel>());

        var funds = await api.GetFundsAsync();
        var orders = await api.GetFundOrdersAsync();
        var trades = await api.GetFundOrderTradesAsync();
        var transactions = await api.GetFundTransactionsAsync(fund.FundId, StartDate, EndDate);
        var balance = await api.GetFundBalanceAsync(fund.FundId);
        var openingBalance = await api.GetOpeningFundBalanceAsync(fund.FundId, StartDate);
        var closingBalance = await api.GetClosingFundBalanceAsync(fund.FundId, EndDate);
        var fundId = await api.GetFundIdFromOrderIdAsync(order.OrderId);
        var pnl = await api.GetFundPnlReportAsync(fund.FundId, StartDate, EndDate);
        var winLoss = await api.GetFundWinLossRatioAsync(fund.FundId, StartDate, EndDate);
        var drawdown = await api.GetFundDrawdownBalancesAsync(fund.FundId, StartDate, EndDate);
        var maxProfit = await api.GetFundMaxProfitGeneratedAsync(fund.FundId, EndDate);

        funds.Should().BeOfType<ServiceOk<FundReadModel[]>>();
        funds.Value.Should().Equal(fund);
        orders.Should().BeOfType<ServiceOk<FundOrderReadModel[]>>();
        orders.Value.Should().Equal(order);
        trades.Should().BeOfType<ServiceOk<FundOrderTradeReadModel[]>>();
        trades.Value.Should().Equal(trade);
        transactions.Should().BeOfType<ServiceOk<FundTransactionReadModel[]>>();
        transactions.Value.Should().Equal(transaction);
        balance.Should().BeOfType<ServiceOk<FundBalanceReadModel>>();
        balance.Value!.Value.Should().Be(1_000m);
        openingBalance.Value!.Value.Should().Be(900m);
        closingBalance.Value!.Value.Should().Be(1_200m);
        fundId.Should().BeOfType<ServiceOk<ScalarReadModel<int>>>();
        fundId.Value!.Value.Should().Be(fund.FundId);
        pnl.Should().BeOfType<ServiceOk<FundPnlReportReadModel>>();
        pnl.Value.Should().BeEquivalentTo(new
        {
            WinRate = 0.5,
            AverageProfit = 150m,
            LossRate = 0.5,
            AverageLoss = -100m,
            WinLossRatio = 1.5,
            PnlAmount = 200m,
            PnlPercent = 0.2,
            TradeCommission = 25m
        });
        winLoss.Should().BeOfType<ServiceOk<FundWinLossRatioReadModel>>();
        winLoss.Value!.WinLossRatio.Should().Be(1.5);
        winLoss.Value.KellyCriteria.Should().BeApproximately(2.0 / 3.0, 0.000001);
        drawdown.Should().BeOfType<ServiceOk<FundDrawdownBalancesReadModel>>();
        drawdown.Value.Should().Be(new FundDrawdownBalancesReadModel(fund.FundId, 1_000m, 1_200m));
        maxProfit.Should().BeOfType<ServiceOk<FundMaxProfitGeneratedReadModel>>();
        maxProfit.Value.Should().BeEquivalentTo(new
        {
            fund.FundId,
            TradeDate = EndDate,
            FundBalance = 1_000m,
            FundProfitOrders = profits,
            FundLossOrders = losses,
            FundDrawdownBalances = new FundDrawdownBalancesReadModel(fund.FundId, 1_000m, 1_300m)
        });
    }

    [Fact]
    public async Task DirectQueryFailuresReturnTheCorrespondingTypedErrorCode()
    {
        var exception = new InvalidOperationException("fund database unavailable");
        var fundId = SampleData.Fund.FundId;
        var orderId = SampleData.FundOrder.OrderId;

        var cases = new (Action<IFundDbContext> Arrange, Func<ActorFundQueryApi, Task<ServiceResult>> Act, int ErrorId)[]
        {
            (db => db.GetFundsAsync().Returns(_ => Task.FromException<ICollection<FundReadModel>>(exception)),
                async api => await api.GetFundsAsync(), GetFundsQuery.ErrorId),
            (db => db.GetFundOrdersAsync().Returns(_ => Task.FromException<ICollection<FundOrderReadModel>>(exception)),
                async api => await api.GetFundOrdersAsync(), GetFundOrdersQuery.ErrorId),
            (db => db.GetFundOrderTradesAsync().Returns(_ => Task.FromException<ICollection<FundOrderTradeReadModel>>(exception)),
                async api => await api.GetFundOrderTradesAsync(), GetFundOrderTradesQuery.ErrorId),
            (db => db.GetFundTransactionsAsync(fundId, StartDate, EndDate)
                    .Returns(_ => Task.FromException<ICollection<FundTransactionReadModel>>(exception)),
                async api => await api.GetFundTransactionsAsync(fundId, StartDate, EndDate),
                GetFundTransactionsQuery.ErrorId),
            (db => db.GetFundBalanceAsync(fundId).Returns(_ => Task.FromException<decimal>(exception)),
                async api => await api.GetFundBalanceAsync(fundId), GetFundBalanceQuery.ErrorId),
            (db => db.GetOpeningFundBalanceAsync(fundId, StartDate).Returns(_ => Task.FromException<decimal>(exception)),
                async api => await api.GetOpeningFundBalanceAsync(fundId, StartDate), GetOpeningFundBalanceQuery.ErrorId),
            (db => db.GetClosingFundBalanceAsync(fundId, EndDate).Returns(_ => Task.FromException<decimal>(exception)),
                async api => await api.GetClosingFundBalanceAsync(fundId, EndDate), GetClosingFundBalanceQuery.ErrorId),
            (db => db.GetFundLossOrdersAsync(fundId, StartDate, EndDate)
                    .Returns(_ => Task.FromException<ICollection<FundOrderAmountReadModel>>(exception)),
                async api => await api.GetFundPnlReportAsync(fundId, StartDate, EndDate), GetFundPnlReportQuery.ErrorId),
            (db => db.GetFundIdFromOrderIdAsync(orderId).Returns(_ => Task.FromException<int>(exception)),
                async api => await api.GetFundIdFromOrderIdAsync(orderId), GetFundIdFromOrderIdQuery.ErrorId),
            (db => db.GetFundLossOrdersAsync(fundId, StartDate, EndDate)
                    .Returns(_ => Task.FromException<ICollection<FundOrderAmountReadModel>>(exception)),
                async api => await api.GetFundWinLossRatioAsync(fundId, StartDate, EndDate),
                GetFundWinLossRatioQuery.ErrorId),
            (db => db.GetFundStartingBalanceAsync(fundId, StartDate).Returns(_ => Task.FromException<decimal>(exception)),
                async api => await api.GetFundDrawdownBalancesAsync(fundId, StartDate, EndDate),
                GetFundDrawdownBalancesQuery.ErrorId),
            (db => db.GetFundBalanceAsync(fundId).Returns(_ => Task.FromException<decimal>(exception)),
                async api => await api.GetFundMaxProfitGeneratedAsync(fundId, EndDate),
                GetFundMaxProfitGeneratedQuery.ErrorId)
        };

        foreach (var (arrange, act, errorId) in cases)
        {
            var (api, db) = CreateApi();
            arrange(db);

            var result = await act(api);

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(errorId);
            result.ErrorMessage.Should().Be(exception.Message);
        }
    }

    static (ActorFundQueryApi Api, IFundDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IFundDbContext>();
        dbFactory.FundDb.Returns(db);
        return (new ActorFundQueryApi(dbFactory), db);
    }
}
