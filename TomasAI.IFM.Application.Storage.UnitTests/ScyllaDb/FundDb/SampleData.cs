using System;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.FundDb;

public static class SampleData
{
    public static FundReadModel Fund => new (
        fundId: 1,
        name: "Sample Fund",
        description: "This is a sample fund.",
        balance: 1000000.00m,
        isProduction: true,
        createdOn: new DateTime(2025,01,10),
        createdBy: "Admin"
    );

    public static FundOrderReadModel FundOrder => new (
        fundId: 1,
        orderId: 1,
        orderDate: DateTime.Now,
        orderStatus: Domain.Fund.Shared.OrderStatus.Open,
        baseContractId: "Contract A",
        tradeDate: DateOnly.FromDateTime(DateTime.Now.Date),
        maturityDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(6).Date),
        reference: "Ref A",
        createdOn: DateTime.Now,
        createdBy: "Admin",
        updatedOn: DateTime.Now,
        updatedBy: "Admin"
    );

    public static FundOrderTradeReadModel FundOrderTrade => new (
        fundId: 1,
        orderId: 1,
        tradeId: 1,
        tradeType: TradeType.LongIronCondor,
        tradeDate: DateOnly.FromDateTime(DateTime.Now.Date),
        maturityDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(6).Date),
        tradeState: TradeState.NewTrade,
        tradeAction: TradeAction.Buy,
        reference: "Trade Ref A",
        primaryTrade: true,
        baseContractSymbol: "Symbol A",
        createdOn: DateTime.Now,
        createdBy: "Admin",
        updatedOn: DateTime.Now,
        updatedBy: "Admin"
    );

    public static FundTransactionReadModel FundTransaction => new (
        transactionId: 1,
        transactionDate: DateTime.Now,
        transactionType: FundTransactionType.OpeningTrade,
        fundId: 1,
        orderId: 1,
        tradeId: 1,
        tradeType: TradeType.LongIronCondor,
        valueDate: DateOnly.FromDateTime(DateTime.Now),
        tradeStatus: TradeStatus.Open,
        description: "Transaction A",
        amount: 10000.00m,
        balance: 1000000.00m
    );

    public static FundPnlReadModel FundPnl => new (
        fundId: 1,
        valueDate: DateOnly.FromDateTime(DateTime.Now),
        orderId: 1,
        tradeId: 1,
        tradeType: TradeType.LongIronCondor,
        pnl: 5000.00m
    );

    public static FundOrderAmountReadModel FundOrderAmount => new (
        fundId: 1,
        valueDate: DateOnly.FromDateTime(DateTime.Now.Date),
        orderId: 1,
        amount: 10000.00m
    );

    public static FundDailyBalanceReadModel FundDailyBalance => new (
        fundId: 1,
        valueDate: DateOnly.FromDateTime(DateTime.Now.Date),
        balance: 1000000.00m
    );

    public static FundDrawdownBalancesReadModel FundDrawdownBalances => new (
        FundId: 1,
        StartBalance: 1000000.00m,
        EndBalance: 950000.00m
    );
}
