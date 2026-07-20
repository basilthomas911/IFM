using System;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.OptionPricerDb;

public static class SampleData
{
    public static OptionPricerDeviceReadModel OptionPricerDevice => new OptionPricerDeviceReadModel(
        deviceId: 1,
        deviceName: "Sample Device",
        spreadPaths: 100,
        volatilityPaths: 100,
        maxBatchSize: 50,
        optionType: OptionType.Call,
        enabled: true
    );

    public static SpreadDistributionReadModel SpreadDistribution1 => new SpreadDistributionReadModel(
        id: 1,
        tradeId: 1,
        valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
        tradeType: TradeType.PutCreditSpread,
        tradeStatus: TradeStatus.Open,
        daysToExpiry: 30,
        forwardPrice: 100.0,
        lossProbability: 0.1,
        lossThreshold: 10.0m,
        lossThresholdCount: 1,
        shortVolatility: 0.2,
        longVolatility: 0.3,
        forwardLossRatio: 0.05,
        createdOn: DateTime.UtcNow
    );

    public static SpreadDistributionReadModel SpreadDistribution2 => new SpreadDistributionReadModel(
        id: 2,
        tradeId: 1,
        valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
        tradeType: TradeType.PutCreditSpread,
        tradeStatus: TradeStatus.Open,
        daysToExpiry: 30,
        forwardPrice: 100.0,
        lossProbability: 0.1,
        lossThreshold: 10.0m,
        lossThresholdCount: 1,
        shortVolatility: 0.2,
        longVolatility: 0.3,
        forwardLossRatio: 0.05,
        createdOn: DateTime.UtcNow
    );

    public static SpreadDistributionJobReadModel SpreadDistributionJob => new SpreadDistributionJobReadModel(
    orderId: 1,
    tradeId: 1,
    tradeType: TradeType.PutCreditSpread,
    tradeStatus: TradeStatus.Open,
    valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
    daysToExpiry: 30,
    jobSubmitted: DateTime.UtcNow,
    jobStatus: SpreadDistributionJobStatus.InProgress,
    jobCompleted: null,
    jobFailed: null,
    inProgress: true,
    lossProbabilityFactor: 0.1
);
}
