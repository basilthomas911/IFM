using TomasAI.IFM.Domain.Trade.Shared.Model;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>Deterministic validation shared by lifecycle command handlers.</summary>
public static class TradeValidation
{
    public static string[] Validate(this TradeOrderDefinition? order)
    {
        if (order is null) return ["Order is required."];
        List<string> errors = [];
        if (!order.Id.IsValid) errors.Add("PortfolioId, FundId, OrderId and TradeId must be greater than zero.");
        if (order.Revision < 1) errors.Add("Order revision must be greater than zero.");
        if (order.ValueDate == default) errors.Add("ValueDate is required.");
        if (order.ValidUntilUtc.Kind != DateTimeKind.Utc) errors.Add("ValidUntilUtc must be UTC.");
        if (order.Components.Length == 0) errors.Add("At least one component is required.");

        HashSet<Guid> components = [];
        HashSet<Guid> legs = [];
        HashSet<int> tradeIds = [];
        foreach (var component in order.Components)
        {
            if (component.ComponentId == Guid.Empty) errors.Add("ComponentId cannot be empty.");
            else if (!components.Add(component.ComponentId)) errors.Add($"Duplicate ComponentId {component.ComponentId}.");
            if (component.ReservedTradeId <= 0) errors.Add("ReservedTradeId must be greater than zero.");
            else if (!tradeIds.Add(component.ReservedTradeId)) errors.Add($"Duplicate ReservedTradeId {component.ReservedTradeId}.");
            if (component.StrategyKind == TradeStrategyKind.Unknown) errors.Add("StrategyKind is required.");
            if (component.Legs.Length == 0) errors.Add($"Component {component.ComponentId} requires at least one leg.");
            ValidateTopology(component, errors);
            foreach (var leg in component.Legs)
            {
                if (leg.TradeLegId == Guid.Empty) errors.Add("TradeLegId cannot be empty.");
                else if (!legs.Add(leg.TradeLegId)) errors.Add($"Duplicate TradeLegId {leg.TradeLegId}.");
                if (leg.MarketInstrumentId == 0) errors.Add($"Leg {leg.TradeLegId} requires MarketInstrumentId.");
                if (leg.AssetFamily == TradeAssetFamily.Unknown) errors.Add($"Leg {leg.TradeLegId} requires AssetFamily.");
                if (leg.SignedQuantity == 0) errors.Add($"Leg {leg.TradeLegId} quantity cannot be zero.");
            }
        }
        return [.. errors];
    }

    static void ValidateTopology(TradeOrderComponentDefinition component, List<string> errors)
    {
        var expected = component.StrategyKind switch
        {
            TradeStrategyKind.FuturesOutright or TradeStrategyKind.SingleOption => 1,
            TradeStrategyKind.VerticalSpread => 2,
            TradeStrategyKind.IronCondor => 4,
            _ => component.Legs.Length
        };
        if (component.Legs.Length != expected)
            errors.Add($"{component.StrategyKind} requires exactly {expected} leg(s).");
        if (component.StrategyKind == TradeStrategyKind.FuturesOutright &&
            component.Legs.Any(static leg => leg.AssetFamily != TradeAssetFamily.Futures))
            errors.Add("FuturesOutright can contain only Futures legs.");
        if (component.StrategyKind is TradeStrategyKind.SingleOption or TradeStrategyKind.VerticalSpread or TradeStrategyKind.IronCondor &&
            component.Legs.Any(static leg => leg.AssetFamily != TradeAssetFamily.FuturesOption))
            errors.Add($"{component.StrategyKind} can contain only FuturesOption legs.");
    }
}

