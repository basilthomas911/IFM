using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public string PortfolioCode { get; init; } = string.Empty;
    [Key(2)] public string Name { get; init; } = string.Empty;
    [Key(3)] public long PortfolioVersion { get; init; }
    [Key(4)] public int SchemaVersion { get; init; } = 1;
    [Key(5)] public string BaseCurrency { get; init; } = "USD";
    [Key(6)] public PortfolioOperatingState OperatingState { get; init; }
    [Key(7)] public DateTime EffectiveFromUtc { get; init; }
    [Key(8)] public DateTime? EffectiveUntilUtc { get; init; }
    [Key(9)] public Guid PolicyId { get; init; }
    [Key(10)] public long PolicyVersion { get; init; }
    [Key(11)] public string[] BrokerAccountRefs { get; init; } = [];
    [Key(12)] public DateTime CreatedOnUtc { get; init; }
    [Key(13)] public string CreatedBy { get; init; } = string.Empty;
    [Key(14)] public DateTime? SupersededOnUtc { get; init; }
    [Key(15)] public string SupersededBy { get; init; } = string.Empty;

    public IReadOnlyList<string> Validate(bool requireActivePolicy = true)
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioId must be greater than zero.");
        if (string.IsNullOrWhiteSpace(PortfolioCode)) errors.Add("PortfolioCode is required.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Name is required.");
        if (PortfolioVersion <= 0) errors.Add("PortfolioVersion must be greater than zero.");
        if (SchemaVersion <= 0) errors.Add("SchemaVersion must be greater than zero.");
        if (string.IsNullOrWhiteSpace(BaseCurrency)) errors.Add("BaseCurrency is required.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc) errors.Add("EffectiveFromUtc must be UTC.");
        if (EffectiveUntilUtc is { } until && (until.Kind != DateTimeKind.Utc || until <= EffectiveFromUtc))
            errors.Add("EffectiveUntilUtc must be UTC and after EffectiveFromUtc.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc) errors.Add("CreatedOnUtc must be UTC.");
        if (string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("CreatedBy is required.");
        if (OperatingState == PortfolioOperatingState.Unknown) errors.Add("OperatingState is required.");
        if (requireActivePolicy && OperatingState == PortfolioOperatingState.Active)
        {
            if (PolicyId == Guid.Empty) errors.Add("An active Portfolio requires PolicyId.");
            if (PolicyVersion <= 0) errors.Add("An active Portfolio requires a positive PolicyVersion.");
        }
        if (BrokerAccountRefs.Any(string.IsNullOrWhiteSpace)) errors.Add("BrokerAccountRefs cannot contain blanks.");
        return errors;
    }

    public PortfolioReadModel DefensiveCopy() => this with { BrokerAccountRefs = [.. BrokerAccountRefs] };
}

[MessagePackObject(AllowPrivate = true)]
public sealed record FundMandateReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public string FundCode { get; init; } = string.Empty;
    [Key(3)] public string Name { get; init; } = string.Empty;
    [Key(4)] public long FundMandateVersion { get; init; }
    [Key(5)] public int SchemaVersion { get; init; } = 1;
    [Key(6)] public int TradingYear { get; init; }
    [Key(7)] public FundOperatingState OperatingState { get; init; }
    [Key(8)] public DateTime EffectiveFromUtc { get; init; }
    [Key(9)] public DateTime? EffectiveUntilUtc { get; init; }
    [Key(10)] public string DecisionHorizon { get; init; } = string.Empty;
    [Key(11)] public string Objective { get; init; } = string.Empty;
    [Key(12)] public string[] UnderlyingUniverse { get; init; } = [];
    [Key(13)] public string[] EligibleAssetTypes { get; init; } = [];
    [Key(14)] public string[] PermittedDirections { get; init; } = [];
    [Key(15)] public string[] PermittedConditions { get; init; } = [];
    [Key(16)] public string[] PermittedTradeFamilies { get; init; } = [];
    [Key(17)] public DateTime CreatedOnUtc { get; init; }
    [Key(18)] public string CreatedBy { get; init; } = string.Empty;

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioId must be greater than zero.");
        if (FundId <= 0) errors.Add("FundId must be greater than zero.");
        if (string.IsNullOrWhiteSpace(FundCode)) errors.Add("FundCode is required.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Name is required.");
        if (FundMandateVersion <= 0) errors.Add("FundMandateVersion must be greater than zero.");
        if (TradingYear is < 2000 or > 2200) errors.Add("TradingYear is outside the supported range.");
        if (OperatingState == FundOperatingState.Unknown) errors.Add("OperatingState is required.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc) errors.Add("EffectiveFromUtc must be UTC.");
        if (EffectiveUntilUtc is { } until && (until.Kind != DateTimeKind.Utc || until <= EffectiveFromUtc))
            errors.Add("EffectiveUntilUtc must be UTC and after EffectiveFromUtc.");
        if (string.IsNullOrWhiteSpace(DecisionHorizon)) errors.Add("DecisionHorizon is required.");
        if (string.IsNullOrWhiteSpace(Objective)) errors.Add("Objective is required.");
        if (UnderlyingUniverse.Length == 0 || UnderlyingUniverse.Any(string.IsNullOrWhiteSpace)) errors.Add("UnderlyingUniverse is required.");
        if (EligibleAssetTypes.Length == 0 || EligibleAssetTypes.Any(string.IsNullOrWhiteSpace)) errors.Add("EligibleAssetTypes is required.");
        if (PermittedTradeFamilies.Length == 0 || PermittedTradeFamilies.Any(string.IsNullOrWhiteSpace)) errors.Add("PermittedTradeFamilies is required.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc) errors.Add("CreatedOnUtc must be UTC.");
        if (string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("CreatedBy is required.");
        return errors;
    }

    public FundMandateReadModel DefensiveCopy() => this with
    {
        UnderlyingUniverse = [.. UnderlyingUniverse],
        EligibleAssetTypes = [.. EligibleAssetTypes],
        PermittedDirections = [.. PermittedDirections],
        PermittedConditions = [.. PermittedConditions],
        PermittedTradeFamilies = [.. PermittedTradeFamilies],
    };
}
