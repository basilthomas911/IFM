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
        var definitions = trade.Legs.ToDictionary(leg => leg.TradeLegId);
        // Older emulator execution evidence may carry multiplier 1. Never
        // overwrite a captured, provider-qualified position multiplier with it.
        var captured = position.ContractCashMultiplier ?? StrategyPositionLegOptionData.CommonContractMultiplier(position);
        return Apply(position with
        {
            Legs = position.Legs.Select(leg => definitions.TryGetValue(leg.TradeLegId, out var definition)
                ? leg with { CashMultiplier = captured ?? definition.CashMultiplier } : leg).ToArray(),
            ContractCashMultiplier = captured,
            OpeningCommission = trade.OpeningCommission,
            OpeningValueDate = trade.TradeDate
        });
    }

    public static StrategyPositionSnapshot Apply(StrategyPositionSnapshot position)
    {
        ArgumentNullException.ThrowIfNull(position);
        var multipliers = position.Legs.Select(leg => StrategyPositionLegOptionData.ContractMultiplier(leg)
            ?? position.ContractCashMultiplier ?? leg.CashMultiplier).Distinct().ToArray();
        var multiplier = multipliers.Length == 1 && multipliers[0] is > 0
            ? multipliers[0] : null;
        if (multiplier is not > 0 || position.OpeningCommission is null or < 0 || position.OpeningValueDate is null)
            return position with { DailyPnl = null };
        var commission = position.OpeningValueDate == position.ValueDate ? position.OpeningCommission.Value : 0m;
        commission += position.ClosingFills.Where(fill =>
            TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.TryGet(new DateTimeOffset(fill.FilledAtUtc), out var date)
            && date == position.ValueDate).Sum(fill => fill.Commission);
        return position with { ContractCashMultiplier = multiplier,
            DailyPnl = checked(position.DailyPnlPoints * multiplier.Value - commission) };
    }
}
