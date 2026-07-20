using MessagePack;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve economic calendar data for a specific date, calendar view type, and country.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetEconomicCalendarQuery : IQuery<EconomicCalendarReadModel[]>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarQuery";
    [IgnoreMember] public const string Verb = "GetEconomicCalendar";
    [IgnoreMember] public const int ErrorId = 1034;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    [Key(2)]
    public DateTime TodaysDate { get; init; }

    [Key(3)]
    public EconomicCalendarViewType CalendarViewType { get; init; }

    [Key(4)]
    public string CountryCode { get; init; } = string.Empty;

    public GetEconomicCalendarQuery() { }

    public GetEconomicCalendarQuery(DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
    {
        TodaysDate = todaysDate;
        CalendarViewType = calendarViewType;
        CountryCode = countryCode ?? string.Empty;
        EntityId = new GetEconomicCalendarParameter(todaysDate,calendarViewType, countryCode);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetEconomicCalendarQuery(ActorSubject subject, IActorEntityId entityId, DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
    {
        Subject = subject;
        EntityId = new GetEconomicCalendarParameter(todaysDate, calendarViewType, countryCode);
        TodaysDate = todaysDate;
        CalendarViewType = calendarViewType;
        CountryCode = countryCode ?? string.Empty;
        ErrorCode = ErrorId;
    }
}
