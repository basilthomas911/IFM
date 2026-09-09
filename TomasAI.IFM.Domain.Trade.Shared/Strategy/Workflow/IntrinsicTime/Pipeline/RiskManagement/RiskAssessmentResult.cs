using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

public enum RiskAssessmentOutcome { Undefined=0, Approved=1, Rejected=2 }

[MessagePackObject]
public sealed record RiskSizedLeg([property: Key(0)] string InstrumentId, [property: Key(1)] string Side,
    [property: Key(2)] int Contracts, [property: Key(3)] long TradeId);

/// <summary>An immutable sizing proposal; Approved is not execution authority until the exact capacity receipt is accepted.</summary>
[MessagePackObject(AllowPrivate=true)]
public sealed record RiskAssessmentResult
{
    [IgnoreMember, Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    CapacityRequirements? _requirements;
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public Guid InvocationId { get; init; }
    [Key(5)] public long InputWorkflowRevision { get; init; }
    [Key(6)] public Guid CompositionResultId { get; init; }
    [Key(7)] public string CompositionResultHash { get; init; } = "";
    [Key(8)] public string UnitCandidateHash { get; init; } = "";
    [Key(9)] public string SizedOrderHash { get; init; } = "";
    [Key(10)] public long OrderId { get; init; }
    [Key(11)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(12)] public DateTime ProducedAtUtc { get; init; }
    [Key(13)] public DateTime ValidUntilUtc { get; init; }
    [Key(14)] public RiskAssessmentOutcome Outcome { get; init; }
    [Key(15)] public int StrategyUnits { get; init; }
    [Key(16)] public RiskUnitResult? UnitRisk { get; init; }
    [Key(17)] public CapacityRequirements? Requirements
    {
        get => _requirements is null ? null : _requirements with { Exposures=[.._requirements.Exposures] };
        init => _requirements = value is null ? null : value with { Exposures=[..value.Exposures] };
    }
    [Key(18)] public FinancialAuthorityReference Authority { get; init; } = new();
    [Key(19)] public FinancialEvidenceReference? MarginEvidence { get; init; }
    [Key(20)] public string Environment { get; init; } = "";
    [Key(21)] public int PortfolioId { get; init; }
    [Key(22)] public int FundId { get; init; }
    [Key(23)] public TimeFrameType TargetHorizon { get; init; }
    [Key(24)] public ImmutableArray<RiskSizedLeg> Legs { get; init; } = [];
    [Key(25)] public ImmutableArray<string> Reasons { get; init; } = [];
    [Key(26)] public string InputHash { get; init; } = "";
    [Key(27)] public string PolicyHash { get; init; } = "";

    public QualifiedCapacityAssessment ToCapacityAssessment() => new()
    {
        InvocationId=InvocationId, ResultId=ResultId, ResultHash=RiskContracts.Hash(this), PortfolioId=PortfolioId, FundId=FundId,
        WorkflowId=WorkflowId.Value, WorkflowRevision=InputWorkflowRevision, CompositionResultId=CompositionResultId,
        CompositionResultHash=CompositionResultHash, UnitCandidateHash=UnitCandidateHash, SizedOrderHash=SizedOrderHash,
        StrategyUnits=StrategyUnits, Requirements=Requirements ?? new(), Authority=Authority, MarginEvidence=MarginEvidence ?? new(),
        Environment=Environment, ValidUntilUtc=ValidUntilUtc, Eligible=Outcome==RiskAssessmentOutcome.Approved
    };
}
