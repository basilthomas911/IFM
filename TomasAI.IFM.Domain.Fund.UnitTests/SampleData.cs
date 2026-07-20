using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Event.Actor;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public static class SampleData
{
    public static FundTransactionReadModel FundTransaction => new(
            transactionId: 1,
            transactionDate: DateTime.UtcNow,
            transactionType: FundTransactionType.OpeningTrade,
            fundId: 1234,
            orderId: 456,
            tradeId: 1,
            tradeType: TradeType.CallCreditSpread,
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            tradeStatus: TradeStatus.Open,
            description: "Opening trade",
            amount: 500m,
            balance: 0m
        );
    public static FundReadModel NewFund => new (
            fundId: 1234,
            name: "TestFund",
            description: "Test Fund Description",
            balance: 100000.00m,
            isProduction: false,
            createdOn: DateTime.UtcNow,
            createdBy: "tester"
        );

    public static FundOrderReadModel FundOrder => new (
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

    public static FundOrderTradeReadModel FundOrderTrade => new (
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

    public static FundReadModel Fund => new (
        1, 
        "Test", 
        "desc", 
        100m, 
        false, 
        DateTime.UtcNow, "tester");

    public static FundMaxProfitGeneratedEvent FundMaxProfitGeneratedEvent => new ()
    {
        Subject = new ActorSubject(
            ActorType.Event,
            FundEventActor.Actor,
            FundMaxProfitGeneratedEvent.Verb,
            "1"),
        Id = Guid.NewGuid(),
        CommandId = Guid.NewGuid(),
        ReceivedOn = DateTime.UtcNow,
        EventId = 1,
        AggregateId = string.Empty,
        EventSource = "unit-test",
        FundOrder = FundOrder,
        FundMaxProfit = new FundMaxProfitReadModel(SampleData.FundOrder.Id, 0m, 0.0)
    };

}
