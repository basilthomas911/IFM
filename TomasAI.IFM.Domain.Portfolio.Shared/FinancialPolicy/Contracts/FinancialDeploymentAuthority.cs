using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>One exact permitted deployment; shared Portfolio/Fund limits remain on the Fund authority.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialDeploymentAuthority([property: Key(0)] FinancialAuthorityReference Reference,
    [property: Key(1)] CapacityLimit[] Limits, [property: Key(2)] decimal MaximumRiskPerTrade = 0);
