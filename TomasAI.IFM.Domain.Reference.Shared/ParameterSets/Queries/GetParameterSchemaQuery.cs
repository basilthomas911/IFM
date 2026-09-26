using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetParameterSchemaQuery:IQuery<ParameterSchemaDefinition>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetParameterSchemaQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="componentCode">The ComponentCode field.</param>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    [SerializationConstructor]
    public GetParameterSchemaQuery(ActorSubject subject, IActorEntityId entityId, string componentCode, int schemaVersion)
    {
        Subject = subject;
        EntityId = entityId;
        ComponentCode = componentCode;
        SchemaVersion = schemaVersion;
    }
 public const string Actor="ParameterSetQuery";public const string Verb="GetParameterSchema";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public string ComponentCode{get;init;}="strategy-workflow.regime-discovery";
 [Key(3)]public int SchemaVersion{get;init;}=ParameterSchemaRegistry.CurrentRegimeSchemaVersion;
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}
