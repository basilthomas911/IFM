using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve economic calendar data for a specific date and view type.
/// </summary>
[MessagePackObject(false)]
public record GetEconomicCalendarDateParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateTime TodaysDate { get; init; }
    [Key(1)] public EconomicCalendarViewType CalendarViewType { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetEconomicCalendarDateParameter() { }

    [SerializationConstructor]
    public GetEconomicCalendarDateParameter(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
    {
        TodaysDate = todaysDate;
        CalendarViewType = calendarViewType;
        QueryParams = $"todaysDate={TodaysDate:yyyy-MM-dd}&calendarViewType={CalendarViewType}";
    }

    public string Format()
        => $"{TodaysDate:yyyy-MM-dd}.{CalendarViewType}";
}
