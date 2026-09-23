using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>The selected cached contracts and the actual selected strike boundaries.</summary>
/// <param name="Contracts">Cached contracts selected for the chain.</param>
/// <param name="LowerBound">Lowest selected strike, excluding only required legs outside the window.</param>
/// <param name="UpperBound">Highest selected strike, excluding only required legs outside the window.</param>
/// <param name="Method">Selection method, including whether the IV window was clipped.</param>
public sealed record OptionChainStrikeWindowResult(
    FuturesOptionContractReadModel[] Contracts, decimal? LowerBound, decimal? UpperBound, string Method);

/// <summary>Pure selectors for cached option strikes; pricing and live subscription happen elsewhere.</summary>
public static class OptionChainStrikeWindow
{
    /// <summary>Selects every cached strike inside the Bollinger band; missing inputs produce no estimated window.</summary>
    public static OptionChainStrikeWindowResult Select(IEnumerable<FuturesOptionContractReadModel> definitions,
        decimal? underlyingPrice, decimal? standardDeviationAmount, double multiplier,
        IReadOnlyCollection<string>? requiredContractIds = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (!double.IsFinite(multiplier) || multiplier <= 0) throw new ArgumentOutOfRangeException(nameof(multiplier));
        var values = definitions.Where(x => x.StrikePrice > 0 && double.IsFinite(x.StrikePrice))
            .DistinctBy(x => x.ContractId).ToArray();
        if (values.Length == 0) return new([], null, null, "Empty");
        var strikes = values.Select(x => (decimal)x.StrikePrice).Distinct().OrderBy(x => x).ToArray();
        if (underlyingPrice is not > 0 || standardDeviationAmount is not > 0)
            return new([], null, null, "WindowInputsUnavailable");
        var centre = underlyingPrice.Value;
        decimal? lower = null, upper = null;
        string method;
        decimal[] selectedStrikes;
        var distance = standardDeviationAmount.Value * (decimal)multiplier;
        lower = centre - distance;
        upper = centre + distance;
        selectedStrikes = strikes.Where(x => x >= lower && x <= upper).ToArray();
        method = $"Bollinger{multiplier:0.##}Sigma";
        var selected = selectedStrikes.ToHashSet();
        var required = (requiredContractIds ?? []).ToHashSet(StringComparer.Ordinal);
        var result = values.Where(x => selected.Contains((decimal)x.StrikePrice) || required.Contains(x.ContractId))
            .OrderBy(x => x.StrikePrice).ThenBy(x => x.OptionType, StringComparer.Ordinal)
            .ThenBy(x => x.ContractId, StringComparer.Ordinal).ToArray();
        if (selectedStrikes.Length > 0) { lower = selectedStrikes[0]; upper = selectedStrikes[^1]; }
        return new(result, lower, upper, method);
    }

    /// <summary>
    /// Estimates a symmetric, expiry-specific delta window using one-side distance
    /// F × ATM IV × √(seconds to expiry / (365 × 86400)) × Z × (1 + buffer).
    /// The result is a strike-selection estimate, not an assertion of exact option delta.
    /// </summary>
    /// <param name="definitions">Cached option definitions for one underlying and expiration.</param>
    /// <param name="underlyingPrice">Current futures price F.</param>
    /// <param name="atTheMoneyImpliedVolatility">Near-ATM annualized IV as a decimal.</param>
    /// <param name="expiresAtUtc">The option's actual expiry instant.</param>
    /// <param name="evaluatedAtUtc">The current UTC calculation instant.</param>
    /// <param name="zScore">Normal-score estimate of the outer delta; 1.65 approximates 5 delta.</param>
    /// <param name="bufferFraction">Extra width as a fraction; 0.15 adds 15 percent.</param>
    /// <param name="requiredContractIds">Staged legs that must remain visible outside the window.</param>
    /// <returns>Selected contracts with actual strike bounds and an IV-window method label.</returns>
    public static OptionChainStrikeWindowResult SelectImpliedVolatility(
        IEnumerable<FuturesOptionContractReadModel> definitions, decimal underlyingPrice,
        double atTheMoneyImpliedVolatility, DateTimeOffset expiresAtUtc, DateTimeOffset evaluatedAtUtc,
        double zScore = 1.65, double bufferFraction = 0.15,
        IReadOnlyCollection<string>? requiredContractIds = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (underlyingPrice <= 0 || !double.IsFinite(atTheMoneyImpliedVolatility) || atTheMoneyImpliedVolatility <= 0
            || !double.IsFinite(zScore) || zScore <= 0 || !double.IsFinite(bufferFraction)
            || bufferFraction is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(atTheMoneyImpliedVolatility), "IV window inputs are invalid.");
        var remainingSeconds = (expiresAtUtc - evaluatedAtUtc).TotalSeconds;
        if (remainingSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));
        var distance = (decimal)((double)underlyingPrice * atTheMoneyImpliedVolatility
            * Math.Sqrt(remainingSeconds / (365d * 86400d)) * zScore * (1d + bufferFraction));
        var values = definitions.Where(x => x.StrikePrice > 0 && double.IsFinite(x.StrikePrice))
            .DistinctBy(x => x.ContractId).ToArray();
        if (values.Length == 0) return new([], null, null, "Empty");
        var lower = underlyingPrice - distance;
        var upper = underlyingPrice + distance;
        var strikes = values.Select(x => (decimal)x.StrikePrice).Distinct()
            .Where(x => x >= lower && x <= upper).ToArray();
        if (strikes.Length == 0)
            strikes = [values.Select(x => (decimal)x.StrikePrice).Distinct().MinBy(x => Math.Abs(x - underlyingPrice))];
        var selected = strikes.ToHashSet();
        var required = (requiredContractIds ?? []).ToHashSet(StringComparer.Ordinal);
        var contracts = values.Where(x => selected.Contains((decimal)x.StrikePrice) || required.Contains(x.ContractId))
            .OrderBy(x => x.StrikePrice).ThenBy(x => x.OptionType, StringComparer.Ordinal)
            .ThenBy(x => x.ContractId, StringComparer.Ordinal).ToArray();
        return new(contracts, selected.Min(), selected.Max(), "ImpliedVolatility5Delta");
    }
}
