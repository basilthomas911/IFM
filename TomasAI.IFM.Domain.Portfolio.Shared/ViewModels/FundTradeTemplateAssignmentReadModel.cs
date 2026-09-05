using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record FundTradeTemplateAssignmentReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public long PortfolioVersion { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public long FundMandateVersion { get; init; }
    [Key(4)] public long AssignmentVersion { get; init; }
    [Key(5)] public Guid TradeTemplateId { get; init; }
    [Key(6)] public long TradeTemplateVersion { get; init; }
    [Key(7)] public bool Enabled { get; init; }
    [Key(8)] public string DecisionHorizon { get; init; } = string.Empty;
    [Key(9)] public string[] UnderlyingUniverse { get; init; } = [];
    [Key(10)] public string AssetType { get; init; } = string.Empty;
    [Key(11)] public string TradeFamily { get; init; } = string.Empty;
    [Key(12)] public int Priority { get; init; }
    [Key(13)] public DateTime EffectiveFromUtc { get; init; }
    [Key(14)] public DateTime? EffectiveUntilUtc { get; init; }
    [Key(15)] public Guid TradeSelectionHintProfileId { get; init; }
    [Key(16)] public long TradeSelectionHintProfileVersion { get; init; }
    [Key(17)] public Guid OrderCompositionProfileId { get; init; }
    [Key(18)] public long OrderCompositionProfileVersion { get; init; }
    [Key(19)] public DateTime CreatedOnUtc { get; init; }
    [Key(20)] public string CreatedBy { get; init; } = string.Empty;
    [Key(21)] public int SchemaVersion { get; init; } = 1;
    [Key(22)] public TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReference? TradeStrategyFamily { get; init; }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0 || PortfolioVersion <= 0) errors.Add("A positive Portfolio identity/version is required.");
        if (FundId <= 0 || FundMandateVersion <= 0) errors.Add("A positive Fund identity/version is required.");
        if (AssignmentVersion <= 0) errors.Add("AssignmentVersion must be positive.");
        if (TradeTemplateId == Guid.Empty || TradeTemplateVersion <= 0) errors.Add("A versioned TradeTemplate is required.");
        if (string.IsNullOrWhiteSpace(DecisionHorizon)) errors.Add("DecisionHorizon is required.");
        if (UnderlyingUniverse.Length == 0 || UnderlyingUniverse.Any(string.IsNullOrWhiteSpace)) errors.Add("UnderlyingUniverse is required.");
        if (string.IsNullOrWhiteSpace(AssetType)) errors.Add("AssetType is required.");
        if (string.IsNullOrWhiteSpace(TradeFamily)) errors.Add("TradeFamily is required.");
        if ((SchemaVersion >= 2 && TradeStrategyFamily is null) || TradeStrategyFamily is { IsValid: false })
            errors.Add("An exact trade strategy family ID/version is required.");
        if (Priority < 0) errors.Add("Priority cannot be negative.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc) errors.Add("EffectiveFromUtc must be UTC.");
        if (EffectiveUntilUtc is { } until && (until.Kind != DateTimeKind.Utc || until <= EffectiveFromUtc)) errors.Add("EffectiveUntilUtc must be UTC and after EffectiveFromUtc.");
        if (TradeSelectionHintProfileId == Guid.Empty || TradeSelectionHintProfileVersion <= 0) errors.Add("A versioned TradeSelectionHintProfile is required.");
        if (OrderCompositionProfileId == Guid.Empty || OrderCompositionProfileVersion <= 0) errors.Add("A versioned OrderCompositionProfile is required.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("UTC audit provenance is required.");
        return errors;
    }

    public bool IsEffectiveAt(DateTime atUtc) => Enabled && atUtc >= EffectiveFromUtc && (EffectiveUntilUtc is null || atUtc < EffectiveUntilUtc);

    public FundTradeTemplateAssignmentReadModel DefensiveCopy() => this with { UnderlyingUniverse = [.. UnderlyingUniverse] };
}
