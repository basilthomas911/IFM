using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject(AllowPrivate = true)]
public sealed record ValidateParameterCandidateQuery:IQuery<ParameterValidationReport>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public ValidateParameterCandidateQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="setId">The SetId field.</param>
    /// <param name="componentCode">The ComponentCode field.</param>
    /// <param name="payloadJson">The PayloadJson field.</param>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    [SerializationConstructor]
    public ValidateParameterCandidateQuery(ActorSubject subject, IActorEntityId entityId, Guid setId, string componentCode, string payloadJson, int schemaVersion)
    {
        Subject = subject;
        EntityId = entityId;
        SetId = setId;
        ComponentCode = componentCode;
        PayloadJson = payloadJson;
        SchemaVersion = schemaVersion;
    }
 public const string Actor="ParameterSetQuery"; public const string Verb="ValidateParameterCandidate";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [Key(3)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
 [Key(4)] public string PayloadJson {get;init;}="{}";
 [Key(5)] public int SchemaVersion {get;init;}=2;
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}
