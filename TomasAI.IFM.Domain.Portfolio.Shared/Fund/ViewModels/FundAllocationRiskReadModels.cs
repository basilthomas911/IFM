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

[MessagePackObject(AllowPrivate = true)]
public sealed record FundRiskEnvelopeReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public long PortfolioVersion { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public long FundMandateVersion { get; init; }
    [Key(4)] public Guid EnvelopeId { get; init; }
    [Key(5)] public long EnvelopeVersion { get; init; }
    [Key(6)] public FundCapacityState CapacityState { get; init; }
    [Key(7)] public string Currency { get; init; } = "USD";
    [Key(8)] public decimal AllocatedCapital { get; init; }
    [Key(9)] public decimal AvailableCapital { get; init; }
    [Key(10)] public decimal MaximumRiskPerTrade { get; init; }
    [Key(11)] public decimal MaximumAggregateRisk { get; init; }
    [Key(12)] public decimal MaximumMargin { get; init; }
    [Key(13)] public decimal MaximumGrossNotional { get; init; }
    [Key(14)] public int MaximumContracts { get; init; }
    [Key(15)] public int MaximumOpenPositions { get; init; }
    [Key(16)] public decimal? MaximumAbsoluteDelta { get; init; }
    [Key(17)] public decimal? MaximumAbsoluteGamma { get; init; }
    [Key(18)] public decimal? MaximumAbsoluteVega { get; init; }
    [Key(19)] public decimal? MaximumDrawdown { get; init; }
    [Key(20)] public decimal RemainingLossBudget { get; init; }
    [Key(21)] public DateTime EffectiveFromUtc { get; init; }
    [Key(22)] public DateTime ExpiresAtUtc { get; init; }
    [Key(23)] public int SourcePolicyId { get; init; }
    [Key(24)] public long SourcePolicyVersion { get; init; }
    [Key(25)] public DateTime CreatedOnUtc { get; init; }
    [Key(26)] public string CreatedBy { get; init; } = string.Empty;
    [Key(27)] public int SchemaVersion { get; init; } = 2;

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0 || PortfolioVersion <= 0 || FundId <= 0 || FundMandateVersion <= 0) errors.Add("Positive Portfolio/Fund identities and versions are required.");
        if (EnvelopeId == Guid.Empty || EnvelopeVersion <= 0) errors.Add("A versioned envelope identity is required.");
        if (CapacityState == FundCapacityState.Unknown) errors.Add("CapacityState is required.");
        if (string.IsNullOrWhiteSpace(Currency)) errors.Add("Currency is required.");
        if (AllocatedCapital < 0 || AvailableCapital < 0 || AvailableCapital > AllocatedCapital) errors.Add("Capital amounts are invalid.");
        if (MaximumRiskPerTrade < 0 || MaximumAggregateRisk < MaximumRiskPerTrade || MaximumMargin < 0 || MaximumGrossNotional < 0) errors.Add("Risk/margin/notional limits are invalid.");
        if (MaximumContracts < 0 || MaximumOpenPositions < 0 || RemainingLossBudget < 0) errors.Add("Capacity counts/loss budget cannot be negative.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc || ExpiresAtUtc.Kind != DateTimeKind.Utc || ExpiresAtUtc <= EffectiveFromUtc) errors.Add("Envelope effective/expiry times must be ordered UTC values.");
        if (SourcePolicyId <= 0 || SourcePolicyVersion <= 0) errors.Add("A versioned source policy is required.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("UTC audit provenance is required.");
        return errors;
    }

    public bool PermitsNewExposureAt(DateTime atUtc) => CapacityState is FundCapacityState.Available or FundCapacityState.Constrained && atUtc >= EffectiveFromUtc && atUtc < ExpiresAtUtc;
}
