using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Exact selected legs pinned to persisted reconstruction inputs, independent of discovery ownership.</summary>
[MessagePackObject]
public sealed record CompositionContractSelection(
    [property: Key(0)] string PricingPlanId,
    [property: Key(1)] ImmutableArray<string> ContractIds)
{
    public void Validate()
    {
        if (PricingPlanId is not { Length: 64 } || !PricingPlanId.All(Uri.IsHexDigit)
            || ContractIds.IsDefaultOrEmpty || ContractIds.Length > 4
            || ContractIds.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 256 || x != x.Trim())
            || ContractIds.Distinct(StringComparer.Ordinal).Count() != ContractIds.Length)
            throw new InvalidDataException("An exact bounded selected-contract set and reconstruction plan are required.");
    }
}
