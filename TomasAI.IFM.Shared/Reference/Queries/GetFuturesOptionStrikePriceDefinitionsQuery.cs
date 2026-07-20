using MessagePack;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the strike price definitions for futures options.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesOptionStrikePriceDefinitionsQuery : IQuery<FuturesOptionStrikePriceReadModel>
{
    [IgnoreMember] public const string Actor = "ReferenceQuery";
    [IgnoreMember] public const string Verb = "GetFuturesOptionStrikePriceDefinitions";
    [IgnoreMember] public const int ErrorId = 1026;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFuturesOptionStrikePriceDefinitionsQuery()
    {
        EntityId = new ActorEntityId();
        ErrorCode = ErrorId;
        QueryParams = string.Empty;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Indices must match the combined keys from the interface properties (0..1).
    /// </summary>
    [SerializationConstructor]
    public GetFuturesOptionStrikePriceDefinitionsQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId)      // Key(1)
    {
        Subject = subject;
        EntityId = entityId ?? new ActorEntityId();
        ErrorCode = ErrorId;
        QueryParams = string.Empty;
    }
}
