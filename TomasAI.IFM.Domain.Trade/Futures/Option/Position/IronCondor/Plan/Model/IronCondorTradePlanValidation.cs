using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
public static class IronCondorTradePlanValidation
{
    public static void Validate(StrategyPositionSnapshot position, TradePlanParameters parameters, DateOnly valueDate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();
        if (!position.Id.IsValid || position.StrategyKind != TradeStrategyKind.IronCondor || position.Legs.Length != 4 || position.PositionSequence < 1 || position.RouteGeneration < 1 || valueDate == default || nowUtc.Kind != DateTimeKind.Utc || position.AsOfUtc == default || position.AsOfUtc.Kind != DateTimeKind.Utc || position.Legs.Any(static leg => leg.TradeLegId == Guid.Empty || leg.SignedQuantity == 0 || string.IsNullOrWhiteSpace(leg.ContractId) || leg.CurrentPrice <= 0) || position.Legs.Select(static leg => leg.TradeLegId).Distinct().Count() != 4)
            throw new ArgumentException("A valid four-leg Iron Condor position snapshot is required.",nameof(position));
    }
}
