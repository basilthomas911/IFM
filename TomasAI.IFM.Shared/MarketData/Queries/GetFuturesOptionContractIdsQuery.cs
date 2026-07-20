using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve a set of futures option contract identifiers.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesOptionContractIdsQuery : IQuery<string[]>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractQuery";
    [IgnoreMember] public const string Verb = "GetFuturesOptionContractIds";
    [IgnoreMember] public const int ErrorId = 1099;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    /// <summary>
    /// Futures option contract identifiers requested by the query.
    /// </summary>
    [Key(2)]
    public string[] ContractIds { get; init; } = [];

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFuturesOptionContractIdsQuery() { }

    /// <summary>
    /// Creates a new query instance for the supplied contract identifiers.
    /// </summary>
    /// <param name="contractIds">Array of contract ids to check.</param>
    public GetFuturesOptionContractIdsQuery(string[] contractIds)
    {
        ContractIds = contractIds ?? [];
        EntityId = new GetFuturesOptionContractIdsParameter(ContractIds);
        ErrorCode = ErrorId;
        QueryParams = $"contractIds={string.Join(",", ContractIds)}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesOptionContractIdsQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        string[] contractIds)       // Key(2)
    {
        Subject = subject;
        EntityId = new GetFuturesOptionContractIdsParameter(contractIds ?? []);
        ContractIds = contractIds ?? [];
        ErrorCode = ErrorId;
        QueryParams = $"contractIds={string.Join(",", ContractIds)}";
    }
}
