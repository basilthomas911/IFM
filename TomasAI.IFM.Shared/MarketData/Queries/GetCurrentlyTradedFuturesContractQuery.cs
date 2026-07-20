using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the currently traded futures contract for a specific symbol.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetCurrentlyTradedFuturesContractQuery : IQuery<FuturesContractV2ReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesContractQuery";
    [IgnoreMember] public const string Verb = "GetCurrentlyTradedFuturesContract";
    [IgnoreMember] public const int ErrorId = 1234;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Symbol of the futures contract to query (e.g., "ES", "NQ").
    /// </summary>
    [Key(2)]
    public string Symbol { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetCurrentlyTradedFuturesContractQuery() { }

    public GetCurrentlyTradedFuturesContractQuery(string symbol)
    {
        Symbol = symbol;
        EntityId = new GetCurrentlyTradedFuturesContractParameter(symbol);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetCurrentlyTradedFuturesContractQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        string symbol)                // Key(2)
    {
        Subject = subject;
        EntityId = new GetCurrentlyTradedFuturesContractParameter(symbol);
        Symbol = symbol;
        ErrorCode = ErrorId;
    }
}
