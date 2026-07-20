using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve details of a futures option contract by its contract identifier.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesOptionContractQuery : IQuery<FuturesOptionContractReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractQuery";
    [IgnoreMember] public const string Verb = "GetFuturesOptionContract";
    [IgnoreMember] public const int ErrorId = 1002;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    /// <summary>
    /// Contract identifier to look up.
    /// </summary>
    [Key(2)]
    public string ContractId { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor required by serializers.
    /// </summary>
    public GetFuturesOptionContractQuery() { }

    /// <summary>
    /// Primary constructor used when creating the query in code.
    /// </summary>
    /// <param name="contractId">Futures option contract identifier.</param>
    public GetFuturesOptionContractQuery(string contractId)
    {
        ContractId = contractId ?? string.Empty;
        EntityId = new GetFuturesOptionContractParameter(ContractId);
        ErrorCode = ErrorId;
        QueryParams = $"contractId={ContractId}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesOptionContractQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId)        // Key(2)
    {
        Subject = subject;
        EntityId = new GetFuturesOptionContractParameter(contractId ?? string.Empty);
        ContractId = contractId ?? string.Empty;
        ErrorCode = ErrorId;
        QueryParams = $"contractId={ContractId}";
    }
}
