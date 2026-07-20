using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the most recent rate of return for a specific symbol on a given date.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetLastRateOfReturnQuery : IQuery<RateOfReturnReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetLastRateOfReturn";
    [IgnoreMember] public const int ErrorId = 1010;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Underlying financial symbol.
    /// </summary>
    [Key(2)]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// As-of (value) date for the requested rate of return.
    /// </summary>
    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetLastRateOfReturnQuery() { }

    /// <summary>
    /// Creates a new query instance and initializes <see cref="QueryParams"/> and <see cref="ErrorCode"/>.
    /// </summary>
    /// <param name="symbol">Underlying symbol.</param>
    /// <param name="valueDate">Value date.</param>
    public GetLastRateOfReturnQuery(string symbol, DateOnly valueDate)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetLastRateOfReturnParameter(symbol, valueDate);
        ErrorCode = ErrorId;
        QueryParams = $"symbol={Symbol}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLastRateOfReturnQuery(
        ActorSubject subject,    // Key(0)
        IActorEntityId entityId, // Key(1)
        string symbol,           // Key(2)
        DateOnly valueDate)      // Key(3)
    {
        Subject = subject;
        EntityId = new GetLastRateOfReturnParameter(symbol, valueDate);
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
