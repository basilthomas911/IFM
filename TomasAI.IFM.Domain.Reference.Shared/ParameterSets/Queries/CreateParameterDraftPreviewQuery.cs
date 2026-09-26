using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject(AllowPrivate = true)]
public sealed record CreateParameterDraftPreviewQuery:IQuery<string>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public CreateParameterDraftPreviewQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="setId">The SetId field.</param>
    /// <param name="componentCode">The ComponentCode field.</param>
    /// <param name="payloadJson">The PayloadJson field.</param>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="targetHorizon">The TargetHorizon field.</param>
    /// <param name="rebuildIntervals">The RebuildIntervals field.</param>
    [SerializationConstructor]
    public CreateParameterDraftPreviewQuery(ActorSubject subject, IActorEntityId entityId, Guid setId, string componentCode, string payloadJson, int schemaVersion, int targetHorizon, bool rebuildIntervals)
    {
        Subject = subject;
        EntityId = entityId;
        SetId = setId;
        ComponentCode = componentCode;
        PayloadJson = payloadJson;
        SchemaVersion = schemaVersion;
        TargetHorizon = targetHorizon;
        RebuildIntervals = rebuildIntervals;
    }
 public const string Actor="ParameterSetQuery"; public const string Verb="CreateParameterDraftPreview";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [Key(3)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
 [Key(4)] public string PayloadJson {get;init;}="{}";
 [Key(5)] public int SchemaVersion {get;init;}=ParameterSchemaRegistry.CurrentRegimeSchemaVersion;
 [Key(6)] public int TargetHorizon {get;init;}=(int)TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily;
 [Key(7)] public bool RebuildIntervals {get;init;}
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}
