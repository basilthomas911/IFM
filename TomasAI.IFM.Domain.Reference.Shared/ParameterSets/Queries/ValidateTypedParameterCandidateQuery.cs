using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Concrete binary parameter request; contains no encoded parameter document.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ValidateTypedParameterCandidateQuery : IQuery<ParameterValidationReport>
{
    public ValidateTypedParameterCandidateQuery() { }
    [SerializationConstructor]
    public ValidateTypedParameterCandidateQuery(ActorSubject subject, IActorEntityId entityId, ParameterSetValue? parameters, string componentCode, int schemaVersion)
    {
        Subject=subject; EntityId=entityId;
        Parameters=parameters;
        ComponentCode=componentCode;
        SchemaVersion=schemaVersion;
    }
    public const string Actor="ParameterSetQuery";
    public const string Verb="ValidateTypedParameterCandidate";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public ParameterSetValue? Parameters {get;init;}=null;
    [Key(3)] public string ComponentCode {get;init;}=ParameterSchemaRegistry.RegimeComponent;
    [Key(4)] public int SchemaVersion {get;init;}=ParameterSchemaRegistry.CurrentRegimeSchemaVersion;
    [IgnoreMember] public int ErrorCode {get;init;}=33101;
    [IgnoreMember] public string? QueryParams {get;init;}
}
