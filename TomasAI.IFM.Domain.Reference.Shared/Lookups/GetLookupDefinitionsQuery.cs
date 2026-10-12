using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Lookups;

[MessagePackObject(AllowPrivate = true)]
public sealed class GetLookupDefinitionsQuery : IQuery<LookupDefinitionReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetLookupDefinitionsQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="groupName">The GroupName field.</param>
    [SerializationConstructor]
    public GetLookupDefinitionsQuery(ActorSubject subject, IActorEntityId entityId, string groupName)
    {
        Subject = subject;
        EntityId = entityId;
        GroupName = groupName;
    }
    [IgnoreMember] public const string Actor = "ReferenceQuery";
    [IgnoreMember] public const string Verb = "GetLookupDefinitions";
    [IgnoreMember] public const int ErrorId = 1064;
    [IgnoreMember] public const string Uri = "/api/reference/lookup-definitions/query";
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public string GroupName { get; set; } = "";
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => GroupName;
}
