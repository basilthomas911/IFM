using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record TradeFamilyRiskLimitReadModel
{
    [Key(0)] public int TradeStrategyFamilyId { get; init; }
    [Key(1)] public long DefinitionVersion { get; init; }
    [Key(2)] public bool Enabled { get; init; }
    [Key(3)] public decimal MaximumRiskPerTrade { get; init; }
    [Key(4)] public decimal MaximumAggregateRisk { get; init; }
    [Key(5)] public decimal MaximumMargin { get; init; }
    [Key(6)] public decimal MaximumGrossNotional { get; init; }
    [Key(7)] public int MaximumOpenPositions { get; init; }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (TradeStrategyFamilyId <= 0 || DefinitionVersion <= 0) errors.Add("A versioned TradeStrategyFamily identity is required.");
        if (MaximumRiskPerTrade < 0 || MaximumAggregateRisk < 0 || MaximumMargin < 0 || MaximumGrossNotional < 0 || MaximumOpenPositions < 0)
            errors.Add("Trade-family limits cannot be negative.");
        if (MaximumRiskPerTrade > MaximumAggregateRisk) errors.Add("Family MaximumRiskPerTrade cannot exceed MaximumAggregateRisk.");
        return errors;
    }
}

[MessagePackObject(AllowPrivate = true)]
public sealed record EffectiveTradeFamilyRiskCaps
{
    [Key(0)] public int TradeStrategyFamilyId { get; init; }
    [Key(1)] public long DefinitionVersion { get; init; }
    [Key(2)] public bool PermitsNewExposure { get; init; }
    [Key(3)] public decimal MaximumRiskPerTrade { get; init; }
    [Key(4)] public decimal MaximumAggregateRisk { get; init; }
    [Key(5)] public decimal MaximumMargin { get; init; }
    [Key(6)] public decimal MaximumGrossNotional { get; init; }
    [Key(7)] public int MaximumOpenPositions { get; init; }
}

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFinancialPolicyReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int PolicyId { get; init; }
    [Key(2)] public long PolicyVersion { get; init; }
    [Key(3)] public int SchemaVersion { get; init; } = 1;
    [Key(4)] public string Name { get; init; } = string.Empty;
    [Key(5)] public PortfolioFinancialPolicyState OperatingState { get; init; }
    [Key(6)] public string BaseCurrency { get; init; } = "USD";
    [Key(7)] public decimal CapitalBase { get; init; }
    [Key(8)] public decimal ProtectedReserve { get; init; }
    [Key(9)] public decimal MaximumDeployableCapital { get; init; }
    [Key(10)] public decimal MaximumRiskPerTrade { get; init; }
    [Key(11)] public decimal MaximumAggregateRisk { get; init; }
    [Key(12)] public decimal MaximumMargin { get; init; }
    [Key(13)] public decimal MaximumGrossNotional { get; init; }
    [Key(14)] public int MaximumOpenPositions { get; init; }
    [Key(15)] public decimal MaximumDrawdownAmount { get; init; }
    [Key(16)] public TradeFamilyRiskLimitReadModel[] TradeFamilyLimits { get; init; } = [];
    [Key(17)] public DateTime EffectiveFromUtc { get; init; }
    [Key(18)] public DateTime? EffectiveUntilUtc { get; init; }
    [Key(19)] public DateTime CreatedOnUtc { get; init; }
    [Key(20)] public string CreatedBy { get; init; } = string.Empty;
    [Key(21)] public DateTime? SupersededOnUtc { get; init; }
    [Key(22)] public string SupersededBy { get; init; } = string.Empty;
    [Key(23)] public long AggregateRevision { get; init; }

    public IReadOnlyList<string> Validate(bool forActivation = false)
    {
        List<string> errors = [];
        if (PortfolioId <= 0 || PolicyId <= 0 || PolicyVersion <= 0) errors.Add("Positive Portfolio/policy identities and version are required.");
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(BaseCurrency)) errors.Add("Policy name and base currency are required.");
        if (OperatingState is PortfolioFinancialPolicyState.Unknown or PortfolioFinancialPolicyState.Deleted) errors.Add("A persisted policy requires a valid operating state.");
        if (CapitalBase < 0 || ProtectedReserve < 0 || MaximumDeployableCapital < 0 || MaximumRiskPerTrade < 0 || MaximumAggregateRisk < 0 || MaximumMargin < 0 || MaximumGrossNotional < 0 || MaximumOpenPositions < 0 || MaximumDrawdownAmount < 0)
            errors.Add("Financial policy limits cannot be negative.");
        if (ProtectedReserve > CapitalBase) errors.Add("ProtectedReserve cannot exceed CapitalBase.");
        if (MaximumDeployableCapital > CapitalBase - ProtectedReserve) errors.Add("MaximumDeployableCapital exceeds available capital.");
        if (MaximumRiskPerTrade > MaximumAggregateRisk) errors.Add("MaximumRiskPerTrade cannot exceed MaximumAggregateRisk.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc || EffectiveUntilUtc is { Kind: not DateTimeKind.Utc }) errors.Add("Policy effective dates must be UTC.");
        if (EffectiveUntilUtc is { } until && until <= EffectiveFromUtc) errors.Add("EffectiveUntilUtc must follow EffectiveFromUtc.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("UTC audit provenance is required.");
        if (TradeFamilyLimits.Length == 0) errors.Add("At least one TradeStrategyFamily definition is required.");
        foreach (var family in TradeFamilyLimits) errors.AddRange(family.Validate());
        if (TradeFamilyLimits.GroupBy(x => (x.TradeStrategyFamilyId, x.DefinitionVersion)).Any(x => x.Count() > 1)) errors.Add("TradeStrategyFamily definitions must be unique.");
        foreach (var family in TradeFamilyLimits.Where(x => x.Enabled))
        {
            if (family.MaximumRiskPerTrade > MaximumRiskPerTrade || family.MaximumAggregateRisk > MaximumAggregateRisk || family.MaximumMargin > MaximumMargin || family.MaximumGrossNotional > MaximumGrossNotional || family.MaximumOpenPositions > MaximumOpenPositions)
                errors.Add($"TradeStrategyFamily {family.TradeStrategyFamilyId} exceeds a Portfolio-wide limit.");
        }
        if (forActivation || OperatingState == PortfolioFinancialPolicyState.Active)
        {
            if (CapitalBase <= 0 || MaximumOpenPositions <= 0) errors.Add("An Active policy requires positive CapitalBase and MaximumOpenPositions.");
            if (!TradeFamilyLimits.Any(x => x.Enabled)) errors.Add("An Active policy requires at least one enabled TradeStrategyFamily.");
        }
        return errors;
    }

    public PortfolioFinancialPolicyReadModel DefensiveCopy() => this with
    {
        TradeFamilyLimits = [.. TradeFamilyLimits.OrderBy(x => x.TradeStrategyFamilyId).ThenBy(x => x.DefinitionVersion)]
    };

    public string CanonicalSha256()
    {
        var json = JsonSerializer.Serialize(DefensiveCopy());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    /// <summary>
    /// Resolves the immutable caps seen by a pipeline decision. Every numeric cap is
    /// the most restrictive of Portfolio-wide, trade-family, and Fund-envelope limits.
    /// A disabled family, a zero cap, or a non-permitting envelope fails closed.
    /// </summary>
    public EffectiveTradeFamilyRiskCaps ResolveEffectiveCaps(
        int tradeStrategyFamilyId,
        long definitionVersion,
        FundRiskEnvelopeReadModel envelope,
        DateTime effectiveAtUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (effectiveAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Effective time must be UTC.", nameof(effectiveAtUtc));
        var family = TradeFamilyLimits.SingleOrDefault(x =>
            x.TradeStrategyFamilyId == tradeStrategyFamilyId && x.DefinitionVersion == definitionVersion)
            ?? throw new InvalidOperationException("The exact TradeStrategyFamily definition is not configured by this policy.");
        if (envelope.SourcePolicyId != PolicyId || envelope.SourcePolicyVersion != PolicyVersion)
            throw new InvalidOperationException("The Fund risk envelope does not reference this exact policy version.");

        var caps = new EffectiveTradeFamilyRiskCaps
        {
            TradeStrategyFamilyId = tradeStrategyFamilyId,
            DefinitionVersion = definitionVersion,
            MaximumRiskPerTrade = Math.Min(Math.Min(MaximumRiskPerTrade, family.MaximumRiskPerTrade), envelope.MaximumRiskPerTrade),
            MaximumAggregateRisk = Math.Min(Math.Min(MaximumAggregateRisk, family.MaximumAggregateRisk), envelope.MaximumAggregateRisk),
            MaximumMargin = Math.Min(Math.Min(MaximumMargin, family.MaximumMargin), envelope.MaximumMargin),
            MaximumGrossNotional = Math.Min(Math.Min(MaximumGrossNotional, family.MaximumGrossNotional), envelope.MaximumGrossNotional),
            MaximumOpenPositions = Math.Min(Math.Min(MaximumOpenPositions, family.MaximumOpenPositions), envelope.MaximumOpenPositions),
        };
        return caps with
        {
            PermitsNewExposure = family.Enabled
                && envelope.PermitsNewExposureAt(effectiveAtUtc)
                && caps.MaximumRiskPerTrade > 0
                && caps.MaximumAggregateRisk > 0
                && caps.MaximumMargin > 0
                && caps.MaximumGrossNotional > 0
                && caps.MaximumOpenPositions > 0
        };
    }
}
