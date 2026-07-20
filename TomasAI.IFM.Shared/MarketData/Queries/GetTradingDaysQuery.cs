using System;
using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the number of trading days within a specified date range,
/// market and currency context.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetTradingDaysQuery : IQuery<ScalarReadModel<int>>
{
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetTradingDays";
    [IgnoreMember] public const int ErrorId = 1011;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>Start of the date range (inclusive).</summary>
    [Key(2)]
    public DateOnly StartDate { get; init; }

    /// <summary>End of the date range (inclusive).</summary>
    [Key(3)]
    public DateOnly EndDate { get; init; }

    /// <summary>Market type for which trading days are requested.</summary>
    [Key(4)]
    public MarketType MarketType { get; init; }

    /// <summary>Currency type associated with the market context.</summary>
    [Key(5)]
    public CurrencyType CurrencyType { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetTradingDaysQuery() { }

    /// <summary>
    /// Creates a new query instance and initializes <see cref="QueryParams"/> and <see cref="ErrorCode"/>.
    /// </summary>
    /// <param name="startDate">Range start date.</param>
    /// <param name="endDate">Range end date.</param>
    /// <param name="marketType">Market type.</param>
    /// <param name="currencyType">Currency type.</param>
    public GetTradingDaysQuery(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;
        EntityId = new GetTradingDaysParameter(startDate, endDate, marketType, currencyType);
        ErrorCode = ErrorId;
        QueryParams = $"startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}&marketType={MarketType}&currencyType={CurrencyType}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetTradingDaysQuery(
        ActorSubject subject,      // Key(0)
        IActorEntityId entityId,   // Key(1)
        DateOnly startDate,        // Key(2)
        DateOnly endDate,          // Key(3)
        MarketType marketType,     // Key(4)
        CurrencyType currencyType) // Key(5)
    {
        Subject = subject;
        EntityId = new GetTradingDaysParameter(startDate, endDate, marketType, currencyType);
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;
        ErrorCode = ErrorId;
    }
}

/// <summary>
/// MessagePack-serializable query to retrieve the trading dates within a specified date range,
/// market and currency context.
/// </summary>
/// <remarks>
/// Same pattern as <see cref="GetTradingDaysQuery"/>; returns an array of trading dates.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetTradingDatesQuery : IQuery<DateOnly[]>
{
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetTradingDates";
    [IgnoreMember] public const int ErrorId = 1011;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>Start of the date range (inclusive).</summary>
    [Key(2)]
    public DateOnly StartDate { get; init; }

    /// <summary>End of the date range (inclusive).</summary>
    [Key(3)]
    public DateOnly EndDate { get; init; }

    /// <summary>Market type for which trading dates are requested.</summary>
    [Key(4)]
    public MarketType MarketType { get; init; }

    /// <summary>Currency type associated with the market context.</summary>
    [Key(5)]
    public CurrencyType CurrencyType { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetTradingDatesQuery() { }

    /// <summary>
    /// Creates a new query instance and initializes <see cref="QueryParams"/> and <see cref="ErrorCode"/>.
    /// </summary>
    /// <param name="startDate">Range start date.</param>
    /// <param name="endDate">Range end date.</param>
    /// <param name="marketType">Market type.</param>
    /// <param name="currencyType">Currency type.</param>
    public GetTradingDatesQuery(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;
        EntityId = new GetTradingDatesParameter(startDate, endDate, marketType, currencyType);
        ErrorCode = ErrorId;
        QueryParams = $"startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}&marketType={MarketType}&currencyType={CurrencyType}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetTradingDatesQuery(
        ActorSubject subject,      // Key(0)
        IActorEntityId entityId,   // Key(1)
        DateOnly startDate,        // Key(2)
        DateOnly endDate,          // Key(3)
        MarketType marketType,     // Key(4)
        CurrencyType currencyType) // Key(5)
    {
        Subject = subject;
        EntityId = new GetTradingDatesParameter(startDate, endDate, marketType, currencyType);
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;
        ErrorCode = ErrorId;
    }
}
