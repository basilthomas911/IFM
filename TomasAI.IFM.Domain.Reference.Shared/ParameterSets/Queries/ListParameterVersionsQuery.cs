using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject(AllowPrivate = true)]
public sealed record ListParameterVersionsQuery:IQuery<ParameterSetVersion[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public ListParameterVersionsQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="setId">The SetId field.</param>
    /// <param name="componentCode">The ComponentCode field.</param>
    /// <param name="payloadJson">The PayloadJson field.</param>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="limit">The Limit field.</param>
    /// <param name="afterName">The AfterName field.</param>
    /// <param name="afterSetId">The AfterSetId field.</param>
    /// <param name="afterVersion">The AfterVersion field.</param>
    [SerializationConstructor]
    public ListParameterVersionsQuery(ActorSubject subject, IActorEntityId entityId, Guid setId, string componentCode, string payloadJson, int schemaVersion, int limit, string afterName, Guid? afterSetId, int afterVersion)
    {
        Subject = subject;
        EntityId = entityId;
        SetId = setId;
        ComponentCode = componentCode;
        PayloadJson = payloadJson;
        SchemaVersion = schemaVersion;
        Limit = limit;
        AfterName = afterName;
        AfterSetId = afterSetId;
        AfterVersion = afterVersion;
    }
 public const string Actor="ParameterSetQuery"; public const string Verb="ListParameterVersions";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [Key(3)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
 [Key(4)] public string PayloadJson {get;init;}="{}";
 [Key(5)] public int SchemaVersion {get;init;}=2;
 [Key(6)] public int Limit{get;init;}=100;
 [Key(7)] public string AfterName{get;init;}=string.Empty;
 [Key(8)] public Guid? AfterSetId{get;init;}
 [Key(9)] public int AfterVersion{get;init;}
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}
