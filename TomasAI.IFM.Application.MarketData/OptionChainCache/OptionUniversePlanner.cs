using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Result of bounded background coverage planning. Capacity failures never truncate an apparently complete scope.</summary>
public sealed record OptionUniversePlanResult(ImmutableArray<PreparedOptionUniverse> Universes, string ReasonCode);

/// <summary>Background-only normalized coverage planner; it performs no network or storage operations.</summary>
public interface IOptionUniversePlanner
{
    /// <summary>Plans the union of enabled biases from a complete definition scope using current volatility coverage.</summary>
    OptionUniversePlanResult Plan(StrategyOptionChainParameterSet parameters, DateOnly valueDate,
        ImmutableArray<OptionDefinitionCandidate> definitions, bool complete, decimal futuresPrice, double impliedVolatility,
        OptionPricingCalendar calendar, TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, IReadOnlyDictionary<string, decimal>? underlyingPrices = null);
}

/// <summary>Expands a volatility envelope by the largest permitted wing and retains every matching expiry/right/strike.</summary>
public sealed class OptionUniversePlanner : IOptionUniversePlanner
{
    /// <inheritdoc />
    public OptionUniversePlanResult Plan(StrategyOptionChainParameterSet parameters, DateOnly valueDate,
        ImmutableArray<OptionDefinitionCandidate> definitions, bool complete, decimal futuresPrice, double impliedVolatility,
        OptionPricingCalendar calendar, TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, IReadOnlyDictionary<string, decimal>? underlyingPrices = null)
    {
        ArgumentNullException.ThrowIfNull(parameters); parameters.Validate();
        if (!parameters.Enabled || !parameters.BiasRows.Any(x => x.Enabled)) return new([], "StrategyDisabled");
        if (!complete || definitions.IsDefault) return new([], "IncompleteDefinitions");
        if (valueDate == default || futuresPrice <= 0 || !double.IsFinite(impliedVolatility) || impliedVolatility is <= 0 or > 5)
            return new([], "CoverageInputsUnavailable");
        if (definitions.Length > 100000 || definitions.Select(x => x.ContractId).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            return new([], "ConflictingDefinitions");
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        DateOnly Expiry(OptionDefinitionCandidate candidate)
        {
            var ns = candidate.Definition.ExpirationTimestampNanoseconds;
            if (ns is null || ns == ulong.MaxValue || ns % 100 != 0 || ns / 100 > long.MaxValue) return default;
            if (ns.Value / 100 > (ulong)(DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks)) return default;
            var at = DateTimeOffset.UnixEpoch.AddTicks((long)(ns.Value / 100));
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, timezone).DateTime);
        }
        var rows = parameters.BiasRows.Where(x => x.Enabled).ToArray();
        var expiryScopes = definitions.Where(x => x.Definition.Ticker == parameters.InstrumentRoot)
            .Select(x => (Definition: x, Expiry: Expiry(x))).Where(x => x.Expiry != default)
            .GroupBy(x => (x.Expiry, Underlying: x.Definition.Definition.Underlying)).Where(g => rows.Any(r => g.Key.Expiry.DayNumber - valueDate.DayNumber >= r.MinimumDte
                && g.Key.Expiry.DayNumber - valueDate.DayNumber <= r.MaximumDte)).OrderBy(g => g.Key).ToArray();
        if (expiryScopes.Length == 0) return new([], "NoEligibleExpiration");
        // Cover each enabled bias's preferred/fallback expiration before adding additional expirations.
        var selected = new HashSet<DateOnly>();
        foreach (var row in rows)
        {
            var eligible = expiryScopes.Where(g => g.Key.Expiry.DayNumber - valueDate.DayNumber >= row.MinimumDte
                && g.Key.Expiry.DayNumber - valueDate.DayNumber <= row.MaximumDte)
                .OrderBy(g => Math.Abs(g.Key.Expiry.DayNumber - valueDate.DayNumber - row.PreferredDte)).ThenBy(g => g.Key.Expiry).ThenBy(g => g.Key.Underlying, StringComparer.Ordinal).ToArray();
            if (eligible.Length == 0) return new([], "BiasExpirationUnavailable");
            selected.Add(eligible[0].Key.Expiry);
        }
        if (selected.Count > parameters.MaximumExpirations) return new([], "ExpirationCapacityExceeded");
        foreach (var scope in expiryScopes.OrderBy(g => rows.Min(r => Math.Abs(g.Key.Expiry.DayNumber - valueDate.DayNumber - r.PreferredDte))).ThenBy(g => g.Key.Expiry).ThenBy(g => g.Key.Underlying, StringComparer.Ordinal))
            if (selected.Count < parameters.MaximumExpirations) selected.Add(scope.Key.Expiry);
        var plans = ImmutableArray.CreateBuilder<PreparedOptionUniverse>();
        var count = 0;
        foreach (var scope in expiryScopes.Where(g => selected.Contains(g.Key.Expiry)).OrderBy(g => rows.Min(r => Math.Abs(g.Key.Expiry.DayNumber - valueDate.DayNumber - r.PreferredDte))).ThenBy(g => g.Key.Expiry).ThenBy(g => g.Key.Underlying, StringComparer.Ordinal))
        {
            var active = rows.Where(r => scope.Key.Expiry.DayNumber - valueDate.DayNumber >= r.MinimumDte
                && scope.Key.Expiry.DayNumber - valueDate.DayNumber <= r.MaximumDte).ToArray();
            var wing = active.SelectMany(r => r.PutWingWidths.Concat(r.CallWingWidths)).DefaultIfEmpty().Max();
            var distance = CoverageDistance(futuresPrice, impliedVolatility, scope.Key.Expiry.DayNumber - valueDate.DayNumber,
                parameters.CacheRangeMultiplier, wing);
            var options = scope.Where(x => underlyingPrices is null ? Math.Abs(x.Definition.Definition.StrikePrice - futuresPrice) <= distance
                    : underlyingPrices.TryGetValue(x.Definition.Definition.Underlying, out var actualForward) && actualForward > 0
                        && Math.Abs(x.Definition.Definition.StrikePrice - actualForward) <= CoverageDistance(actualForward, impliedVolatility,
                            scope.Key.Expiry.DayNumber - valueDate.DayNumber, parameters.CacheRangeMultiplier, wing))
                .Select(x => x.Definition).OrderBy(x => x.ContractId, StringComparer.Ordinal).ToImmutableArray();
            if (options.IsEmpty) return new([], "StrikeCoverageUnavailable");
            count += options.Length;
            if (count > parameters.MaximumContracts) return new([], "ContractCapacityExceeded");
            var plan = new CompositionMarketDataPlan { IncludeOptions = true, ScopeComplete = true, ValueDate = valueDate,
                MaturityDate = scope.Key.Expiry, Root = parameters.InstrumentRoot, Options = options,
                Calendar = calendar, Publication = publication, Conversion = conversion };
            var digest = parameters.Hash();
            var id = PricingSemanticHash.Compute(new { parameters.ParameterSetId, parameters.Version, Expiry = scope.Key.Expiry, scope.Key.Underlying, Digest = digest, Contracts = options.Select(x => x.ContractId).ToArray() });
            plans.Add(new(id, digest, plan, ["Weekly", "Monthly"]) { Parameters = parameters });
        }
        return new(plans.ToImmutable(), "");
    }

    /// <summary>Underlying points: F ? IV ? sqrt(calendar DTE / 365) ? configured multiplier + maximum wing width.</summary>
    public static decimal CoverageDistance(decimal futuresPrice, double impliedVolatility, int calendarDte,
        double multiplier, decimal maximumWingWidth)
    {
        if (futuresPrice <= 0 || calendarDte <= 0 || !double.IsFinite(impliedVolatility) || impliedVolatility <= 0
            || !double.IsFinite(multiplier) || multiplier is < 1 or > 5 || maximumWingWidth < 0)
            throw new ArgumentException("Invalid volatility coverage inputs.");
        return checked((decimal)((double)futuresPrice * impliedVolatility * Math.Sqrt(calendarDte / 365d) * multiplier) + maximumWingWidth);
    }
}
