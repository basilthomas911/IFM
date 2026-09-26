using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetParameterSetStateQuery:IQuery<ParameterSetSnapshot>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetParameterSetStateQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="setId">The SetId field.</param>
    [SerializationConstructor]
    public GetParameterSetStateQuery(ActorSubject subject, IActorEntityId entityId, Guid setId)
    {
        Subject = subject;
        EntityId = entityId;
        SetId = setId;
    }
 public const string Actor="ParameterSetQuery"; public const string Verb="GetParameterSetState";
 [Key(0)] public ActorSubject Subject {get;init;}
 [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
 [Key(2)] public Guid SetId {get;init;}
 [IgnoreMember] public int ErrorCode {get;init;}=33101;
 [IgnoreMember] public string? QueryParams {get;init;}
}
