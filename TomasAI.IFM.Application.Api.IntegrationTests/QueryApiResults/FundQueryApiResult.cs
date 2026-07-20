using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class FundQueryApiResult
{
    public static Task FromGetFundsAsync(HttpResponse resp)
        => resp.SetResult(new FundReadModel[]
        {
            new FundReadModel(
                fundId: 1,
                name: "Sample Fund",
                description: "Sample Description",
                balance: 1000000m,
                isProduction: true,
                createdOn: DateTime.UtcNow,
                createdBy: "UnitTest"
            )
        });

    public static Task FromGetClosingFundBalanceAsync(HttpResponse resp)
        => resp.SetResult(new FundBalanceReadModel(1000000m));

    public static Task FromGetFundBalanceAsync(HttpResponse resp)
        => resp.SetResult(new FundBalanceReadModel(1000000m));

    public static Task FromGetFundDrawdownBalancesAsync(HttpResponse resp)
        => resp.SetResult(new FundDrawdownBalancesReadModel(
            FundId: 1,
            StartBalance: 900000m,
            EndBalance: 1000000m
        ));

    public static Task FromGetFundIdFromOrderIdAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<int>(1));

    public static Task FromGetFundOrdersAsync(HttpResponse resp)
        => resp.SetResult(new FundOrderReadModel[]
        {
            new FundOrderReadModel(
                fundId: 1,
                orderId: 1,
                orderDate: DateTime.UtcNow,
                orderStatus: Domain.Fund.Shared.OrderStatus.Open,
                baseContractId: "AAPL",
                tradeDate: new DateOnly(2020, 1, 1),
                maturityDate: new DateOnly(2020, 12, 31),
                reference: "Ref1",
                createdOn: DateTime.UtcNow,
                createdBy: "UnitTest",
                updatedOn: null,
                updatedBy: null
            )
        });

    public static Task FromGetFundOrderTradesAsync(HttpResponse resp)
        => resp.SetResult(new FundOrderTradeReadModel[]
        {
            new FundOrderTradeReadModel(
                fundId: 1,
                orderId: 1,
                tradeId: 1,
                tradeType: TradeType.LongCall,
                tradeDate: new DateOnly(2020, 1, 1),
                maturityDate: new DateOnly(2020, 12, 31),
                tradeState: TradeState.NewTrade,
                tradeAction: TradeAction.Buy,
                reference: "Ref1",
                primaryTrade: true,
                baseContractSymbol: "AAPL",
                createdOn: DateTime.UtcNow,
                createdBy: "UnitTest",
                updatedOn: null,
                updatedBy: null
            )
        });

    public static Task FromGetFundPnlReportAsync(HttpResponse resp)
        => resp.SetResult(new FundPnlReportReadModel(
            WinRate: 0.6,
            AverageProfit: 10000m,
            LossRate: 0.4,
            AverageLoss: 5000m,
            WinLossRatio: 1.5,
            TargetSharpeRatio: 1.2,
            ActualSharpeRatio: 1.1,
            PnlAmount: 50000m,
            PnlPercent: 5.0,
            TradeCommission: 1000m
        ));

    public static Task FromGetFundTransactionsAsync(HttpResponse resp)
        => resp.SetResult(new FundTransactionReadModel[]
        {
            new FundTransactionReadModel(
                transactionId: 1,
                transactionDate: DateTime.UtcNow,
                transactionType: FundTransactionType.CashDeposit,
                fundId: 1,
                orderId: 0,
                tradeId: 0,
                tradeType: TradeType.Unknown,
                valueDate: new DateOnly(2020, 1, 2),
                tradeStatus: TradeStatus.Open,
                description: "Deposit",
                amount: 10000m,
                balance: 1010000m
            )
        });

    public static Task FromGetFundWinLossRatioAsync(HttpResponse resp)
        => resp.SetResult(new FundWinLossRatioReadModel(
            WinLossRatio: 1.5,
            KellyCriteria: 0.7
        ));

    public static Task FromGetOpeningFundBalanceAsync(HttpResponse resp)
        => resp.SetResult(new FundBalanceReadModel(500000m));
}
