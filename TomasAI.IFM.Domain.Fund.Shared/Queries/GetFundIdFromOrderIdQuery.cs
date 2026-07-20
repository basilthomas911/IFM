using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

namespace TomasAI.IFM.Domain.Fund.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the fund ID associated with a specific order ID.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFundIdFromOrderIdQuery : IQuery<ScalarReadModel<int>>
{
    [IgnoreMember] public const string Actor = "FundQuery";
    [IgnoreMember] public const string Verb = "GetFundIdFromOrderId";
    [IgnoreMember] public const int ErrorId = 1010;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Order identifier used to look up the associated fund.
    /// </summary>
    [Key(2)]
    public int OrderId { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFundIdFromOrderIdQuery() { }

    public GetFundIdFromOrderIdQuery(int orderId)
    {
        OrderId = orderId;
        EntityId = new GetFundIdFromOrderIdParameter(orderId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFundIdFromOrderIdQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        int orderId)                  // Key(2)
    {
        Subject = subject;
        EntityId = new GetFundIdFromOrderIdParameter(orderId);
        ErrorCode = ErrorId;
        OrderId = orderId;
    }
}