using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
public static class FuturesTradePlanValidation
{
    public static void Validate(StrategyPositionSnapshot position, TradePlanParameters parameters, DateOnly valueDate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(position);
        parameters.Validate();
        if (!position.Id.IsValid || position.StrategyKind != TradeStrategyKind.FuturesOutright || position.Legs.Length != 1 || position.PositionSequence < 1 || position.RouteGeneration < 1 || valueDate == default || nowUtc.Kind != DateTimeKind.Utc || position.AsOfUtc.Kind != DateTimeKind.Utc || position.Legs.Any(static x => x.TradeLegId == Guid.Empty || x.SignedQuantity == 0 || string.IsNullOrWhiteSpace(x.ContractId) || x.CurrentPrice <= 0))
            throw new ArgumentException("A valid one-leg Futures position snapshot is required.",nameof(position));
    }
}
