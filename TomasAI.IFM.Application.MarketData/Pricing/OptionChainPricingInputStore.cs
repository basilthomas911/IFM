using System.Collections.Concurrent;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Bounded worker-local input store. Registration happens before physical session admission.</summary>
public sealed class OptionChainPricingInputStore
{
    readonly ConcurrentDictionary<string, OptionChainPricingInputs> entries = new(StringComparer.Ordinal);
    readonly System.Threading.Lock registration = new();

    public void Set(OptionChainPricingInputs value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var id = value.Context.Contract.ContractId;
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Exact contract identity is required.",nameof(value));
        lock (registration)
        {
            if (!entries.ContainsKey(id) && entries.Count >= 2048) throw new InvalidOperationException("Option pricing input capacity exceeded.");
            // A reference update never retains Greeks or an independently captured option quote.
            entries[id] = value with { OptionQuote = null };
        }
    }

    public bool TryGet(string id, out OptionChainPricingInputs? inputs) => entries.TryGetValue(id, out inputs);
    public bool Remove(string id) { lock (registration) return entries.TryRemove(id, out _); }

    public void ObserveQuote(string id, OptionChainPricingInputs expected, LastQuoteTickSnapshot quote)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.OptionQuote is { } previous && (quote.EventTimestamp < previous.EventTimestamp
            || quote.EventTimestamp == previous.EventTimestamp && quote.SourceSequence <= previous.SourceSequence)) return;
        entries.TryUpdate(id, expected with { OptionQuote = quote }, expected);
    }
}
