using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetParameterStartupRunQuery:IQuery<ParameterStartupRun>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetParameterStartupRunQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="runId">The RunId field.</param>
    [SerializationConstructor]
    public GetParameterStartupRunQuery(ActorSubject subject, IActorEntityId entityId, Guid runId)
    {
        Subject = subject;
        EntityId = entityId;
        RunId = runId;
    }
 public const string Actor="ParameterSetQuery";public const string Verb="GetParameterStartupRun";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public Guid RunId{get;init;}
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}
