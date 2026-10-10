using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
namespace TomasAI.IFM.Application.MarketData.OptionChainCache;
/// <summary>Background-only deterministic delta shortlist and exact protective wings. No solver runs on the reader.</summary>
public static class OptionChainCandidatePreparation
{
    /// <summary>Selects bounded absolute-delta candidates and their quoted wings; missing wings are never synthesized.</summary>
    public static ImmutableArray<CompositionInstrumentSnapshot> Select(ImmutableArray<CompositionInstrumentSnapshot> source,
        OptionStrategyBiasParameters policy, DateTimeOffset now)
    {
        policy.Validate();
        bool Usable(CompositionInstrumentSnapshot row) => row.Valuation is not null && row.Instrument.Pricing is { } context
            && context.ValidUntilUtc > now && row.Instrument.Quote is { } quote && row.Instrument.Underlying is { } underlying
            && quote.Bid > 0 && quote.Ask >= quote.Bid && quote.Ask - quote.Bid <= policy.MaximumLegSpreadPoints
            && quote.BidSize >= policy.MinimumQuoteSize && quote.AskSize >= policy.MinimumQuoteSize
            && quote.ReceivedAtUtc <= now && underlying.ReceivedAtUtc <= now
            && (now - quote.EventAtUtc).TotalMilliseconds <= policy.MaximumQuoteAgeMilliseconds
            && (now - quote.ReceivedAtUtc).TotalMilliseconds <= policy.MaximumQuoteAgeMilliseconds
            && (now - underlying.EventAtUtc).TotalMilliseconds <= policy.MaximumQuoteAgeMilliseconds
            && (now - underlying.ReceivedAtUtc).TotalMilliseconds <= policy.MaximumQuoteAgeMilliseconds
            && Math.Abs((quote.EventAtUtc - underlying.EventAtUtc).TotalMilliseconds) <= policy.MaximumQuoteSkewMilliseconds;
        var usable = source.Where(Usable).ToArray();
        if (usable.Length == 0) return [];
        var latest = usable.Max(x => x.Instrument.Quote!.EventAtUtc > x.Instrument.Underlying!.EventAtUtc
            ? x.Instrument.Quote.EventAtUtc : x.Instrument.Underlying.EventAtUtc);
        var eligible = usable.Where(x => (latest - x.Instrument.Quote!.EventAtUtc).TotalMilliseconds <= policy.MaximumQuoteSkewMilliseconds
            && (latest - x.Instrument.Underlying!.EventAtUtc).TotalMilliseconds <= policy.MaximumQuoteSkewMilliseconds).ToArray();
        var selected = new Dictionary<string, CompositionInstrumentSnapshot>(StringComparer.Ordinal);
        foreach (var isCall in new[] { false, true })
        {
            var delta = isCall ? policy.CallDelta : policy.PutDelta;
            var widths = isCall ? policy.CallWingWidths : policy.PutWingWidths;
            var side = eligible.Where(x => x.Instrument.IsCall == isCall).ToArray();
            var wings = side.GroupBy(x => (x.Instrument.Strike, x.Instrument.Pricing!.Contract.UnderlyingContractId,
                x.Instrument.Pricing.Contract.ExpirationUtc)).ToDictionary(x => x.Key,
                    x => x.MinBy(row => row.Instrument.ContractId, StringComparer.Ordinal)!);
            var shorts = side.Where(x => (decimal)Math.Abs(x.Valuation!.Delta) >= delta.Minimum
                && (decimal)Math.Abs(x.Valuation!.Delta) <= delta.Maximum)
                .OrderBy(x => Math.Abs((decimal)Math.Abs(x.Valuation!.Delta) - delta.Target))
                .ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal).Take(policy.MaximumCandidatesPerSide);
            foreach (var shortLeg in shorts)
                foreach (var width in widths)
                {
                    var strike = shortLeg.Instrument.Strike + (isCall ? width : -width);
                    if (!wings.TryGetValue((strike, shortLeg.Instrument.Pricing!.Contract.UnderlyingContractId,
                        shortLeg.Instrument.Pricing.Contract.ExpirationUtc), out var wing)) continue;
                    selected.TryAdd(shortLeg.Instrument.ContractId, shortLeg); selected.TryAdd(wing.Instrument.ContractId, wing);
                }
        }
        return selected.Values.OrderBy(x => x.Instrument.ContractId, StringComparer.Ordinal).ToImmutableArray();
    }
}
