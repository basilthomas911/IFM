using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve economic calendar data for a specific date, calendar view type, and country.
/// </summary>
[MessagePackObject(false)]
public record GetEconomicCalendarParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateTime TodaysDate { get; init; }
    [Key(1)] public EconomicCalendarViewType CalendarViewType { get; init; }
    [Key(2)] public string CountryCode { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetEconomicCalendarParameter() { }

    [SerializationConstructor]
    public GetEconomicCalendarParameter(DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
    {
        TodaysDate = todaysDate;
        CalendarViewType = calendarViewType;
        CountryCode = countryCode ?? string.Empty;
        QueryParams = $"todaysDate={TodaysDate:yyyy-MM-dd}&calendarViewType={CalendarViewType}&countryCode={CountryCode}";
    }

    public string Format()
        => $"{TodaysDate:yyyy-MM-dd:hh-mm}.{CalendarViewType}.{CountryCode}";
}
