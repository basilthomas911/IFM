using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Retains displayed values across partial snapshots. Never use this display cache for order eligibility.</summary>
public static class OptionChainDisplaySnapshot
{
    public static EvaluatedOptionChainReadModel Merge(EvaluatedOptionChainReadModel? previous, EvaluatedOptionChainReadModel incoming)
    {
        if (previous is null || previous.UnderlyingContractId != incoming.UnderlyingContractId
            || previous.ExpiryDate != incoming.ExpiryDate
            || Frozen(previous) != Frozen(incoming)) return incoming;
        var rows = previous.Contracts.Select(x => x with { IsStale = true, SelectionValid = false })
            .ToDictionary(x => x.ContractId, StringComparer.Ordinal);
        foreach (var next in incoming.Contracts)
        {
            if (!rows.TryGetValue(next.ContractId, out var old)) { rows[next.ContractId] = next; continue; }
            rows[next.ContractId] = next with {
                Bid=next.Bid??old.Bid, Ask=next.Ask??old.Ask, BidSize=next.BidSize??old.BidSize, AskSize=next.AskSize??old.AskSize,
                Last=next.Last??old.Last, LastSize=next.LastSize??old.LastSize,
                ImpliedVolatility=next.ImpliedVolatility??old.ImpliedVolatility, TheoreticalPrice=next.TheoreticalPrice??old.TheoreticalPrice,
                Delta=next.Delta??old.Delta, Gamma=next.Gamma??old.Gamma, Vega=next.Vega??old.Vega, Theta=next.Theta??old.Theta, Rho=next.Rho??old.Rho,
                Volume=next.Volume??old.Volume, OpenInterest=next.OpenInterest??old.OpenInterest,
                QuoteAtUtc=next.QuoteAtUtc??old.QuoteAtUtc, TradeAtUtc=next.TradeAtUtc??old.TradeAtUtc,
                EvaluatedAtUtc=next.EvaluatedAtUtc??old.EvaluatedAtUtc,
                VolumeValueDate=next.VolumeValueDate??old.VolumeValueDate, OpenInterestValueDate=next.OpenInterestValueDate??old.OpenInterestValueDate
            };
        }
        return incoming with {Contracts=rows.Values.OrderByDescending(x=>x.Strike).ThenByDescending(x=>x.IsCall).ToArray()};
    }
    static bool Frozen(EvaluatedOptionChainReadModel chain) => chain.WindowMethod.StartsWith("Frozen emulator preview",StringComparison.Ordinal);
}
