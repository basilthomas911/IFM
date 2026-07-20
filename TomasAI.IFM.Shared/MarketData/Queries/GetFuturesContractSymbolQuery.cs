using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the symbol for a futures contract by its contract identifier.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesContractSymbolQuery : IQuery<string>
{
    [IgnoreMember] public const string Actor = "FuturesContractQuery";
    [IgnoreMember] public const string Verb = "GetFuturesContractSymbol";
    [IgnoreMember] public const int ErrorId = 1009;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Contract identifier to lookup.
    /// </summary>
    [Key(2)]
    public string ContractId { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFuturesContractSymbolQuery() { }

    /// <summary>
    /// Creates a new query for the specified contract id.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    public GetFuturesContractSymbolQuery(string contractId)
    {
        ContractId = contractId ?? string.Empty;
        EntityId = new GetFuturesContractSymbolParameter(ContractId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesContractSymbolQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        string contractId)            // Key(2)
    {
        Subject = subject;
        EntityId = new GetFuturesContractSymbolParameter(contractId);
        ContractId = contractId ?? string.Empty;
        ErrorCode = ErrorId;
    }
}
