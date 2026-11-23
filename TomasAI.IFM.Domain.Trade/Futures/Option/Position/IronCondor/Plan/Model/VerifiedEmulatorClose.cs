using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Reconstructs a closed emulator execution from authentic, contemporaneous stored bid/ask evidence.</summary>
public static class VerifiedEmulatorClose
{
    public static VerifiedEmulatorCloseResult Reconstruct(OrderExecutionDefinition execution,
        EstablishedTradeDefinition trade, StrategyPositionSnapshot closed, StrategyPositionSnapshot quotes,
        StrategyTradePlanSnapshot decision, long planRevision, DateTime executionAtUtc, DateTime correctedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(closed);
        ArgumentNullException.ThrowIfNull(quotes);
        if (executionAtUtc.Kind != DateTimeKind.Utc || correctedAtUtc.Kind != DateTimeKind.Utc
            || correctedAtUtc < executionAtUtc || closed.IsOpen || closed.Legs.Length != 4
            || closed.Legs.Any(x => x.SignedQuantity != 0) || closed.ClosingFills.Length != 4
            || execution.Fills.Length != 4 || trade.ClosingFills.Length != 4 || quotes.Id != closed.Id
            || trade.Id != closed.Id.Trade || execution.Order.TargetPositionId != closed.Id
            || closed.ContractCashMultiplier is not > 0)
            throw new ArgumentException("Complete matching closed emulator execution and four-leg source evidence are required.");
        Guid generation = Guid.Empty;
        var fills = execution.Fills.Select(fill => {
            var leg = quotes.Legs.Single(x => x.TradeLegId == fill.TradeLegId && x.ContractId == fill.ContractId);
            var quote = leg.OptionTick ?? throw new ArgumentException("Original option tick is required.");
            var at = quote.ObservedAtUtc.UtcDateTime;
            if (quote.SourceGeneration == Guid.Empty || generation != Guid.Empty && generation != quote.SourceGeneration
                || at > executionAtUtc || executionAtUtc - at > TimeSpan.FromSeconds(2)
                || !double.IsFinite(quote.BidPrice) || !double.IsFinite(quote.AskPrice)
                || quote.BidPrice <= 0 || quote.AskPrice < quote.BidPrice
                || (fill.SignedQuantity > 0 ? quote.AskSize : quote.BidSize) < Math.Abs(fill.SignedQuantity)
                || fill.SignedQuantity != -leg.SignedQuantity
                || !closed.ClosingFills.Contains(fill) || !trade.ClosingFills.Contains(fill))
                throw new ArgumentException("All four executable quotes must have coherent generation, time, price and depth.");
            generation = quote.SourceGeneration;
            return fill with { Price = (decimal)(fill.SignedQuantity > 0 ? quote.AskPrice : quote.BidPrice), FilledAtUtc = executionAtUtc };
        }).ToArray();
        var deltaPoints = -fills.Sum(x => x.Price * x.SignedQuantity)
            + execution.Fills.Sum(x => x.Price * x.SignedQuantity);
        var position = closed with { ClosingFills = fills, PositionSequence = checked(closed.PositionSequence + 1),
            RouteGeneration = checked(closed.RouteGeneration + 1), RealizedPnl = closed.RealizedPnl + deltaPoints,
            DailyPnlPoints = closed.RealizedPnl + deltaPoints + closed.UnrealizedPnl - closed.DailyOpeningPnl,
            HistoryAsOfUtc = closed.HistoryAsOfUtc == default ? closed.AsOfUtc : closed.HistoryAsOfUtc,
            HistoryPositionSequence = closed.HistoryPositionSequence == 0 ? closed.PositionSequence : closed.HistoryPositionSequence };
        position = StrategyPositionCurrencyPnl.Apply(position);
        var plan = IronCondorClosePlan.Create(decision, position,
            "Verified stored bid/ask reconstruction; original synthetic fills retained in source history", planRevision, correctedAtUtc);
        return new(execution with { Fills = fills, CompletedAtUtc = executionAtUtc },
            trade with { ClosingFills = fills, ClosedAtUtc = executionAtUtc, EvidenceRevision = checked(trade.EvidenceRevision + 1) },
            position, plan, deltaPoints * closed.ContractCashMultiplier.Value);
    }
}

public sealed record VerifiedEmulatorCloseResult(OrderExecutionDefinition Execution, EstablishedTradeDefinition Trade,
    StrategyPositionSnapshot Position, StrategyTradePlanSnapshot Plan, decimal CashDelta);
