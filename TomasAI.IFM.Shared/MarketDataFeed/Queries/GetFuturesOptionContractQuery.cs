using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve a futures option contract by its contract identifier.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesOptionContractQuery : IQuery<FuturesOptionContractReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetFuturesOptionContract";
    [IgnoreMember] public const int ErrorId = 1019;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    /// <summary>
    /// Contract identifier to query.
    /// </summary>
    [Key(2)]
    public string ContractId { get; set; }

    /// <summary>
    /// Optional contract view model to query for matching contract.
    /// </summary>
    [Key(3)]
    public FuturesOptionContractReadModel? QueryForContract { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFuturesOptionContractQuery() { }

    public GetFuturesOptionContractQuery(string contractId, FuturesOptionContractReadModel? queryForContract = null)
    {
        ContractId = contractId ?? string.Empty;
        QueryForContract = queryForContract;
        EntityId = new GetFuturesOptionContractParameter(contractId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Indices must match the keys defined in this type (0..3).
    /// </summary>
    [SerializationConstructor]
    public GetFuturesOptionContractQuery(
        ActorSubject subject,                           // Key(0)
        IActorEntityId entityId,                        // Key(1)
        string contractId,                              // Key(2)
        FuturesOptionContractReadModel? queryForContract) // Key(3)
    {
        Subject = subject;
        EntityId = new GetFuturesOptionContractParameter(contractId);
        ContractId = contractId ?? string.Empty;
        QueryForContract = queryForContract;
        ErrorCode = ErrorId;
    }
}
