using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record MarketConditionAssessmentReferenceRow
{
    [Key(0)] public string Mode { get; init; }="MarketAssessment";
    [Key(1)] public int SchemaVersion { get; init; }=1;
    [Key(2)] public string CaseCode { get; init; }="";
    [Key(3)] public string CoverageKind { get; init; }="Representative";
    [Key(4)] public bool IsAuthoritative { get; init; }
    [Key(5)] public MarketConditionAssessmentResult Result { get; init; }=new();
}
[MessagePackObject(AllowPrivate = true)]
public sealed record GetMarketConditionAssessmentReferenceQuery:IQuery<MarketConditionAssessmentReferenceRow[]>
{

    /// <summary>Creates an empty query for serialization and existing callers.</summary>
    public GetMarketConditionAssessmentReferenceQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    [SerializationConstructor]
    public GetMarketConditionAssessmentReferenceQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = entityId;
    }
    public const string Actor=GetMarketConditionAssessmentQuery.Actor;
    public const string Verb="GetAssessmentReference";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }=ActorEntityId.Default;
    [IgnoreMember] public int ErrorCode=>23222;
    [IgnoreMember] public string? QueryParams { get; init; }
}
