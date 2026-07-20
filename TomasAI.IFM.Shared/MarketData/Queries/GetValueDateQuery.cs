using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query that returns a single date value (wrapped in <see cref="ScalarReadModel{DateOnly}"/>).
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetValueDateQuery : IQuery<ScalarReadModel<DateOnly>>
{
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetValueDate";
    [IgnoreMember] public const int ErrorId = 1015;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetValueDateQuery()
    {
        EntityId = new GetValueDateParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetValueDateQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId)      // Key(1)
    {
        Subject = subject;
        EntityId = entityId;
        ErrorCode = ErrorId;
    }
}
