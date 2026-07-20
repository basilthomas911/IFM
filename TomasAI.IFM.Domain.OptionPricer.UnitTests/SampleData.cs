using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests;

public static class SampleData
{
    public static SpreadDistributionReadModel PutSpreadDistribution => new(
        id: 1001,
        tradeId: 2345,
        valueDate: new DateOnly(2021, 10, 10),
        tradeType: TradeType.PutCreditSpread,
        tradeStatus: TradeStatus.Open,
        daysToExpiry: 10,
        forwardPrice: 10.45,
        lossProbability: 0.69,
        lossThreshold: 0m,
        lossThresholdCount: 0,
        shortVolatility: 0.25,
        longVolatility: 0.36,
        forwardLossRatio: 0.52,
        createdOn: new DateTime(2021, 10, 9)
    );

    public static SpreadDistributionReadModel CallSpreadDistribution => new(
        id: 1002,
        tradeId: 2345,
        valueDate: new DateOnly(2021, 10, 10),
        tradeType: TradeType.CallCreditSpread,
        tradeStatus: TradeStatus.Open,
        daysToExpiry: 10,
        forwardPrice: 11.20,
        lossProbability: 0.55,
        lossThreshold: 0m,
        lossThresholdCount: 0,
        shortVolatility: 0.22,
        longVolatility: 0.30,
        forwardLossRatio: 0.48,
        createdOn: new DateTime(2021, 10, 9)
    );

    // ─── SpreadDistributionJob Sample Data ──────────────────────────────

    public static SpreadDistributionJobEntityId SpreadDistributionJobEntityId => new(OrderId: 100, TradeId: 200, ValueDate: new DateOnly(2024, 1, 1));

    public static SpreadDistributionJobReadModel SpreadDistributionJob => new(
        orderId: 100,
        tradeId: 200,
        tradeType: TradeType.PutCreditSpread,
        tradeStatus: TradeStatus.Open,
        valueDate: new DateOnly(2024, 12, 20),
        daysToExpiry: 30,
        jobSubmitted: new DateTime(2024, 11, 20, 10, 0, 0),
        jobStatus: SpreadDistributionJobStatus.InProgress,
        jobCompleted: null,
        jobFailed: null,
        inProgress: false,
        lossProbabilityFactor: 0.65
    );

    public static SpreadDistributionJobSubmittedEvent SpreadDistributionJobSubmittedEvent => new()
    {
        Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobSubmittedEvent.Actor, SpreadDistributionJobSubmittedEvent.Verb, SpreadDistributionJobEntityId.Format()),
        Id = Guid.NewGuid(),
        CommandId = Guid.NewGuid(),
        EntityId = SpreadDistributionJobEntityId,
        EventId = 1,
        ReceivedOn = DateTime.UtcNow,
        EventSource = "test",
        SpreadDistributionJob = SpreadDistributionJob,
        CreatedBy = "test-user",
        CreatedOn = DateTime.UtcNow
    };
}
