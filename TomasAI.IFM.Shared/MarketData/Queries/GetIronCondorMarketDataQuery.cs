using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve market data for an Iron Condor options strategy within a specified
/// date range and market context.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetIronCondorMarketDataQuery : IQuery<IronCondorMarketDataReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractQuery";
    [IgnoreMember] public const string Verb = "GetIronCondorMarketData";
    [IgnoreMember] public const int ErrorId = 1235;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>Underlying futures contract identifier.</summary>
    [Key(2)]
    public string UnderlyingContractId { get; init; }

    /// <summary>Short put option contract identifier.</summary>
    [Key(3)]
    public string ShortPutOptionContractId { get; init; }

    /// <summary>Long put option contract identifier.</summary>
    [Key(4)]
    public string LongPutOptionContractId { get; init; }

    /// <summary>Short call option contract identifier.</summary>
    [Key(5)]
    public string ShortCallOptionContractId { get; init; }

    /// <summary>Long call option contract identifier.</summary>
    [Key(6)]
    public string LongCallOptionContractId { get; init; }

    /// <summary>Start date of the requested data range (inclusive).</summary>
    [Key(7)]
    public DateOnly StartDate { get; init; }

    /// <summary>End date of the requested data range (inclusive).</summary>
    [Key(8)]
    public DateOnly EndDate { get; init; }

    /// <summary>Market type (e.g. Equity, Futures).</summary>
    [Key(9)]
    public MarketType MarketType { get; init; }

    /// <summary>Currency type for market data.</summary>
    [Key(10)]
    public CurrencyType CurrencyType { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetIronCondorMarketDataQuery() { }

    /// <summary>
    /// Creates a new query instance.
    /// </summary>
    public GetIronCondorMarketDataQuery(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType)
    {
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        ShortPutOptionContractId = shortPutOptionContractId ?? string.Empty;
        LongPutOptionContractId = longPutOptionContractId ?? string.Empty;
        ShortCallOptionContractId = shortCallOptionContractId ?? string.Empty;
        LongCallOptionContractId = longCallOptionContractId ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;

        EntityId = new GetIronCondorMarketDataParameter(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            startDate,
            endDate,
            marketType,
            currencyType);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetIronCondorMarketDataQuery(
        ActorSubject subject,               // Key(0)
        IActorEntityId entityId,            // Key(1)
        string underlyingContractId,        // Key(2)
        string shortPutOptionContractId,    // Key(3)
        string longPutOptionContractId,     // Key(4)
        string shortCallOptionContractId,   // Key(5)
        string longCallOptionContractId,    // Key(6)
        DateOnly startDate,                 // Key(7)
        DateOnly endDate,                   // Key(8)
        MarketType marketType,              // Key(9)
        CurrencyType currencyType)          // Key(10)
    {
        Subject = subject;
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        ShortPutOptionContractId = shortPutOptionContractId ?? string.Empty;
        LongPutOptionContractId = longPutOptionContractId ?? string.Empty;
        ShortCallOptionContractId = shortCallOptionContractId ?? string.Empty;
        LongCallOptionContractId = longCallOptionContractId ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;

        EntityId = new GetIronCondorMarketDataParameter(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            startDate,
            endDate,
            marketType,
            currencyType);
        ErrorCode = ErrorId;
    }
}
