using System.Globalization;
using System.Text.Json;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Preserves immutable provider ownership when different options share the legacy expiry/right/strike label.</summary>
public static class OptionReferenceIdentity
{
    /// <summary>Uses the existing persisted identity field order and representation.</summary>
    public static string Serialize(FuturesOptionContractReadModel value) => JsonSerializer.Serialize(new
    {
        value.ContractId, value.Dataset, value.PublisherId, value.InstrumentId, value.Symbol, value.ContractMonth, value.OptionType,
        Strike = value.GetExactStrikePrice(), value.UnderlyingContractId, value.UnderlyingInstrumentId, value.UnderlyingPublisherId
    });

    /// <summary>Scopes a provider ID to the option expiry; venues can reuse IDs after expiration.</summary>
    public static string ProviderKey(FuturesOptionContractReadModel value)
        => string.Create(CultureInfo.InvariantCulture, $"{LegacyProviderKey(value)}:option:{value.ContractMonth:yyyy-MM-dd}");

    /// <summary>Identifies pre-lifecycle bindings, which remain immutable and readable.</summary>
    public static string LegacyProviderKey(FuturesOptionContractReadModel value)
        => string.Create(CultureInfo.InvariantCulture, $"provider:{value.Dataset}:{value.PublisherId}:{value.InstrumentId}");

    /// <summary>Returns an old binding only when it belongs to this lifecycle; rejects unproven reuse.</summary>
    public static string? ExistingProviderBinding(FuturesOptionContractReadModel candidate, IReadOnlyDictionary<string, string> bindings)
    {
        if (bindings.TryGetValue(ProviderKey(candidate), out var scoped)) return scoped;
        if (!bindings.TryGetValue(LegacyProviderKey(candidate), out var legacy)) return null;
        using var document = JsonDocument.Parse(legacy);
        var expiry = DateOnly.ParseExact(document.RootElement.GetProperty("ContractMonth").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (expiry == candidate.ContractMonth) return legacy;
        // A new expiry alone is insufficient: the provider's actual definition must postdate the old lifecycle.
        if (candidate.ContractMonth > expiry && candidate.DefinitionTimestampUtc is { Offset: var offset } defined && offset == TimeSpan.Zero
            && DateOnly.FromDateTime(defined.UtcDateTime) > expiry
            && defined < candidate.ExpirationUtc
            && candidate.DefinitionDigest is { Length: 64 } && candidate.DefinitionDigest.All(Uri.IsHexDigit)
            && !string.IsNullOrWhiteSpace(candidate.RawDefinitionReference)) return null;
        throw new InvalidDataException($"Option provider ID reuse is not proven for {LegacyProviderKey(candidate)}.");
    }

    /// <summary>Resolves names before publication. Existing bindings must match every identity field exactly.</summary>
    public static FuturesOptionContractReadModel[] Resolve(IReadOnlyList<FuturesOptionContractReadModel> candidates, IReadOnlyDictionary<string, string> bindings)
    {
        var ambiguous = candidates.GroupBy(x => x.ContractId, StringComparer.Ordinal)
            .Where(g => g.Select(ProviderKey).Distinct(StringComparer.Ordinal).Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var resolved = new Dictionary<string, FuturesOptionContractReadModel>(StringComparer.Ordinal);
        return candidates.Select(candidate =>
        {
            if (candidate.PublisherId is not > 0 || candidate.InstrumentId is not > 0 || string.IsNullOrWhiteSpace(candidate.Dataset))
                throw new InvalidDataException($"Option '{candidate.ContractId}' is missing its exact provider identity.");
            var provider = ProviderKey(candidate);
            FuturesOptionContractReadModel value;
            var existing = ExistingProviderBinding(candidate, bindings);
            if (existing is not null)
            {
                using var document = JsonDocument.Parse(existing);
                var id = document.RootElement.GetProperty("ContractId").GetString() ?? throw new InvalidDataException("Missing stored option identity.");
                value = candidate with { ContractId = id };
                if (Serialize(value) != existing)
                    throw new InvalidDataException($"Option identity conflict for {provider}: the stored binding differs from '{candidate.ContractId}'.");
            }
            else
            {
                var occupied = bindings.TryGetValue("ifm:" + candidate.ContractId, out var owner) && owner != Serialize(candidate);
                value = occupied || ambiguous.Contains(candidate.ContractId)
                    ? candidate with { ContractId = FuturesOptionContractId.WithProviderIdentity(candidate.ContractId, candidate.PublisherId.Value, candidate.InstrumentId.Value) }
                    : candidate;
            }
            if (bindings.TryGetValue("ifm:" + value.ContractId, out var claimed) && claimed != Serialize(value))
                throw new InvalidDataException($"Option IFM identity '{value.ContractId}' is already bound to a different instrument.");
            if (resolved.TryGetValue(value.ContractId, out var previous) && Serialize(previous) != Serialize(value))
                throw new InvalidDataException($"Multiple option instruments resolved to '{value.ContractId}'.");
            resolved[value.ContractId] = value;
            return value;
        }).ToArray();
    }
}
