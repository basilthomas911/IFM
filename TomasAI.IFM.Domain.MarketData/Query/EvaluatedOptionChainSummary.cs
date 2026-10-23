using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Server summary of exact evaluated-chain inputs; no client market arithmetic is required.</summary>
public static class EvaluatedOptionChainSummary
{
    public static EvaluatedOptionChainReadModel Apply(EvaluatedOptionChainReadModel chain, DateOnly valueDate)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (valueDate == default) throw new ArgumentException("Operational ValueDate is required.", nameof(valueDate));
        var nearest = chain.UnderlyingPrice is { } price ? chain.Contracts
            .Where(x => x.ImpliedVolatility is > 0 && double.IsFinite(x.ImpliedVolatility.Value))
            .OrderBy(x => Math.Abs(x.Strike - price)).ThenBy(x => x.Strike).FirstOrDefault()?.Strike : null;
        var iv = nearest.HasValue ? chain.Contracts.Where(x => x.Strike == nearest && x.ImpliedVolatility is > 0 && double.IsFinite(x.ImpliedVolatility.Value))
            .Average(x => x.ImpliedVolatility) : null;
        return chain with {
            AtmImpliedVolatility = iv.HasValue && double.IsFinite(iv.Value) && iv.Value <= (double)decimal.MaxValue ? (decimal)iv.Value : null,
            DailyPriceChange = chain.LastUnderlyingPrice.HasValue && chain.PreviousSessionClose is > 0
                ? chain.LastUnderlyingPrice.Value - chain.PreviousSessionClose.Value : null,
            DailyPriceChangeFraction = chain.LastUnderlyingPrice.HasValue && chain.PreviousSessionClose is > 0
                ? chain.LastUnderlyingPrice.Value / chain.PreviousSessionClose.Value - 1 : null,
            DaysToExpiry = chain.ExpiryDate.DayNumber - valueDate.DayNumber };
    }
}
