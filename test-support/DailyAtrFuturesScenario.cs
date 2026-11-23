using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Tests.Support;
public static class DailyAtrFuturesScenario
{
    public static readonly DateTime At = new(2026,11,19,15,0,0,DateTimeKind.Utc);
    public static readonly DateOnly Date = new(2026,11,19);
    public const string Contract = "ES20261218";
    public static DailyFuturesEntryState Arm => new() { DcTriggerId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ContractId = Contract, ValueDate = Date, DcAtUtc = At, DcPrice = 100, Threshold = 8,
        Atr = new(Contract,8,At.AddMinutes(-1),At.AddSeconds(-30),123) };
    public static IntrinsicTimeStrategyWorkflowView View(double price = 103, IntrinsicTimeModeType mode = IntrinsicTimeModeType.Trending) => new()
    {
        EntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(new(Contract,Date,TimeFrameType.Daily)) with
        { Assignment = new(101,701,"Daily","Future","ES",TimeFrameType.Daily,new(CatalogKind(),Guid.NewGuid(),1),1,At.AddDays(-1),null) },
        WorkflowId = StrategyWorkflowId.New(TimeProvider.System), TriggerEventId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(),
        StartedAtUtc = At.AddSeconds(1), ExpiresAtUtc = At.AddMinutes(1),
        DailyFuturesEntry = Arm,
        TriggerEvent = new() { EntityId = new(Contract,Date,TimeFrameType.Daily), CreatedOn = At.AddSeconds(1),
            FuturesItiSignal = new() { ContractId = Contract, ValueDate = Date, TimePeriod = TimeFrameType.Daily,
                IntrinsicTime = At.AddSeconds(1), IntrinsicPrice = price, IntrinsicTimeMode = mode, Threshold = 8, DailyAtr = Arm.Atr } }
    };
    static StrategyCatalogKind CatalogKind() => StrategyCatalogKind.Deployment;
    public static StrategyPositionSnapshot Position(int sign = 1, decimal current = 103) => new()
    {
        Id = StrategyPositionId.Create(new(101,701,1601,99901),TradeStrategyKind.FuturesOutright),
        StrategyKind = TradeStrategyKind.FuturesOutright, IsOpen = true, PositionSequence = 1, RouteGeneration = 1,
        Phase = StrategyPositionPhase.MarkToMarket, AsOfUtc = At.AddSeconds(1), LastMarketObservationUtc = At.AddSeconds(1),
        ValueDate = Date, DailyFuturesEntry = Arm with { Consumed = true },
        Legs = [new() { TradeLegId = Guid.Parse("22222222-2222-2222-2222-222222222222"), ContractId = Contract,
            SignedQuantity = sign, OpeningPrice = sign > 0 ? 103 : 97, CurrentPrice = current, LastPriceAtUtc = At.AddSeconds(1) }]
    };
}
