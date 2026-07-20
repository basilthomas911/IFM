using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Reference.QueryParameters;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the next seed ID for a specified seed type.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// This query is used to fetch the next available seed ID based on the provided seed type.
/// The result is returned as a <see cref="ScalarReadModel{int}"/> containing the next seed value.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetNextSeedIdQuery : IQuery<ScalarReadModel<int>>
{
    [IgnoreMember] public const string Actor = "ReferenceQuery";
    [IgnoreMember] public const string Verb = "GetNextSeedId";
    [IgnoreMember] public const int ErrorId = 1023;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    /// <summary>
    /// The type of seed for which to retrieve the next identifier.
    /// </summary>
    [Key(2)]
    public string SeedType { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetNextSeedIdQuery() { }

    /// <summary>
    /// Create a query for the specified seed type.
    /// </summary>
    public GetNextSeedIdQuery(string seedType)
    {
        SeedType = seedType ?? string.Empty;
        EntityId = new GetNextSeedIdParameter(seedType);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Indices must match the combined keys from the interface properties (0..1) and this derived type (2).
    /// </summary>
    [SerializationConstructor]
    public GetNextSeedIdQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        string seedType)              // Key(2)
    {
        Subject = subject;
        EntityId = new GetNextSeedIdParameter(seedType);
        SeedType = seedType ?? string.Empty;
        ErrorCode = ErrorId;
    }
}
