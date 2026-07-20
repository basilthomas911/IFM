using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve a futures trade signal for a specific symbol and value date.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesTradeSignalBySymbolQuery : IQuery<FuturesTradeSignalV2ReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataAnalyticsQuery";
    [IgnoreMember] public const string Verb = "GetFuturesTradeSignalBySymbol";
    [IgnoreMember] public const int ErrorId = 1009;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    /// <summary>
    /// Symbol representing the futures contract (e.g. "ES").
    /// </summary>
    [Key(2)]
    public string Symbol { get; init; }

    /// <summary>
    /// Value (as-of) date for which the trade signal is requested.
    /// </summary>
    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFuturesTradeSignalBySymbolQuery() { }

    /// <summary>
    /// Creates a new query for the specified symbol and value date.
    /// </summary>
    /// <param name="symbol">The futures symbol (e.g. "ES").</param>
    /// <param name="valueDate">The value date.</param>
    public GetFuturesTradeSignalBySymbolQuery(string symbol, DateOnly valueDate)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetFuturesTradeSignalBySymbolParameter(Symbol, ValueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Indices must match the keys defined in this type (0..3).
    /// </summary>
    [SerializationConstructor]
    public GetFuturesTradeSignalBySymbolQuery(
        ActorSubject subject,    // Key(0)
        IActorEntityId entityId, // Key(1)
        string symbol,           // Key(2)
        DateOnly valueDate)      // Key(3)
    {
        Subject = subject;
        EntityId = new GetFuturesTradeSignalBySymbolParameter(symbol, valueDate);
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
