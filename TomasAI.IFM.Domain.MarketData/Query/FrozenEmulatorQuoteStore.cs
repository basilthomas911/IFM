using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Process-local, bounded bridge from a requested frozen chain to emulator matching.</summary>
public static class FrozenEmulatorQuoteStore
{
    private static readonly ConcurrentDictionary<string, FrozenQuote> Quotes = new(StringComparer.Ordinal);
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(10);

    public static void Publish(IEnumerable<EvaluatedOptionContractReadModel> contracts, DateTimeOffset at)
    {
        foreach (var contract in contracts)
        {
            if (contract.Bid is not > 0 || contract.Ask is not { } ask || ask < contract.Bid)
                continue;
            Quotes[contract.ContractId] = new(contract.Bid.Value, ask, at);
        }
        foreach (var item in Quotes)
            if (at - item.Value.CapturedAtUtc > PreviewLifetime)
                Quotes.TryRemove(item.Key, out _);
    }

    public static bool TryGet(string contractId, DateTimeOffset now, out decimal bid, out decimal ask)
    {
        if (Quotes.TryGetValue(contractId, out var quote)
            && now >= quote.CapturedAtUtc && now - quote.CapturedAtUtc <= PreviewLifetime)
        {
            bid = quote.Bid;
            ask = quote.Ask;
            return true;
        }
        bid = ask = 0;
        return false;
    }

    private sealed record FrozenQuote(decimal Bid, decimal Ask, DateTimeOffset CapturedAtUtc);
}
