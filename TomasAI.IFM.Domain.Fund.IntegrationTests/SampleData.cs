using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.IntegrationTests;

/// <summary>
/// Provides sample data instances for use in testing or demonstration scenarios.
/// </summary>
/// <remarks>This class supplies pre-populated, read-only view model objects that can be used to facilitate unit
/// tests, UI prototyping, or documentation examples. The sample data is static and does not reflect live or production
/// information.</remarks>
public static  class SampleData
{
    public static FundReadModel NewFund => new (
            fundId: 1234,
            name: "TestFund",
            description: "Test Fund Description",
            balance: 100000.00m,
            isProduction: false,
            createdOn: DateTime.UtcNow,
            createdBy: "tester"
        );

    public static FundOrderReadModel FundOrder => new(
            fundId: 1234,
            orderId: 456,
            orderDate: DateTime.UtcNow,
            orderStatus: Shared.OrderStatus.Open,
            baseContractId: "ESU9",
            tradeDate: DateOnly.FromDateTime(DateTime.UtcNow),
            maturityDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            reference: "ref",
            createdOn: DateTime.UtcNow,
            createdBy: "tester",
            updatedOn: null,
            updatedBy: string.Empty
        );

    public static FundOrderTradeReadModel FundOrderTrade => new(
            fundId: 1234,
            orderId: 456,
            tradeId: 1,
            tradeType: TradeType.CallCreditSpread,
            tradeDate: DateOnly.FromDateTime(DateTime.UtcNow),
            maturityDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            tradeState: TradeState.TradeToOpen,
            tradeAction: TradeAction.Buy,
            reference: "ref",
            primaryTrade: true,
            baseContractSymbol: "ESU9",
            createdOn: DateTime.UtcNow,
            createdBy: "tester",
            updatedOn: null,
            updatedBy: string.Empty
        );

    public static FundTransactionReadModel FundTransaction => new(
        transactionId: 1,
        transactionDate: DateTime.Now,
        transactionType: FundTransactionType.OpeningTrade,
        fundId: 1234,
        orderId: 456,
        tradeId: 1,
        tradeType: TradeType.LongIronCondor,
        valueDate: DateOnly.FromDateTime(DateTime.Now),
        tradeStatus: TradeStatus.Open,
        description: "Transaction A",
        amount: 10000.00m,
        balance: 1000000.00m
    );

    public static FundPnlReadModel FundPnl => new(
        fundId: 1234,
        valueDate: DateOnly.FromDateTime(DateTime.Now),
        orderId: 456,
        tradeId: 1,
        tradeType: TradeType.LongIronCondor,
        pnl: 5000.00m
    );

    public static FundOrderAmountReadModel FundOrderAmount => new(
        fundId: 1234,
        valueDate: DateOnly.FromDateTime(DateTime.Now.Date),
        orderId: 456,
        amount: 10000.00m
    );

    public static FundDailyBalanceReadModel FundDailyBalance => new(
        fundId: 1234,
        valueDate: DateOnly.FromDateTime(DateTime.Now.Date),
        balance: 1000000.00m
    );

    public static FundDrawdownBalancesReadModel FundDrawdownBalances => new(
        FundId: 1234,
        StartBalance: 1000000.00m,
        EndBalance: 950000.00m
    );
}
