using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the names of lookup types.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetLookupTypeNamesQuery : IQuery<string[]>
{
    [IgnoreMember] public const string Actor = "LookupTypeQuery";
    [IgnoreMember] public const string Verb = "GetLookupTypeNames";
    [IgnoreMember] public const int ErrorId = 1038;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetLookupTypeNamesQuery()
    {
        ErrorCode = ErrorId;
        EntityId = ActorEntityId.Default;
    }

    /// <summary>
    /// Constructor to create query with entity id.
    /// </summary>
    /// <param name="entityId">The entity identifier for the query.</param>
    public GetLookupTypeNamesQuery(IActorEntityId entityId)
    {
        EntityId = entityId;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Parameter order and types must match the keys (0..1).
    /// </summary>
    [SerializationConstructor]
    public GetLookupTypeNamesQuery(
        ActorSubject subject,    // Key(0)
        IActorEntityId entityId) // Key(1)
    {
        Subject = subject;
        EntityId = entityId;
        ErrorCode = ErrorId;
    }
}
