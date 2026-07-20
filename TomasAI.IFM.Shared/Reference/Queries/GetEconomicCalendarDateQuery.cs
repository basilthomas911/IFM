using MessagePack;
using TomasAI.IFM.Shared.Reference.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve economic calendar data for a specific date and view type.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetEconomicCalendarDateQuery : IQuery<string>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarQuery";
    [IgnoreMember] public const string Verb = "GetEconomicCalendarDate";
    [IgnoreMember] public const int ErrorId = 1035;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    /// <summary>
    /// The date for which the economic calendar data is requested.
    /// </summary>
    [Key(2)]
    public DateTime TodaysDate { get; set; }

    /// <summary>
    /// The view type for the economic calendar (Today, Yesterday, Tomorrow, ThisWeek, NextWeek).
    /// </summary>
    [Key(3)]
    public EconomicCalendarViewType CalendarViewType { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetEconomicCalendarDateQuery() { }

    public GetEconomicCalendarDateQuery(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
    {
        TodaysDate = todaysDate;
        CalendarViewType = calendarViewType;
        EntityId = new GetEconomicCalendarDateParameter(todaysDate, calendarViewType);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Indices must match the keys from this query type.
    /// </summary>
    [SerializationConstructor]
    public GetEconomicCalendarDateQuery(
        ActorSubject subject,                       // Key(0)
        IActorEntityId entityId,                    // Key(1)
        DateTime todaysDate,                        // Key(2)
        EconomicCalendarViewType calendarViewType)  // Key(3)
    {
        Subject = subject;
        EntityId = new GetEconomicCalendarDateParameter(todaysDate, calendarViewType);
        TodaysDate = todaysDate;
        CalendarViewType = calendarViewType;
        ErrorCode = ErrorId;
    }
}

