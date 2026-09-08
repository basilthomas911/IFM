using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Exact normalized provider record and its independently requested reviewed mapping.</summary>
public sealed record OptionDefinitionCandidate(string ContractId, string MappingVersion,
    string DefinitionDigest, OptionContractDefinition Definition);

[MessagePackObject]
public sealed record QualifiedOptionDefinition(
    [property: Key(0)] OptionPricingConvention Convention,
    [property: Key(1)] decimal Strike,
    [property: Key(2)] bool IsCall);

public sealed record EuropeanOptionUniverseResult(ImmutableArray<QualifiedOptionDefinition> Definitions,
    ImmutableArray<OptionPricingFailure> Exclusions, OptionPricingFailure? Failure);

/// <summary>Qualifies a complete bounded scope before subscriptions. Raw definitions remain untouched.</summary>
public sealed class EuropeanOptionUniverse(IOptionPricingConventionStore conventions)
{
    public async Task<EuropeanOptionUniverseResult> QualifyAsync(IReadOnlyList<OptionDefinitionCandidate> scope,
        bool complete, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        EuropeanOptionUniverseResult Fail(string code, string id) => new([], [], new(code, "Definitions", id, "Complete qualified option scope is unavailable."));
        if (!complete) return Fail("IncompleteChain", "");
        if (scope.Count > 512) return Fail("SnapshotLimit", "");
        if (scope.Select(x => x.ContractId).Distinct(StringComparer.Ordinal).Count() != scope.Count
            || scope.Select(x => x.Definition.Instrument).Distinct().Count() != scope.Count)
            return Fail("ConflictingDefinition", "");
        var accepted = ImmutableArray.CreateBuilder<QualifiedOptionDefinition>();
        var excluded = ImmutableArray.CreateBuilder<OptionPricingFailure>();
        foreach (var candidate in scope.OrderBy(x => x.ContractId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definition = candidate.Definition;
            var mapping = await conventions.GetAsync(candidate.ContractId, candidate.MappingVersion, cancellationToken).ConfigureAwait(false);
            if (mapping is null || mapping.ContractId != candidate.ContractId || mapping.MappingVersion != candidate.MappingVersion
                || mapping.DefinitionDigest != candidate.DefinitionDigest || mapping.Dataset != definition.Dataset
                || mapping.PublisherId != definition.Instrument.PublisherId || mapping.InstrumentId != definition.Instrument.InstrumentId
                || mapping.RawSymbol != definition.RawSymbol || definition.StrikePrice <= 0
                || definition.Right is not (OptionRightSelection.Call or OptionRightSelection.Put)
                || definition.ExpirationTimestampNanoseconds is not { } ns || ns == ulong.MaxValue || ns % 100 != 0
                || DateTimeOffset.UnixEpoch.AddTicks((long)(ns / 100)) != mapping.ExpirationUtc)
                return Fail("ContractMetadataUnavailable", candidate.ContractId);
            var invalid = OptionPricingQualification.Validate(mapping, at);
            if (invalid is not null)
            {
                if (invalid.Code is "PricingModelUnsupported" or "ExpiredContract") excluded.Add(invalid);
                else return Fail(invalid.Code, candidate.ContractId);
            }
            else accepted.Add(new(mapping, definition.StrikePrice, definition.Right == OptionRightSelection.Call));
        }
        return new(accepted.ToImmutable(), excluded.ToImmutable(), null);
    }
}
