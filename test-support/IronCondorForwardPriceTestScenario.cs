using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using SessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.TestSupport;

/// <summary>Artificial protocol/threshold boundary fixtures; no provider quotes or financial accounts are used.</summary>
/// <remarks>Marks, theoretical values and PnL are independently assigned here to isolate behavior. These are not
/// coherent market valuations. Use IronCondorForwardPriceValuationScenarioTests for price/PnL comparisons.</remarks>
public static class IronCondorForwardPriceTestScenario
{
    public static CalculateIronCondorForwardPriceCommand Request(int quantity = 1, bool negative = false)
    {
        var now = DateTime.UtcNow;
        var date = DateOnly.FromDateTime(now);
        var legs = Enumerable.Range(0,4).Select(i => new IronCondorForwardPriceLeg(Guid.NewGuid(), $"TEST-OPTION-{i}", i >= 2,
            i % 2 == 0 ? -quantity : quantity, i % 2 != 0 ? 10 : negative ? (i == 0 ? 11 : 110) : 25,
            negative ? .9 : .4, i % 2 == 0 ? 25m : 10m, 50m, "CAD")).ToArray();
        var position = new StrategyPositionSnapshot {
            Id = StrategyPositionId.Create(new TradeEntityId(1,2,3,Random.Shared.Next(1000000,9000000)), TradeStrategyKind.IronCondor),
            StrategyKind = TradeStrategyKind.IronCondor, ValueDate = date, PositionSequence = 1, RouteGeneration = 1,
            IsOpen = true, AsOfUtc = now, DailyPnl = 2, UnrealizedPnl = 2,
            Legs = legs.Select(l => new StrategyPositionLeg { TradeLegId = l.TradeLegId, ContractId = l.ContractId,
                PutCall = (byte)(l.IsCall ? 1 : 2), SignedQuantity = l.SignedQuantity, OpeningPrice = l.MarkPrice,
                CurrentPrice = l.MarkPrice, LastPriceAtUtc = now, AssetFamily = TradeAssetFamily.FuturesOption }).ToArray() };
        var id = new SessionId(position.Id,date);
        return new() { CommandId = Guid.NewGuid(), EntityId = id, Position = position,
            Subject = new(ActorType.Function, CalculateIronCondorForwardPriceCommand.Actor, CalculateIronCondorForwardPriceCommand.Verb,id.Format()),
            SourceEventId = Guid.NewGuid(), RequestedAtUtc = now,
            IronCondorForwardPriceInputs = new() { TradeDate = negative ? date : date.AddDays(-30), MaturityDate = date.AddDays(10), OptionLegs = legs } };
    }

    public static UpdateIronCondorTradePlanCommand PlanRequest(CalculateIronCondorForwardPriceCommand request, IronCondorForwardPriceCalculatedEvent? completed, string? failure = null)
        => new() { CommandId = Guid.NewGuid(), EntityId = request.EntityId, Position = request.Position,
            Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor, UpdateIronCondorTradePlanCommand.Verb,request.EntityId.Format()),
            SourceEventId = request.SourceEventId, RequestedAtUtc = DateTime.UtcNow, UseForwardPriceFunction = true,
            IronCondorForwardPriceCalculated = completed, ForwardPriceFailure = failure,
            IronCondorTradePlanInputs = new() { TradeDate = request.IronCondorForwardPriceInputs.TradeDate, MaturityDate = request.IronCondorForwardPriceInputs.MaturityDate,
                TradeLimits = new() { TradeId = request.Position.Id.Trade.TradeId, MaxLoss = -1000m },
                DailyRiskInputs = new() { ContractMultiplier = 50m, Currency = "CAD" } } };
}
