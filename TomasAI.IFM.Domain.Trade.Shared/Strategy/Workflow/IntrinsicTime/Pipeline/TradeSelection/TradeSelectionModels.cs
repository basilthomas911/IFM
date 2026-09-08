using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

public enum SelectionOutcome : byte { Unknown=0, Selected=1, NoTrade=2 }
public enum SelectionRuleStatus : byte { NotApplicable=0, Passed=1, Rejected=2 }
public enum SelectionCandidateStatus : byte { NotEvaluated=0, Ineligible=1, EligibleNotSelected=2, Selected=3 }
public enum CompositionHandoffStatus : byte { None=0, ReservationPending=1, Reserved=2, Stopped=3 }

[MessagePackObject]
public sealed record SelectionProduct
{
    [Key(0)] public int ProductId { get; init; }
    [Key(1)] public string Symbol { get; init; } = string.Empty;
    [Key(2)] public string Exchange { get; init; } = string.Empty;
    [Key(3)] public string Currency { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionCapability
{
    [Key(0)] public string Role { get; init; } = string.Empty;
    [Key(1)] public string Code { get; init; } = string.Empty;
    [Key(2)] public int Version { get; init; }
}

[MessagePackObject]
public sealed record SelectionExpiryGroup
{
    [Key(0)] public string Key { get; init; } = string.Empty;
    [Key(1)] public string? AfterGroup { get; init; }
}

[MessagePackObject]
public sealed record SelectionLeg
{
    [Key(0)] public string Key { get; init; } = string.Empty;
    [Key(1)] public string InstrumentClass { get; init; } = string.Empty;
    [Key(2)] public string Side { get; init; } = string.Empty;
    [Key(3)] public string OptionRight { get; init; } = string.Empty;
    [Key(4)] public decimal Ratio { get; init; }
    [Key(5)] public string ExpiryGroup { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionVariantLeg
{
    [Key(0)] public string LegKey { get; init; } = string.Empty;
    [Key(1)] public string Side { get; init; } = string.Empty;
    [Key(2)] public decimal Ratio { get; init; }
}

[MessagePackObject]
public sealed record SelectionPipelineParameter
{
    [Key(0)] public string Role { get; init; } = string.Empty;
    [Key(1)] public CatalogPipelineParameterKind Kind { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public int Version { get; init; }
    [Key(4)] public string Hash { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionParameterBinding
{
    [Key(0)] public string Role { get; init; } = string.Empty;
    [Key(1)] public CatalogKey ParameterSet { get; init; }
}

[MessagePackObject]
public sealed record SelectionLegacyFamily
{
    [Key(0)] public int Id { get; init; }
    [Key(1)] public long Version { get; init; }
}

[MessagePackObject]
public sealed record SelectionCatalogDefinitionSnapshot
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public CatalogKey Key { get; init; }
    [Key(2)] public string Code { get; init; } = string.Empty;
    [Key(3)] public string Name { get; init; } = string.Empty;
    [Key(4)] public string Description { get; init; } = string.Empty;
    [Key(5)] public short DefinitionSchemaVersion { get; init; }
    [Key(6)] public CatalogKey? Parent { get; init; }
    [Key(7)] public TimeFrameType Horizon { get; init; }
    [Key(8)] public string Side { get; init; } = string.Empty;
    [Key(9)] public string Bias { get; init; } = string.Empty;
    [Key(10)] public string PremiumMode { get; init; } = string.Empty;
    [Key(11)] public string SettingsJson { get; init; } = string.Empty;
    CatalogKey[] _Families = [];
    [Key(12)] public CatalogKey[] Families { get => [.. _Families]; init => _Families = value is null ? [] : [.. value]; }
    CatalogKey[] _Structures = [];
    [Key(13)] public CatalogKey[] Structures { get => [.. _Structures]; init => _Structures = value is null ? [] : [.. value]; }
    CatalogKey[] _Variants = [];
    [Key(14)] public CatalogKey[] Variants { get => [.. _Variants]; init => _Variants = value is null ? [] : [.. value]; }
    SelectionCapability[] _Capabilities = [];
    [Key(15)] public SelectionCapability[] Capabilities { get => [.. _Capabilities]; init => _Capabilities = value is null ? [] : [.. value]; }
    SelectionExpiryGroup[] _ExpiryGroups = [];
    [Key(16)] public SelectionExpiryGroup[] ExpiryGroups { get => [.. _ExpiryGroups]; init => _ExpiryGroups = value is null ? [] : [.. value]; }
    SelectionLeg[] _Legs = [];
    [Key(17)] public SelectionLeg[] Legs { get => [.. _Legs]; init => _Legs = value is null ? [] : [.. value]; }
    SelectionVariantLeg[] _VariantLegs = [];
    [Key(18)] public SelectionVariantLeg[] VariantLegs { get => [.. _VariantLegs]; init => _VariantLegs = value is null ? [] : [.. value]; }
    SelectionProduct[] _Products = [];
    [Key(19)] public SelectionProduct[] Products { get => [.. _Products]; init => _Products = value is null ? [] : [.. value]; }
    SelectionPipelineParameter[] _PipelineParameters = [];
    [Key(20)] public SelectionPipelineParameter[] PipelineParameters { get => [.. _PipelineParameters]; init => _PipelineParameters = value is null ? [] : [.. value]; }
    SelectionParameterBinding[] _Parameters = [];
    [Key(21)] public SelectionParameterBinding[] Parameters { get => [.. _Parameters]; init => _Parameters = value is null ? [] : [.. value]; }
    SelectionLegacyFamily[] _LegacyFamilies = [];
    [Key(22)] public SelectionLegacyFamily[] LegacyFamilies { get => [.. _LegacyFamilies]; init => _LegacyFamilies = value is null ? [] : [.. value]; }
    [Key(23)] public string ContentHash { get; init; } = string.Empty;
    [Key(24)] public CatalogLifecycleStatus Status { get; init; }
    [Key(25)] public DateTime CreatedUtc { get; init; }
    [Key(26)] public string CreatedBy { get; init; } = string.Empty;
    [Key(27)] public DateTime? EffectiveFromUtc { get; init; }
    [Key(28)] public string? PublishedBy { get; init; }
    [Key(29)] public DateTime? RetiredAtUtc { get; init; }
    [Key(30)] public string? RetiredBy { get; init; }
}

[MessagePackObject]
public sealed record SelectionDeploymentSnapshot
{
    [Key(0)] public CatalogKey DeploymentKey { get; init; }
    [Key(1)] public DateTime AsOfUtc { get; init; }
    CatalogKey[] _DefinitionKeys = [];
    [Key(2)] public CatalogKey[] DefinitionKeys { get => [.. _DefinitionKeys]; init => _DefinitionKeys = value is null ? [] : [.. value]; }
    [Key(3)] public string ContentHash { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionPipelinePolicySnapshot
{
    [Key(0)] public CatalogPipelineParameterKind Kind { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public int Version { get; init; }
    [Key(3)] public short SchemaVersion { get; init; }
    [Key(4)] public string PayloadJson { get; init; } = string.Empty;
    [Key(5)] public string PayloadSha256 { get; init; } = string.Empty;
    [Key(6)] public CatalogLifecycleStatus Status { get; init; }
    [Key(7)] public DateTime? EffectiveFromUtc { get; init; }
    [Key(8)] public DateTime? RetiredAtUtc { get; init; }
}

[MessagePackObject]
public sealed record SelectionPipelinePolicyReference
{
    [Key(0)] public CatalogPipelineParameterKind Kind { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public int Version { get; init; }
    [Key(3)] public string PayloadSha256 { get; init; } = string.Empty;
    [Key(4)] public string Role { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionCandidateBinding
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public long AssignmentVersion { get; init; }
    [Key(2)] public CatalogKey DeploymentKey { get; init; }
    [Key(3)] public CatalogKey StrategyKey { get; init; }
    [Key(4)] public CatalogKey StructureKey { get; init; }
    [Key(5)] public CatalogKey VariantKey { get; init; }
    [Key(6)] public SelectionProduct Product { get; init; }
    [Key(7)] public SelectionPipelinePolicyReference SelectionPolicyReference { get; init; }
    [Key(8)] public SelectionPipelinePolicyReference CompositionPolicyReference { get; init; }
    SelectionParameterBinding[] _SpecializedParameterBindings = [];
    [Key(9)] public SelectionParameterBinding[] SpecializedParameterBindings { get => [.. _SpecializedParameterBindings]; init => _SpecializedParameterBindings = value is null ? [] : [.. value]; }
    CatalogKey[] _FamilyKeys = [];
    [Key(10)] public CatalogKey[] FamilyKeys { get => [.. _FamilyKeys]; init => _FamilyKeys = value is null ? [] : [.. value]; }
    [Key(11)] public int AssignmentPriority { get; init; }
    [Key(12)] public string CandidateHash { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionAssignmentExclusion
{
    [Key(0)] public long AssignmentVersion { get; init; }
    [Key(1)] public CatalogKey? DeploymentKey { get; init; }
    string[] _ReasonCodes = [];
    [Key(2)] public string[] ReasonCodes { get => [.. _ReasonCodes]; init => _ReasonCodes = value is null ? [] : [.. value]; }
}

[MessagePackObject]
public sealed record TradeSelectionBinding
{
    [Key(0)] public short SchemaVersion { get; init; }
    PortfolioFundStrategySnapshot _portfolioSnapshot = new();
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    [Newtonsoft.Json.JsonConverter(typeof(SelectionPortfolioSnapshotJsonConverter))]
    [Key(1)] public PortfolioFundStrategySnapshot PortfolioSnapshot { get => _portfolioSnapshot.DefensiveCopy(); init => _portfolioSnapshot = value?.DefensiveCopy() ?? new(); }
    SelectionCatalogDefinitionSnapshot[] _CatalogDefinitions = [];
    [Key(2)] public SelectionCatalogDefinitionSnapshot[] CatalogDefinitions { get => [.. _CatalogDefinitions]; init => _CatalogDefinitions = value is null ? [] : [.. value]; }
    SelectionDeploymentSnapshot[] _DeploymentSnapshots = [];
    [Key(3)] public SelectionDeploymentSnapshot[] DeploymentSnapshots { get => [.. _DeploymentSnapshots]; init => _DeploymentSnapshots = value is null ? [] : [.. value]; }
    SelectionPipelinePolicySnapshot[] _PipelinePolicies = [];
    [Key(4)] public SelectionPipelinePolicySnapshot[] PipelinePolicies { get => [.. _PipelinePolicies]; init => _PipelinePolicies = value is null ? [] : [.. value]; }
    SelectionCandidateBinding[] _Candidates = [];
    [Key(5)] public SelectionCandidateBinding[] Candidates { get => [.. _Candidates]; init => _Candidates = value is null ? [] : [.. value]; }
    SelectionAssignmentExclusion[] _ExcludedAssignments = [];
    [Key(6)] public SelectionAssignmentExclusion[] ExcludedAssignments { get => [.. _ExcludedAssignments]; init => _ExcludedAssignments = value is null ? [] : [.. value]; }
    [Key(7)] public SelectionPipelinePolicyReference CommonPolicy { get; init; }
    [Key(8)] public DateTime FrozenAtUtc { get; init; }
    [Key(9)] public DateTime ValidUntilUtc { get; init; }
    [Key(10)] public DateOnly RequestedTradeDate { get; init; }
    [Key(11)] public string TradeDatePolicy { get; init; } = string.Empty;
    [Key(12)] public string PayloadSha256 { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionCandidateIntent
{
    [Key(0)] public string CandidateHash { get; init; } = string.Empty;
    [Key(1)] public long AssignmentVersion { get; init; }
    [Key(2)] public CatalogKey DeploymentKey { get; init; }
    [Key(3)] public CatalogKey StrategyKey { get; init; }
    [Key(4)] public CatalogKey StructureKey { get; init; }
    [Key(5)] public CatalogKey VariantKey { get; init; }
    [Key(6)] public SelectionProduct Product { get; init; }
    [Key(7)] public string Side { get; init; } = string.Empty;
    [Key(8)] public string Bias { get; init; } = string.Empty;
    [Key(9)] public string PremiumMode { get; init; } = string.Empty;
    [Key(10)] public SelectionPipelinePolicyReference SelectionPolicyReference { get; init; }
    [Key(11)] public SelectionPipelinePolicyReference CompositionPolicyReference { get; init; }
    SelectionParameterBinding[] _SpecializedParameterBindings = [];
    [Key(12)] public SelectionParameterBinding[] SpecializedParameterBindings { get => [.. _SpecializedParameterBindings]; init => _SpecializedParameterBindings = value is null ? [] : [.. value]; }
    CatalogKey[] _FamilyKeys = [];
    [Key(13)] public CatalogKey[] FamilyKeys { get => [.. _FamilyKeys]; init => _FamilyKeys = value is null ? [] : [.. value]; }
}

[MessagePackObject]
public sealed record TradeSelectionDecisionContext
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public StrategyStageResultEnvelope RegimeResultEnvelope { get; init; }
    [Key(2)] public StrategyStageResultEnvelope AssessmentResultEnvelope { get; init; }
    [Key(3)] public TradeSelectionBinding SelectionBinding { get; init; }
}

[MessagePackObject]
public sealed record SelectionRuleEvidence
{
    [Key(0)] public string RuleId { get; init; } = string.Empty;
    [Key(1)] public string FieldPath { get; init; } = string.Empty;
    [Key(2)] public SelectionRuleStatus Status { get; init; }
    [Key(3)] public string ActualJson { get; init; } = string.Empty;
    [Key(4)] public string ExpectedJson { get; init; } = string.Empty;
    [Key(5)] public string ReasonCode { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record SelectionComparisonTuple
{
    [Key(0)] public int Priority { get; init; }
    [Key(1)] public int Preference { get; init; }
    [Key(2)] public CatalogKey Deployment { get; init; }
    [Key(3)] public CatalogKey Strategy { get; init; }
    [Key(4)] public CatalogKey Structure { get; init; }
    [Key(5)] public CatalogKey Variant { get; init; }
    [Key(6)] public int ProductId { get; init; }
    [Key(7)] public long AssignmentVersion { get; init; }
}

[MessagePackObject]
public sealed record SelectionCandidateDecision
{
    [Key(0)] public string CandidateHash { get; init; } = string.Empty;
    [Key(1)] public SelectionCandidateStatus Status { get; init; }
    SelectionRuleEvidence[] _RuleEvidence = [];
    [Key(2)] public SelectionRuleEvidence[] RuleEvidence { get => [.. _RuleEvidence]; init => _RuleEvidence = value is null ? [] : [.. value]; }
    [Key(3)] public SelectionComparisonTuple Comparison { get; init; }
    string[] _ReasonCodes = [];
    [Key(4)] public string[] ReasonCodes { get => [.. _ReasonCodes]; init => _ReasonCodes = value is null ? [] : [.. value]; }
}

[MessagePackObject]
public sealed record TradeSelectionResult
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public Guid InvocationId { get; init; }
    [Key(5)] public long InputWorkflowRevision { get; init; }
    [Key(6)] public Guid TriggerEventId { get; init; }
    [Key(7)] public int PortfolioId { get; init; }
    [Key(8)] public int FundId { get; init; }
    [Key(9)] public TimeFrameType DecisionHorizon { get; init; }
    [Key(10)] public SelectionOutcome Outcome { get; init; }
    [Key(11)] public SelectionCandidateIntent? SelectedCandidate { get; init; }
    [Key(12)] public TradeSelectionDecisionContext DecisionContext { get; init; }
    SelectionRuleEvidence[] _GlobalEvidence = [];
    [Key(13)] public SelectionRuleEvidence[] GlobalEvidence { get => [.. _GlobalEvidence]; init => _GlobalEvidence = value is null ? [] : [.. value]; }
    SelectionCandidateDecision[] _CandidateDecisions = [];
    [Key(14)] public SelectionCandidateDecision[] CandidateDecisions { get => [.. _CandidateDecisions]; init => _CandidateDecisions = value is null ? [] : [.. value]; }
    [Key(15)] public decimal SelectionConfidence { get; init; }
    [Key(16)] public decimal? CompatibilityScore { get; init; }
    [Key(17)] public string PrimaryReasonCode { get; init; } = string.Empty;
    [Key(18)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(19)] public DateTime ProducedAtUtc { get; init; }
    [Key(20)] public DateTime ValidUntilUtc { get; init; }
    [Key(21)] public SelectionPipelinePolicyReference CommonPolicyReference { get; init; }
    [Key(22)] public string SummaryText { get; init; } = string.Empty;
    /// <summary>Copies owned collections; nested records expose only init setters and defensive collection accessors.</summary>
    public TradeSelectionResult CopyContent() => this with
    {
        GlobalEvidence = GlobalEvidence, CandidateDecisions = CandidateDecisions,
        DecisionContext = DecisionContext with { SelectionBinding = DecisionContext.SelectionBinding with { } },
        SelectedCandidate = SelectedCandidate is null ? null : SelectedCandidate with { }
    };

    /// <summary>Fingerprints semantic content while measuring the established uncompressed MessagePack budget.</summary>
    public (string Hash, int Size) ContentFingerprint() =>
        (MarketCondition.Assessment.MarketConditionAssessmentHash.Compute(this),
         TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.MeasureContent(this));

}

[MessagePackObject]
public sealed record WorkflowCompositionHandoffState
{
    [Key(0)] public CompositionHandoffStatus Status { get; init; }
    [Key(1)] public Guid SelectionSourceEventId { get; init; }
    [Key(2)] public long AcceptedSelectionRevision { get; init; }
    ReserveFundOrderCompositionRequest _request;
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    [Key(3)] public ReserveFundOrderCompositionRequest Request { get => Copy(_request); init => _request=Copy(value); }
    FundCompositionReservationResult? _reservation;
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    [Key(4)] public FundCompositionReservationResult? Reservation { get => Copy(_reservation); init => _reservation=Copy(value); }
    static ReserveFundOrderCompositionRequest Copy(ReserveFundOrderCompositionRequest value) => value?.DefensiveCopy()!;
    static FundCompositionReservationResult? Copy(FundCompositionReservationResult? value) => value is null ? null : value with {Trades=[..value.Trades]};
    [Key(5)] public string ReservationRequestSha256 { get; init; } = string.Empty;
    [Key(6)] public DateTime UpdatedAtUtc { get; init; }
}
