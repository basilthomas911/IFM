using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Model;

/// <summary>Revalues a historical close using immutable quotes while retaining current live observations.</summary>
public static class StoredQuotePositionEndOfDayRecovery
{
    public sealed record Result(StrategyPositionSnapshot Current, StrategyPositionSnapshot Seal);

    public static Result Recover(StrategyPositionSnapshot original, StrategyPositionSnapshot current,
        DateOnly date, DateTime boundary, IReadOnlyList<StoredPositionClosingQuote> quotes, StrategyPositionSnapshot? previousEndOfDay = null)
    {
        if (original.Id != current.Id || original.StrategyKind != current.StrategyKind || !original.IsOpen || !current.IsOpen
            || original.ValueDate != date || current.ValueDate < date || original.PositionSequence > current.PositionSequence
            || original.Phase is not (StrategyPositionPhase.MarkToMarket or StrategyPositionPhase.EndOfDay)
            || boundary.Kind != DateTimeKind.Utc || boundary != FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime)
            throw new InvalidOperationException("POSITION.EOD_RECOVERY.INVALID_SOURCE");
        var active = original.Legs.Where(leg => leg.SignedQuantity != 0).ToArray();
        if (active.Length == 0 || quotes.Count != active.Select(leg => leg.ContractId).Distinct(StringComparer.Ordinal).Count()
            || quotes.Select(quote => quote.ContractId).Distinct(StringComparer.Ordinal).Count() != quotes.Count
            || current.Legs.Length != original.Legs.Length
            || original.Legs.Any(leg => !current.Legs.Any(now => now.TradeLegId == leg.TradeLegId
                && now.ContractId == leg.ContractId && now.SignedQuantity == leg.SignedQuantity && now.OpeningPrice == leg.OpeningPrice)))
            throw new InvalidOperationException("POSITION.EOD_RECOVERY.LEG_MISMATCH");
        var byContract = quotes.ToDictionary(quote => quote.ContractId, StringComparer.Ordinal);
        VerifiedExpiryFixing.RequireOffsetDelivery(original,quotes,boundary);
        foreach (var leg in active)
        {
            if(byContract.TryGetValue(leg.ContractId,out var expiry) && expiry.ExpiryFixing is not null)
            {
                if(expiry.ValueDate!=date || !expiry.ExpiryFixing.Covers(expiry,boundary)
                    || expiry.EvidenceId.Length!=64 || !expiry.EvidenceId.All(Uri.IsHexDigit)
                    || expiry.SourceArchiveSha256.Length!=64 || !expiry.SourceArchiveSha256.All(Uri.IsHexDigit))
                    throw new InvalidOperationException("POSITION.EOD_RECOVERY.INVALID_EXPIRY_FIXING");
                continue;
            }
            if (!byContract.TryGetValue(leg.ContractId, out var quote) || quote.ValueDate != date
                || quote.InstrumentId == 0 || quote.SourceSequence == 0 || quote.Ask <= 0 || (quote.HasBid ? quote.Bid <= 0 || quote.Ask < quote.Bid : quote.Bid != 0 || quote.BidSize != 0)
                || (quote.HasBid && quote.BidSize == 0) || quote.AskSize == 0
                || quote.EvidenceId.Length != 64 || !quote.EvidenceId.All(Uri.IsHexDigit)
                || quote.SourceArchiveSha256.Length != 64 || !quote.SourceArchiveSha256.All(Uri.IsHexDigit)
                || quote.ReceiveTimestampNanoseconds < quote.EventTimestampNanoseconds
                || quote.EventTimestampNanoseconds > (boundary.Ticks - DateTime.UnixEpoch.Ticks) * 100 || (boundary - quote.ObservedAtUtc > TimeSpan.FromMinutes(15) && quote.Book?.Covers(boundary) != true)
                || (quote.ObservedAtUtc < leg.LastPriceAtUtc && leg.LastPriceAtUtc <= boundary))
                throw new InvalidOperationException("POSITION.EOD_RECOVERY.INVALID_QUOTE");
        }
        var legs = original.Legs.Select(leg => leg.SignedQuantity == 0 ? leg : leg with
        {
            CurrentPrice = byContract[leg.ContractId].MarkPrice,
            LastPriceAtUtc = byContract[leg.ContractId].ExpiryFixing is null ? byContract[leg.ContractId].ObservedAtUtc : leg.LastPriceAtUtc,
            LastSourceSequence = byContract[leg.ContractId].ExpiryFixing is null ? byContract[leg.ContractId].SourceSequence : leg.LastSourceSequence,
            // Recovered raw quotes do not claim that historical Greeks were recalculated.
            OptionTick = null, OptionRisk = null, Scenario = null
        }).ToArray();
        if(previousEndOfDay is not null && (previousEndOfDay.Id!=original.Id || previousEndOfDay.Phase!=StrategyPositionPhase.EndOfDay || previousEndOfDay.ValueDate>=date))
            throw new InvalidOperationException("POSITION.EOD_RECOVERY.PRIOR_SEAL_MISMATCH");
        var machine = new StrategyPositionActorStateMachine();
        machine.Replay(original with { Legs = legs,
            DailyOpeningPnl = previousEndOfDay is null ? original.DailyOpeningPnl : previousEndOfDay.RealizedPnl + previousEndOfDay.UnrealizedPnl, LatestSealedValueDate = date.AddDays(-1),
            LastMarketObservationUtc = quotes.All(q=>q.ExpiryFixing is not null) ? original.LastMarketObservationUtc : active.Max(leg => byContract[leg.ContractId].ObservedAtUtc), HistoryValuation = null });
        var decision = machine.EndOfDay(date, boundary);
        if (!decision.Accepted) throw new InvalidOperationException($"{decision.Code};{decision.Detail}");
        var seal = decision.Value! with { PositionSequence = checked(current.PositionSequence + 1), EndOfDayClosingQuotes = quotes.ToArray() };
        if (current.ValueDate == date) return new(seal, seal);
        // An older historical correction must not replace the baseline owned by a later sealed day.
        if (current.LatestSealedValueDate > date) return new(current with { PositionSequence = seal.PositionSequence }, seal);
        if (current.DailyOpeningPnl != original.RealizedPnl + original.UnrealizedPnl)
            throw new InvalidOperationException("POSITION.EOD_RECOVERY.BASELINE_CONFLICT");
        var openingPnl = seal.RealizedPnl + seal.UnrealizedPnl;
        if(quotes.All(q=>q.ExpiryFixing is not null))
        {
            // An expired strategy cannot regain a tradable price in the new session. Carry its
            // definitive economic value forward while retaining the actual quote clocks and quantities.
            var settledLegs=current.Legs.Select(leg=>leg.SignedQuantity==0?leg:leg with{
                CurrentPrice=byContract[leg.ContractId].MarkPrice,OptionTick=null,OptionRisk=null,Scenario=null}).ToArray();
            var unrealized=settledLegs.Sum(leg=>(leg.CurrentPrice-leg.OpeningPrice)*leg.SignedQuantity);
            return new(StrategyPositionCurrencyPnl.Apply(current with{Legs=settledLegs,PositionSequence=seal.PositionSequence,
                MarketValue=settledLegs.Sum(leg=>leg.CurrentPrice*leg.SignedQuantity),UnrealizedPnl=unrealized,
                DailyOpeningPnl=openingPnl,DailyPnlPoints=current.RealizedPnl+unrealized-openingPnl,
                EndOfDayClosingQuotes=quotes.ToArray(),HistoryValuation=null}),seal);
        }
        var corrected = StrategyPositionCurrencyPnl.Apply(current with
        {
            PositionSequence = seal.PositionSequence, DailyOpeningPnl = openingPnl,
            DailyPnlPoints = current.RealizedPnl + current.UnrealizedPnl - openingPnl, HistoryValuation = null
        });
        return new(corrected, seal);
    }
}
