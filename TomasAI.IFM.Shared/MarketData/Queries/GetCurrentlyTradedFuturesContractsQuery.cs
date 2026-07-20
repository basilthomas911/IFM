using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the futures contracts that are currently traded.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetCurrentlyTradedFuturesContractsQuery : IQuery<FuturesContractV2ReadModel[]>
{
    [IgnoreMember] public const string Actor = "FuturesContractQuery";
    [IgnoreMember] public const string Verb = "GetCurrentlyTradedFuturesContracts";
    [IgnoreMember] public const int ErrorId = 1235;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }
    /// <summary>
    /// Symbol of the futures contract to query (e.g., "ES", "NQ").
    /// </summary>
    [Key(2)] public string Symbol { get; init; }

    public GetCurrentlyTradedFuturesContractsQuery()
    { 
    }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetCurrentlyTradedFuturesContractsQuery(string symbol)
    {
        Symbol = symbol;
        EntityId = new GetCurrentlyTradedFuturesContractsParameter(symbol);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetCurrentlyTradedFuturesContractsQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,
        string symbol)      // Key(1)
    {
        Subject = subject;
        EntityId = new GetCurrentlyTradedFuturesContractsParameter(symbol);
        Symbol = symbol;
        ErrorCode = ErrorId;
    }
}
