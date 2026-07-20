using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve futures option contracts for a specified symbol.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesOptionContractsQuery : IQuery<FuturesOptionContractReadModel[]>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractQuery";
    [IgnoreMember] public const string Verb = "GetFuturesOptionContracts";
    [IgnoreMember] public const int ErrorId = 1003;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Underlying symbol to filter option contracts (e.g. "ES").
    /// </summary>
    [Key(2)]
    public string Symbol { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFuturesOptionContractsQuery() { }

    /// <summary>
    /// Creates a new query instance for the specified symbol.
    /// </summary>
    /// <param name="symbol">Underlying futures symbol.</param>
    public GetFuturesOptionContractsQuery(string symbol)
    {
        Symbol = symbol ?? string.Empty;
        EntityId = new GetFuturesOptionContractsParameter(symbol);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesOptionContractsQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        string symbol)                // Key(2)
    {
        Subject = subject;
        EntityId = new GetFuturesOptionContractsParameter(symbol);
        Symbol = symbol ?? string.Empty;
        ErrorCode = ErrorId;
    }
}
