using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

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
    public string? Actual { get; init; }

    /// <summary>Market/analyst forecast value.</summary>
    [Key(4)]
    public string? Forecast { get; init; }

    /// <summary>Previously reported value (prior).</summary>
    [Key(5)]
    public string? Prior { get; init; }

    /// <summary>Timestamp when this record was created.</summary>
    [Key(6)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or process that created the record.</summary>
    [Key(7)]
    public string CreatedBy { get; init; }

    [Key(8)]
    public string? Impact { get; init; }

    [Key(9)]
    public string? Unit { get; init; }

    [Key(10)]
    public string? Change { get; init; }

    [Key(11)]
    public string? ChangePercentage { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes strings to empty.
    /// </summary>
    public EconomicCalendarReadModel()
    {
        CountryCode = string.Empty;
        EventName = string.Empty;
        CreatedBy = string.Empty;
    }

    /// <summary>
    /// Full constructor for creating a new economic calendar view model.
    /// </summary>
    public EconomicCalendarReadModel(
        DateTime eventDate,
        string countryCode,
        string eventName,
        string? actual,
        string? forecast,
        string? prior,
        DateTime createdOn,
        string createdBy,
        string? impact = null,
        string? unit = null,
        string? change = null,
        string? changePercentage = null)
    {
        EventDate = eventDate;
        CountryCode = countryCode;
        EventName = eventName;
        Actual = actual;
        Forecast = forecast;
        Prior = prior;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        Impact = impact;
        Unit = unit;
        Change = change;
        ChangePercentage = changePercentage;
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
