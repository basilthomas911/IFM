using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.BDDTests;

public static class SampleData
{
    public static FundReadModel Fund => new (
        fundId: 1234,
        name: "TestFund",
        description: "A Test Fund",
        balance: 1056.78m,
        isProduction: false,
        createdOn: new DateTime(2020, 10, 10),
        createdBy: "basilt"
    );

    public static FundOrderReadModel FundOrder => new (
        fundId: 1234,
        orderId: 1234,
        orderDate: new DateTime(2020, 10, 10),
        orderStatus: Shared.OrderStatus.Open,
        baseContractId: "ES20220617",
        tradeDate: new DateOnly(2020, 10, 10),
        maturityDate: new DateOnly(2020, 10, 10),
        reference: "The Rain In Spain",
        createdOn: new DateTime(2020, 10, 10),
        createdBy: "basilt",
        updatedOn: new DateTime(2020, 10, 10),
        updatedBy: "basilt"
    );

    public static FundOrderReadModel FundOrderWithNonExistingFundId => FundOrder with { FundId = 9999 };

    public static FundOrderId FundOrderIdWithNonExistingFundId => new FundOrderId(9999, FundOrder.OrderId);

    public static FundOrderId FundOrderIdWithNonExistingOrderId => new FundOrderId(FundOrder.FundId, 9999);

    public static FundOrderReadModel FundOrderWithClosedOrderStatus => FundOrder with { OrderStatus = Shared.OrderStatus.Closed };

    public static FundOrderTradeReadModel FundOrderTrade => new (
        fundId: 1234,
        orderId: 1234,
        tradeId: 4567,
        tradeType: TradeType.ShortIronCondor,
        tradeDate: new DateOnly(2020, 10, 10),
        maturityDate: new DateOnly (2020,10,20),
        tradeState: TradeState.NewTrade,
        tradeAction: TradeAction.Sell,
        reference: string.Empty,
        primaryTrade: true,
        baseContractSymbol: "ES",
        createdOn: new DateTime(2020, 10, 10),
        createdBy: "basilt",
        updatedOn: new DateTime(2020, 10, 10),
        updatedBy: "basilt"
    );

    public static FundOrderTradeReadModel FundOrderTradeWithNonExistingFundId => FundOrderTrade with { FundId = 9999 };

    public static FundOrderTradeReadModel FundOrderTradeWithNonExistingOrderId => FundOrderTrade with { OrderId = 9999 };

    public static FundOrderTradeReadModel FundOrderTradeWithMinimumTradeDate => FundOrderTrade with { TradeDate = DateOnly.MinValue };

    public static FundOrderTradeId FundOrderTradeIdWithNonExistingFundId => new FundOrderTradeId(9999, FundOrderTrade.OrderId, FundOrderTrade.TradeId);

    public static FundTransactionReadModel FundTransaction => new (
        transactionId: 0,
        transactionDate: new DateTime(2021, 03, 01),
        transactionType: FundTransactionType.OpeningTrade,
        fundId: 1234,
        orderId: 2345,
        tradeId: 3456,
        tradeType: TradeType.PutCreditSpread,
        valueDate: new DateOnly(2021, 03, 01),
        tradeStatus: TradeStatus.Open,
        description: string.Empty,
        amount: 1000.0M,
        balance: 100.0M);

}
