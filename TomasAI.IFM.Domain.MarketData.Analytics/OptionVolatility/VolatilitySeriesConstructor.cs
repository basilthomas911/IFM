using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

/// <summary>Builds one comparable observation from bounded, already-qualified IV-only inputs.</summary>
public static class VolatilitySeriesConstructor
{
    public static VolatilitySeriesConstructionResult Construct(VolatilitySeriesConstructionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var definition = request.Definition;
        var diagnostics = ImmutableArray.CreateBuilder<string>();

        var compatible = request.Inputs
            .Where(input => Compatible(definition, input, request.AsOfUtc, diagnostics))
            .OrderBy(input => input.ExpirationUtc)
            .ThenBy(input => input.Strike)
            .ThenBy(input => input.IsCall ? 0 : 1)
            .ThenBy(input => input.OptionContractId, StringComparer.Ordinal)
            .ToArray();
        if (compatible.Length == 0)
            return Failure(request, MostSpecificCompatibilityFailure(diagnostics), diagnostics);

        var expiryPoints = compatible.GroupBy(input => input.ExpirationUtc)
            .Select(group => BuildExpiryPoint(definition, request.AsOfUtc, group.ToArray()))
            .OrderBy(point => point.TenorDays)
            .ToArray();
        var incomplete = expiryPoints.FirstOrDefault(point => point.Failure != VolatilitySeriesConstructionFailure.None);
        if (incomplete is not null)
        {
            diagnostics.Add(incomplete.Failure.ToString());
            return Failure(request, incomplete.Failure, diagnostics,
                incomplete.Contributors, VolatilityObservationStatus.Partial);
        }

        var target = definition.Tenor.TargetCalendarDays;
        var exact = expiryPoints.FirstOrDefault(point => point.TenorDays == target);
        decimal value;
        ImmutableArray<QualifiedOptionIvInput> selected;
        if (exact is not null)
        {
            value = exact.Volatility!.Value;
            selected = exact.Contributors;
        }
        else
        {
            var lower = expiryPoints.LastOrDefault(point => point.TenorDays < target);
            var upper = expiryPoints.FirstOrDefault(point => point.TenorDays > target);
            if (lower is null || upper is null ||
                definition.Construction.ConstantTenorInterpolation == VolatilityInterpolationMethod.None)
                return Failure(request, VolatilitySeriesConstructionFailure.MissingTenorBracket, diagnostics);

            value = Interpolate(definition.Construction.ConstantTenorInterpolation, target, lower, upper);
            selected = lower.Contributors.AddRange(upper.Contributors);
        }

        var provenance = Provenance(selected, definition);
        var id = Identity(request, provenance.InputDigest);
        var observation = new OptionIvObservation(
            OptionIvObservation.CurrentSchemaVersion, id, definition.Identity,
            request.ExchangeValueDate, request.SamplingSlot, value,
            VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified, string.Empty,
            selected.Max(x => x.QuoteObservedAtUtc), request.AsOfUtc, request.AsOfUtc,
            request.Revision, request.SupersedesObservationId, provenance);
        return new(observation, VolatilitySeriesConstructionFailure.None,
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray());
    }

    static ExpiryPoint BuildExpiryPoint(VolatilitySeriesDefinition definition, DateTimeOffset asOf,
        QualifiedOptionIvInput[] expiryInputs)
    {
        var tenorDays = (decimal)(expiryInputs[0].ExpirationUtc - asOf).TotalDays;
        var byUnderlying = expiryInputs.GroupBy(x => x.UnderlyingFuturesContractId, StringComparer.Ordinal).ToArray();
        if (definition.Qualification.RequireExactUnderlyingMatch && byUnderlying.Length != 1)
            return new(tenorDays, null, [], VolatilitySeriesConstructionFailure.ExactUnderlyingMismatch);

        var forward = expiryInputs.Select(x => x.UnderlyingPrice).Distinct().ToArray();
        if (definition.Qualification.RequireExactUnderlyingMatch && forward.Length != 1)
            return new(tenorDays, null, [], VolatilitySeriesConstructionFailure.ExactUnderlyingMismatch);
        var reference = forward.Order().ElementAt(forward.Length / 2);
        var strike = expiryInputs.Select(x => x.Strike).Distinct()
            .OrderBy(x => Math.Abs(x - reference)).ThenBy(x => x).First();
        var atm = expiryInputs.Where(x => x.Strike == strike).ToArray();
        var call = atm.Where(x => x.IsCall).OrderBy(x => x.OptionContractId, StringComparer.Ordinal).FirstOrDefault();
        var put = atm.Where(x => !x.IsCall).OrderBy(x => x.OptionContractId, StringComparer.Ordinal).FirstOrDefault();

        return definition.Tenor.OptionSideSelection switch
        {
            VolatilityOptionSideSelection.Calls when call is not null => Point(call),
            VolatilityOptionSideSelection.Puts when put is not null => Point(put),
            VolatilityOptionSideSelection.CallPutCombined when call is not null && put is not null &&
                definition.Tenor.SideCombinationMethod == VolatilitySideCombinationMethod.ArithmeticMean =>
                new(tenorDays, (call.ImpliedVolatility + put.ImpliedVolatility) / 2m, [call, put],
                    VolatilitySeriesConstructionFailure.None),
            _ => new(tenorDays, null, atm.ToImmutableArray(), VolatilitySeriesConstructionFailure.MissingOptionSide)
        };

        ExpiryPoint Point(QualifiedOptionIvInput input) =>
            new(tenorDays, input.ImpliedVolatility, [input], VolatilitySeriesConstructionFailure.None);
    }

    static decimal Interpolate(VolatilityInterpolationMethod method, int targetDays, ExpiryPoint lower, ExpiryPoint upper)
    {
        var lowerDays = lower.TenorDays;
        var upperDays = upper.TenorDays;
        if (lowerDays <= 0m || upperDays <= lowerDays)
            throw new InvalidOperationException("The qualified tenor bracket is invalid.");
        var weight = (targetDays - lowerDays) / (upperDays - lowerDays);
        var low = lower.Volatility!.Value;
        var high = upper.Volatility!.Value;
        return method switch
        {
            VolatilityInterpolationMethod.LinearVolatility => low + weight * (high - low),
            VolatilityInterpolationMethod.LinearTotalVariance =>
                CheckedSqrt((lowerDays * low * low + weight *
                    (upperDays * high * high - lowerDays * low * low)) / targetDays),
            _ => throw new InvalidOperationException("The configured interpolation method is unsupported.")
        };
    }

    static decimal CheckedSqrt(decimal value)
    {
        if (value < 0m) throw new InvalidOperationException("Interpolated total variance is negative.");
        var result = Math.Sqrt((double)value);
        if (!double.IsFinite(result)) throw new InvalidOperationException("Interpolated volatility is non-finite.");
        return (decimal)result;
    }

    static bool Compatible(VolatilitySeriesDefinition definition, QualifiedOptionIvInput input,
        DateTimeOffset asOf, ImmutableArray<string>.Builder diagnostics)
    {
        string? failure = null;
        if (input.ImpliedVolatility <= 0m || input.UnderlyingPrice <= 0m || input.Strike <= 0m)
            failure = VolatilitySeriesConstructionFailure.InvalidImpliedVolatility.ToString();
        else if (input.UnderlyingRoot != definition.UnderlyingRoot ||
                 input.Environment != definition.DataIdentity.Environment ||
                 input.Venue != definition.Venue || input.Currency != definition.Currency ||
                 input.Provider != definition.DataIdentity.Provider || input.Dataset != definition.DataIdentity.Dataset)
            failure = VolatilitySeriesConstructionFailure.IncompatibleSource.ToString();
        else if (!definition.Tenor.EligibleProductFamilies.Contains(input.ProductFamily, StringComparer.Ordinal) ||
                 input.ExerciseConvention != definition.Pricing.ExerciseConvention ||
                 input.PremiumConvention != definition.Pricing.PremiumConvention ||
                 input.SettlementConvention != definition.Pricing.SettlementConvention ||
                 input.NumericalPolicyVersion != definition.Pricing.NumericalPolicyVersion ||
                 !definition.Pricing.ApplicablePricerVersions.Contains(input.PricerVersion, StringComparer.Ordinal))
            failure = VolatilitySeriesConstructionFailure.IncompatibleConvention.ToString();
        else if (input.QuoteObservedAtUtc > asOf || input.UnderlyingObservedAtUtc > asOf ||
                 asOf - input.QuoteObservedAtUtc > definition.Qualification.MaximumQuoteAge ||
                 asOf - input.UnderlyingObservedAtUtc > definition.Qualification.MaximumIvAge ||
                 (input.QuoteObservedAtUtc - input.UnderlyingObservedAtUtc).Duration() > definition.Qualification.MaximumQuoteSkew)
            failure = VolatilitySeriesConstructionFailure.StaleInput.ToString();
        if (failure is null) return true;
        diagnostics.Add($"{failure}:{input.OptionContractId}");
        return false;
    }

    static VolatilityObservationProvenance Provenance(IEnumerable<QualifiedOptionIvInput> inputs,
        VolatilitySeriesDefinition definition)
    {
        var ordered = inputs.OrderBy(x => x.OptionContractId, StringComparer.Ordinal).ToArray();
        var digestText = string.Join("|", ordered.Select(x => x.InputDigest));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digestText))).ToLowerInvariant();
        return new(ordered.Select(x => new VolatilityContributor(x.OptionContractId,
                x.UnderlyingFuturesContractId, x.QuoteObservedAtUtc, x.UnderlyingObservedAtUtc,
                x.QuoteSequence, x.UnderlyingSequence, x.SourceGenerationId)).ToImmutableArray(),
            string.Join("+", ordered.Select(x => x.PricerVersion).Distinct(StringComparer.Ordinal).Order()),
            digest, definition.Governance.ApprovalEvidenceId);
    }

    static VolatilitySeriesConstructionFailure MostSpecificCompatibilityFailure(
        ImmutableArray<string>.Builder diagnostics)
    {
        foreach (var failure in new[]
                 {
                     VolatilitySeriesConstructionFailure.InvalidImpliedVolatility,
                     VolatilitySeriesConstructionFailure.IncompatibleConvention,
                     VolatilitySeriesConstructionFailure.IncompatibleSource,
                     VolatilitySeriesConstructionFailure.StaleInput
                 })
            if (diagnostics.Any(x => x.StartsWith($"{failure}:", StringComparison.Ordinal)))
                return failure;
        return VolatilitySeriesConstructionFailure.NoQualifiedInputs;
    }

    static VolatilitySeriesConstructionResult Failure(VolatilitySeriesConstructionRequest request,
        VolatilitySeriesConstructionFailure failure, ImmutableArray<string>.Builder diagnostics,
        ImmutableArray<QualifiedOptionIvInput> contributors = default,
        VolatilityObservationStatus status = VolatilityObservationStatus.Unavailable)
    {
        var inputs = contributors.IsDefault ? ImmutableArray<QualifiedOptionIvInput>.Empty : contributors;
        var provenance = Provenance(inputs, request.Definition);
        var observation = new OptionIvObservation(OptionIvObservation.CurrentSchemaVersion,
            Identity(request, provenance.InputDigest), request.Definition.Identity, request.ExchangeValueDate,
            request.SamplingSlot, null, VolatilityValueUnit.AnnualDecimal, status, failure.ToString(),
            request.AsOfUtc, request.AsOfUtc, request.AsOfUtc, request.Revision,
            request.SupersedesObservationId, provenance);
        return new(observation, failure,
            diagnostics.Append(failure.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray());
    }

    static string Identity(VolatilitySeriesConstructionRequest request, string digest)
    {
        var text = string.Join("|", request.Definition.DataIdentity.Environment,
            request.Definition.Identity.SeriesId, request.Definition.Identity.MethodologyVersion,
            request.ExchangeValueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.SamplingSlot, request.Revision.ToString(CultureInfo.InvariantCulture), digest);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    static void ValidateRequest(VolatilitySeriesConstructionRequest request)
    {
        var d = request.Definition;
        if (d.SchemaVersion != VolatilitySeriesDefinition.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(d.Identity.SeriesId) || string.IsNullOrWhiteSpace(d.Identity.MethodologyVersion) ||
            d.Tenor.TargetCalendarDays <= 0 || d.Tenor.MoneynessConvention != VolatilityMoneynessConvention.AtTheMoneyForward ||
            d.Tenor.OptionSideSelection == VolatilityOptionSideSelection.Unspecified ||
            d.Tenor.SideCombinationMethod == VolatilitySideCombinationMethod.Unspecified ||
            d.Construction.ConstantTenorInterpolation == VolatilityInterpolationMethod.Unspecified ||
            request.ExchangeValueDate == default || string.IsNullOrWhiteSpace(request.SamplingSlot) ||
            request.AsOfUtc.Offset != TimeSpan.Zero || request.Revision < 1 ||
            (request.Revision == 1) != (request.SupersedesObservationId is null))
            throw new ArgumentException("A complete, explicit Stage 4 series construction request is required.", nameof(request));
    }

    sealed record ExpiryPoint(decimal TenorDays, decimal? Volatility,
        ImmutableArray<QualifiedOptionIvInput> Contributors, VolatilitySeriesConstructionFailure Failure);
}
