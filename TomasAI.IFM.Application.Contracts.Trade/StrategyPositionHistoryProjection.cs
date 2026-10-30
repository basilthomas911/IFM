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
            trade.Legs.Select(l => Math.Abs(l.SignedQuantity)).DefaultIfEmpty(0).Min(), trade.OpeningValue, trade.OpeningCommission) };
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
        return position with { StrategyKind = trade.StrategyKind, HistoryValuation = new(date, expiry.DayNumber - date.DayNumber, quantity,
            position.Phase == StrategyPositionPhase.Open ? trade.OpeningValue
                : quantity > 0 ? position.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity) / quantity : 0m,
            position.Phase == StrategyPositionPhase.Open ? trade.OpeningCommission : 0m) };
    }
}
