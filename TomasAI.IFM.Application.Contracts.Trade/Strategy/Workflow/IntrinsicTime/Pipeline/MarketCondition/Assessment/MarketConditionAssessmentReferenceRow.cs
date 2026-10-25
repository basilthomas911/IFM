using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record MarketConditionAssessmentReferenceRow
{
    [Key(0)] public string Mode { get; init; } = "MarketAssessment";
    [Key(1)] public int SchemaVersion { get; init; } = 1;
    [Key(2)] public string CaseCode { get; init; } = "";
    [Key(3)] public string CoverageKind { get; init; } = "Representative";
    [Key(4)] public bool IsAuthoritative { get; init; }
    [Key(5)] public MarketConditionAssessmentResult Result { get; init; } = new();
}
