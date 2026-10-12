using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Portfolio-owned spending authority; its source versions must still match committed configuration.</summary>
[MessagePackObject]
public sealed record FinancialFundAuthority
{
    [Key(0)] public int FundId { get; init; }
    [Key(1)] public bool CanSpend { get; init; }
    [Key(2)] public FinancialAuthorityReference Reference { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(3)] public CapacityLimit[] Limits { get; init; } = [];
    [Key(4)] public long PortfolioStreamVersion { get; init; }
    [Key(5)] public long FundStreamVersion { get; init; }
    [Key(6)] public long PolicyStreamVersion { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(7)] public FinancialDeploymentAuthority[] Deployments { get; init; } = [];
}
