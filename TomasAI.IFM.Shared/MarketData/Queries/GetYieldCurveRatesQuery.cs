using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve yield curve rates for a specified date range.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetYieldCurveRatesQuery : IQuery<YieldCurveRateReadModel[]>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateQuery";
    [IgnoreMember] public const string Verb = "GetYieldCurveRates";
    [IgnoreMember] public const int ErrorId = 1013;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Start date of the date range for which yield curve rates are requested.
    /// </summary>
    [Key(2)]
    public DateOnly StartDate { get; init; }

    /// <summary>
    /// End date of the date range for which yield curve rates are requested.
    /// </summary>
    [Key(3)]
    public DateOnly EndDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetYieldCurveRatesQuery() { }

    public GetYieldCurveRatesQuery(DateOnly startDate, DateOnly endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
        EntityId = new GetYieldCurveRatesParameter(startDate, endDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetYieldCurveRatesQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        DateOnly startDate,           // Key(2)
        DateOnly endDate)             // Key(3)
    {
        Subject = subject;
        EntityId = new GetYieldCurveRatesParameter(startDate, endDate);
        StartDate = startDate;
        EndDate = endDate;
        ErrorCode = ErrorId;
    }
}

