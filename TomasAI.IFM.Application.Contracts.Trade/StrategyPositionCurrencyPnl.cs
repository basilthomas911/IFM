namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Converts explicit price-point evidence to currency once, independently of a loaded trade plan.</summary>
public static class StrategyPositionCurrencyPnl
{
    public static StrategyPositionSnapshot Apply(StrategyPositionSnapshot position, EstablishedTradeDefinition trade)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(trade);
        if (position.Id.Trade != trade.Id)
            throw new ArgumentException("Position and currency evidence must identify the same trade.");
        for (var first = 0; first < trade.Legs.Length; first++)
            for (var next = first + 1; next < trade.Legs.Length; next++)
                if (trade.Legs[first].TradeLegId == trade.Legs[next].TradeLegId)
                    throw new ArgumentException("Trade leg identities must be unique.", nameof(trade));
        // Older emulator execution evidence may carry multiplier 1. Never
        // overwrite a captured, provider-qualified position multiplier with it.
        var captured = position.ContractCashMultiplier ?? StrategyPositionLegOptionData.CommonContractMultiplier(position);
        StrategyPositionLeg[]? changed = null;
        for (var index = 0; index < position.Legs.Length; index++)
        {
            var leg = position.Legs[index];
            foreach (var definition in trade.Legs)
            {
                if (definition.TradeLegId != leg.TradeLegId) continue;
                var multiplier = captured ?? definition.CashMultiplier;
                if (leg.CashMultiplier != multiplier)
                {
                    changed ??= (StrategyPositionLeg[])position.Legs.Clone();
                    changed[index] = leg with { CashMultiplier = multiplier };
                }
                break;
            }
        }
        if (changed is not null || position.ContractCashMultiplier != captured
            || position.OpeningCommission != trade.OpeningCommission || position.OpeningValueDate != trade.TradeDate)
            position = position with { Legs = changed ?? position.Legs, ContractCashMultiplier = captured,
                OpeningCommission = trade.OpeningCommission, OpeningValueDate = trade.TradeDate };
        return Apply(position);
    }

    public static StrategyPositionSnapshot Apply(StrategyPositionSnapshot position)
    {
        ArgumentNullException.ThrowIfNull(position);
        decimal? multiplier = null;
        for (var index = 0; index < position.Legs.Length; index++)
        {
            var leg = position.Legs[index];
            var current = StrategyPositionLegOptionData.ContractMultiplier(leg) ?? position.ContractCashMultiplier ?? leg.CashMultiplier;
            if (index == 0) multiplier = current;
            else if (current != multiplier) { multiplier = null; break; }
        }
        if (multiplier is not > 0 || position.OpeningCommission is null or < 0 || position.OpeningValueDate is null)
            return position.DailyPnl is null ? position : position with { DailyPnl = null };
        var commission = position.OpeningValueDate == position.ValueDate ? position.OpeningCommission.Value : 0m;
        foreach (var fill in position.ClosingFills)
            if (TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.TryGet(new DateTimeOffset(fill.FilledAtUtc), out var date)
                && date == position.ValueDate) commission += fill.Commission;
        var pnl = checked(position.DailyPnlPoints * multiplier.Value - commission);
        return position.ContractCashMultiplier == multiplier && position.DailyPnl == pnl ? position
            : position with { ContractCashMultiplier = multiplier, DailyPnl = pnl };
    }
}
