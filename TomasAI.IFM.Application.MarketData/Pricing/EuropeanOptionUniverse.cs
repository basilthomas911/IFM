using TomasAI.IFM.Shared.MarketData.Definitions;
using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Qualifies a complete bounded scope before subscriptions. Raw definitions remain untouched.</summary>
public sealed class EuropeanOptionUniverse(IOptionPricingConventionStore conventions)
{
    public async Task<EuropeanOptionUniverseResult> QualifyAsync(IReadOnlyList<OptionDefinitionCandidate> scope,
        bool complete, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        EuropeanOptionUniverseResult Fail(string code, string id) => new([], [], new(code, "Definitions", id, "Complete qualified option scope is unavailable."));
        if (!complete) return Fail("IncompleteChain", "");
        if (scope.Count > 2048) return Fail("SnapshotLimit", "");
        if (scope.Select(x => x.ContractId).Distinct(StringComparer.Ordinal).Count() != scope.Count
            || scope.Select(x => x.Definition.Instrument).Distinct().Count() != scope.Count)
            return Fail("ConflictingDefinition", "");
        var ordered = scope.OrderBy(x => x.ContractId, StringComparer.Ordinal).ToArray();
        var mappings = new OptionPricingConvention?[ordered.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, ordered.Length), new ParallelOptions
        {
            MaxDegreeOfParallelism = 8,
            CancellationToken = cancellationToken
        }, async (index, token) =>
        {
            var candidate = ordered[index];
            mappings[index] = await conventions.GetAsync(candidate.ContractId, candidate.MappingVersion, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
        var accepted = ImmutableArray.CreateBuilder<QualifiedOptionDefinition>();
        var excluded = ImmutableArray.CreateBuilder<OptionPricingFailure>();
        for (var index = 0; index < ordered.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = ordered[index];
            var definition = candidate.Definition;
            var mapping = mappings[index];
            if (mapping is null || !string.Equals(mapping.ContractId,candidate.ContractId,StringComparison.Ordinal) || !string.Equals(mapping.MappingVersion,candidate.MappingVersion,StringComparison.Ordinal)
                || !string.Equals(mapping.DefinitionDigest,candidate.DefinitionDigest,StringComparison.Ordinal) || !string.Equals(mapping.Dataset,definition.Dataset,StringComparison.Ordinal)
                || mapping.InstrumentId != definition.Instrument.InstrumentId
                || mapping.PublisherId != definition.Instrument.PublisherId
                || mapping.SchemaVersion == 3 && (mapping.Strike != definition.StrikePrice
                    || mapping.Right != (definition.Right == OptionRightSelection.Call ? PricingOptionRight.Call : PricingOptionRight.Put))
                || !string.Equals(mapping.RawSymbol,definition.RawSymbol,StringComparison.Ordinal) || !string.Equals(mapping.UnderlyingContractId,definition.Underlying,StringComparison.Ordinal) || definition.StrikePrice <= 0
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
