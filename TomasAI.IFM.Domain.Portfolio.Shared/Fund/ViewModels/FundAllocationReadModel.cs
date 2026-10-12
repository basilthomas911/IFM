using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record FundAllocationReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public long PortfolioVersion { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public long FundMandateVersion { get; init; }
    [Key(4)] public long AllocationVersion { get; init; }
    [Key(5)] public decimal TargetWeight { get; init; }
    [Key(6)] public decimal MinimumWeight { get; init; }
    [Key(7)] public decimal MaximumWeight { get; init; }
    [Key(8)] public decimal AllocatedCapital { get; init; }
    [Key(9)] public string Currency { get; init; } = "USD";
    [Key(10)] public DateTime EffectiveFromUtc { get; init; }
    [Key(11)] public DateTime? EffectiveUntilUtc { get; init; }
    [Key(12)] public long SourcePolicyVersion { get; init; }
    [Key(13)] public DateTime CreatedOnUtc { get; init; }
    [Key(14)] public string CreatedBy { get; init; } = string.Empty;
    [Key(15)] public int SchemaVersion { get; init; } = 2;
    [Key(16)] public int SourcePolicyId { get; init; }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0 || PortfolioVersion <= 0 || FundId <= 0 || FundMandateVersion <= 0) errors.Add("Positive Portfolio/Fund identities and versions are required.");
        if (AllocationVersion <= 0 || SourcePolicyId <= 0 || SourcePolicyVersion <= 0) errors.Add("Positive allocation and policy identities/versions are required.");
        if (MinimumWeight < 0 || TargetWeight < MinimumWeight || MaximumWeight < TargetWeight || MaximumWeight > 1) errors.Add("Allocation weights must satisfy 0 <= minimum <= target <= maximum <= 1.");
        if (AllocatedCapital < 0) errors.Add("AllocatedCapital cannot be negative.");
        if (string.IsNullOrWhiteSpace(Currency)) errors.Add("Currency is required.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc) errors.Add("EffectiveFromUtc must be UTC.");
        if (EffectiveUntilUtc is { } until && (until.Kind != DateTimeKind.Utc || until <= EffectiveFromUtc)) errors.Add("EffectiveUntilUtc must be UTC and after EffectiveFromUtc.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("UTC audit provenance is required.");
        return errors;
    }
}
