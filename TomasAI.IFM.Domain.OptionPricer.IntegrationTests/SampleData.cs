using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.OptionPricer.IntegrationTests;

public static class SampleData
{
    public const int OrderId = 1;
    public const int TradeId = 1001;
    public const int JobId = 1;
    public static readonly DateOnly ValueDate = new(2025, 6, 20);
    public static readonly TradeType PutTradeType = TradeType.PutCreditSpread;
    public static readonly TradeType CallTradeType = TradeType.CallCreditSpread;
    public static readonly TradeStatus TradeStatus = Shared.Trade.TradeStatus.Open;
    public const int DaysToExpiry = 30;

    public static SpreadDistributionReadModel PutSpreadDistribution => new(
        id: 0,
        tradeId: TradeId,
        valueDate: ValueDate,
        tradeType: PutTradeType,
        tradeStatus: TradeStatus,
        daysToExpiry: DaysToExpiry,
        forwardPrice: 5500.0,
        lossProbability: 0.15,
        lossThreshold: 100.0m,
        lossThresholdCount: 5,
        shortVolatility: 0.20,
        longVolatility: 0.18,
        forwardLossRatio: 0.03,
        createdOn: new DateTime(2025, 6, 20, 10, 0, 0));

    public static SpreadDistributionReadModel CallSpreadDistribution => new(
        id: 0,
        tradeId: TradeId,
        valueDate: ValueDate,
        tradeType: CallTradeType,
        tradeStatus: TradeStatus,
        daysToExpiry: DaysToExpiry,
        forwardPrice: 5600.0,
        lossProbability: 0.12,
        lossThreshold: 90.0m,
        lossThresholdCount: 3,
        shortVolatility: 0.22,
        longVolatility: 0.19,
        forwardLossRatio: 0.025,
        createdOn: new DateTime(2025, 6, 20, 10, 0, 0));

    public static SpreadDistributionJobEntityId SpreadDistributionJobEntityId
        => new(OrderId, TradeId, ValueDate);

    public static SpreadDistributionJobReadModel SpreadDistributionJob => new(
        orderId: OrderId,
        tradeId: TradeId,
        tradeType: CallTradeType,
        tradeStatus: TradeStatus,
        valueDate: ValueDate,
        daysToExpiry: DaysToExpiry,
        jobSubmitted: new DateTime(2025, 6, 20, 10, 0, 0),
        jobStatus: SpreadDistributionJobStatus.InProgress,
        jobCompleted: null,
        jobFailed: null,
        inProgress: true,
        lossProbabilityFactor: 0.1);
}
