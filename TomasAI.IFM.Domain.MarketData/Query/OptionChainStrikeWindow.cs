using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

public sealed record OptionChainStrikeWindowResult(
    FuturesOptionContractReadModel[] Contracts, decimal? LowerBound, decimal? UpperBound, string Method);

public static class OptionChainStrikeWindow
{
    public static OptionChainStrikeWindowResult Select(IEnumerable<FuturesOptionContractReadModel> definitions,
        decimal? underlyingPrice, decimal? standardDeviationAmount, double multiplier, int maximumStrikeCount,
        IReadOnlyCollection<string>? requiredContractIds = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (!double.IsFinite(multiplier) || multiplier <= 0) throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (maximumStrikeCount is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(maximumStrikeCount));
        var values = definitions.Where(x => x.StrikePrice > 0 && double.IsFinite(x.StrikePrice))
            .DistinctBy(x => x.ContractId).ToArray();
        if (values.Length == 0) return new([], null, null, "Empty");
        var strikes = values.Select(x => (decimal)x.StrikePrice).Distinct().OrderBy(x => x).ToArray();
        var centre = underlyingPrice is > 0 ? underlyingPrice.Value : strikes[strikes.Length / 2];
        decimal? lower = null, upper = null;
        string method;
        decimal[] selectedStrikes;
        if (underlyingPrice is > 0 && standardDeviationAmount is > 0)
        {
            var distance = standardDeviationAmount.Value * (decimal)multiplier;
            lower = centre - distance;
            upper = centre + distance;
            selectedStrikes = strikes.Where(x => x >= lower && x <= upper).ToArray();
            method = $"Bollinger{multiplier:0.##}Sigma";
        }
        else
        {
            selectedStrikes = strikes.OrderBy(x => Math.Abs(x - centre)).Take(maximumStrikeCount).OrderBy(x => x).ToArray();
            method = "NearestStrikesFallback";
        }
        if (selectedStrikes.Length > maximumStrikeCount)
            selectedStrikes = selectedStrikes.OrderBy(x => Math.Abs(x - centre)).Take(maximumStrikeCount).OrderBy(x => x).ToArray();
        var selected = selectedStrikes.ToHashSet();
        var required = (requiredContractIds ?? []).ToHashSet(StringComparer.Ordinal);
        var result = values.Where(x => selected.Contains((decimal)x.StrikePrice) || required.Contains(x.ContractId))
            .OrderBy(x => x.StrikePrice).ThenBy(x => x.OptionType, StringComparer.Ordinal)
            .ThenBy(x => x.ContractId, StringComparer.Ordinal).ToArray();
        if (selectedStrikes.Length > 0) { lower = selectedStrikes[0]; upper = selectedStrikes[^1]; }
        return new(result, lower, upper, method);
    }
}
