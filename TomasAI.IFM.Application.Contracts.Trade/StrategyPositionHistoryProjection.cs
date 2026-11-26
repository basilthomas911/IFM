namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Backend projection helpers shared by position queries and server realtime publication.</summary>
public static class StrategyPositionHistoryProjection
{
    public static EstablishedTradeDefinition EnrichTrade(EstablishedTradeDefinition trade)
    {
        ArgumentNullException.ThrowIfNull(trade);
        if (trade.TradeDate is not { } date || trade.MaturityDate is not { } expiry) return trade;
        StrategyPositionSnapshot? opening = null;
        if (trade.Legs.Length > 0 && trade.Legs.All(leg => trade.OriginalFills.Any(fill => fill.TradeLegId == leg.TradeLegId)))
        {
            opening = new() { Id = StrategyPositionId.Create(trade.Id, trade.StrategyKind), StrategyKind = trade.StrategyKind,
                Phase = StrategyPositionPhase.Open, ValueDate = date, AsOfUtc = trade.EstablishedAtUtc,
                Legs = trade.Legs.Select(leg => {
                    var fills = trade.OriginalFills.Where(fill => fill.TradeLegId == leg.TradeLegId).ToArray();
                    var quantity = fills.Sum(fill => Math.Abs(fill.SignedQuantity));
                    if (quantity <= 0) throw new InvalidOperationException("Opening execution evidence has no filled quantity.");
                    var price = fills.Sum(fill => fill.Price * Math.Abs(fill.SignedQuantity)) / quantity;
                    return new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                        SignedQuantity = leg.SignedQuantity, CurrentPrice = price, OpeningPrice = price };
                }).ToArray() };
            opening = EnrichPosition(opening, trade);
        }
        return trade with { OpeningPositionSnapshot = opening, OpeningHistoryValuation = new(date, expiry.DayNumber - date.DayNumber,
            trade.Legs.Select(l => Math.Abs(l.SignedQuantity)).DefaultIfEmpty(0).Min(), trade.OpeningValue, trade.OpeningCommission) {
            RealizedPnl = opening?.HistoryValuation?.RealizedPnl,
            TotalCommission = opening?.HistoryValuation?.TotalCommission,
            NetPnl = opening?.HistoryValuation?.NetPnl,
            PeriodPnl = opening?.HistoryValuation?.NetPnl } };
    }

    /// <summary>Values the complete chronological history as non-overlapping net changes, including commissions exactly once.</summary>
    public static StrategyPositionSnapshot[] EnrichHistory(IEnumerable<StrategyPositionSnapshot> history, EstablishedTradeDefinition trade)
    {
        var valued = history.Select(position => EnrichPosition(position, trade)).ToArray();
        decimal? previous = EnrichTrade(trade).OpeningHistoryValuation?.NetPnl;
        static int Compare(StrategyPositionSnapshot left, StrategyPositionSnapshot right)
        {
            var value = left.ValueDate.CompareTo(right.ValueDate);
            if (value == 0) value = left.AsOfUtc.CompareTo(right.AsOfUtc);
            return value != 0 ? value : left.PositionSequence.CompareTo(right.PositionSequence);
        }
        int[]? order = null;
        for (var index = 1; index < valued.Length; index++)
            if (Compare(valued[index - 1], valued[index]) > 0)
            {
                order = Enumerable.Range(0, valued.Length).ToArray();
                Array.Sort(order, (left, right) => Compare(valued[left], valued[right]) is var comparison && comparison != 0 ? comparison : left.CompareTo(right));
                break;
            }
        for (var ordinal = 0; ordinal < valued.Length; ordinal++) {
            var index = order is null ? ordinal : order[ordinal];
            var position = valued[index];
            var current = position.HistoryValuation?.NetPnl;
            var change = position.Phase == StrategyPositionPhase.Open ? current
                : current is not null && previous is not null ? current - previous : null;
            if (position.HistoryValuation is { } valuation && valuation.PeriodPnl != change)
                valued[index] = position with { HistoryValuation = valuation with { PeriodPnl=change } };
            previous = current;
        }
        return valued;
    }

    public static StrategyPositionSnapshot EnrichPosition(StrategyPositionSnapshot position, EstablishedTradeDefinition trade)
    {
        ArgumentNullException.ThrowIfNull(position); ArgumentNullException.ThrowIfNull(trade);
        if (position.Id.Trade != trade.Id || position.StrategyKind != TradeStrategyKind.Unknown && position.StrategyKind != trade.StrategyKind)
            throw new ArgumentException("Position and established trade identities must match.");
        position = StrategyPositionCurrencyPnl.Apply(position, trade);
        if (trade.MaturityDate is not { } expiry) return position;
        var date = position.ValueDate != default ? position.ValueDate
            : TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetOperational(new DateTimeOffset(DateTime.SpecifyKind(position.AsOfUtc, DateTimeKind.Utc)));
        var quantity = position.Legs.Select(leg => Math.Abs(leg.SignedQuantity)).DefaultIfEmpty(0).Min();
        // Point P&L is already quantity-weighted. Convert once using captured
        // contract evidence, and keep lifetime commissions independent of sessions.
        var multiplier = position.ContractCashMultiplier;
        decimal? realized = multiplier is > 0 ? checked(position.RealizedPnl * multiplier.Value) : null;
        decimal? totalCommission = position.OpeningCommission is >= 0 && position.ClosingFills.All(fill => fill.Commission >= 0)
            ? checked(position.OpeningCommission.Value + position.ClosingFills.Sum(fill => fill.Commission)) : null;
        decimal? net = multiplier is > 0 && totalCommission is not null
            ? checked((position.RealizedPnl + position.UnrealizedPnl) * multiplier.Value - totalCommission.Value) : null;
        return position with { StrategyKind = trade.StrategyKind, HistoryValuation = new(date, expiry.DayNumber - date.DayNumber, quantity,
            position.Phase == StrategyPositionPhase.Open ? trade.OpeningValue
                : quantity > 0 ? position.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity) / quantity : 0m,
            position.Phase == StrategyPositionPhase.Open ? trade.OpeningCommission
                : position.Phase == StrategyPositionPhase.Close ? position.ClosingFills.Sum(fill => fill.Commission) : 0m) {
            RealizedPnl = realized, TotalCommission = totalCommission, NetPnl = net } };
    }
}
