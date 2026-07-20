using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing an economic calendar entry (event definition).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members
/// excluded via <see cref="IgnoreMemberAttribute"/> and <see cref="JsonIgnoreAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarReadModel
{
    /// <summary>UTC (or localized) timestamp of the economic event.</summary>
    [Key(0)]
    public DateTime EventDate { get; init; }

    /// <summary>ISO (2–3 letter) country code related to the event.</summary>
    [Key(1)]
    public string CountryCode { get; init; }

    /// <summary>Descriptive name/title of the economic event.</summary>
    [Key(2)]
    public string EventName { get; init; }

    /// <summary>Published actual value.</summary>
    [Key(3)]
    public string Actual { get; init; }

    /// <summary>Market/analyst forecast value.</summary>
    [Key(4)]
    public string Forecast { get; init; }

    /// <summary>Previously reported value (prior).</summary>
    [Key(5)]
    public string Prior { get; init; }

    /// <summary>Timestamp when this record was created.</summary>
    [Key(6)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or process that created the record.</summary>
    [Key(7)]
    public string CreatedBy { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes strings to empty.
    /// </summary>
    public EconomicCalendarReadModel()
    {
        CountryCode = string.Empty;
        EventName = string.Empty;
        Actual = string.Empty;
        Forecast = string.Empty;
        Prior = string.Empty;
        CreatedBy = string.Empty;
    }

    /// <summary>
    /// Full constructor for creating a new economic calendar view model.
    /// </summary>
    public EconomicCalendarReadModel(
        DateTime eventDate,
        string countryCode,
        string eventName,
        string actual,
        string forecast,
        string prior,
        DateTime createdOn,
        string createdBy)
    {
        EventDate = eventDate;
        CountryCode = countryCode;
        EventName = eventName;
        Actual = actual;
        Forecast = forecast;
        Prior = prior;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

    /// <summary>Derived identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public EconomicCalendarId Id => new(EventDate, CountryCode, EventName);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => EventDate > DateTime.MinValue && !string.IsNullOrEmpty(CountryCode);

    /// <summary>Returns a compact JSON representation of the entry.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}

/// <summary>
/// Raw JSON import model (external feed) for an economic calendar item.
/// </summary>
public record EconomicCalendarJsonModel(
    string Event,
    DateTime Date,
    string Country,
    string Actual,
    string Previous,
    string Change,
    string ChangePercentage,
    string Estimate,
    string Impact)
{
    /// <summary>
    /// Maps the external JSON model to the internal <see cref="EconomicCalendarReadModel"/>.
    /// </summary>
    public EconomicCalendarReadModel ToViewModel()
        => new(
            eventDate: new DateTime(Date.Year, Date.Month, Date.Day, Date.Hour, Date.Minute, Date.Second, DateTimeKind.Utc).ToLocalTime(),
            countryCode: GetCountryCode().Length >= 2 ? GetCountryCode()[..2] : GetCountryCode(),
            eventName: GetEventName(),
            actual: GetActual(),
            forecast: GetEstimate(),
            prior: GetPrevious(),
            createdOn: DateTime.UtcNow,
            createdBy: string.Empty);

    string GetCountryCode() => string.IsNullOrWhiteSpace(Country) ? string.Empty : Country.Trim();
    string GetEventName() => string.IsNullOrWhiteSpace(Event) ? string.Empty : Event.Trim();
    string GetActual() => string.IsNullOrWhiteSpace(Actual) ? "0" : Actual.Trim();
    string GetEstimate() => string.IsNullOrWhiteSpace(Estimate) ? "0" : Estimate.Trim();
    string GetPrevious() => string.IsNullOrWhiteSpace(Previous) ? "0" : Previous.Trim();
}