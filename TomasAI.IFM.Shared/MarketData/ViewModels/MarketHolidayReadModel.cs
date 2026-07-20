using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a market holiday,
/// including the currency context, holiday date, and a short description.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record MarketHolidayReadModel
{
    /// <summary>Currency context for the market holiday.</summary>
    [Key(0)]
    public CurrencyType CurrencyType { get; init; }

    /// <summary>Date of the market holiday.</summary>
    [Key(1)]
    public DateOnly HolidayDate { get; init; }

    /// <summary>Short description of the holiday.</summary>
    [Key(2)]
    public string Description { get; init; } = string.Empty;

    /// <summary>Parameterless constructor for serializers.</summary>
    public MarketHolidayReadModel() { }

    /// <summary>
    /// Creates a new market holiday view model.
    /// </summary>
    /// <param name="currencyType">Currency context.</param>
    /// <param name="holidayDate">Holiday date.</param>
    /// <param name="description">Holiday description.</param>
    public MarketHolidayReadModel(CurrencyType currencyType, DateOnly holidayDate, string description)
    {
        CurrencyType = currencyType;
        HolidayDate = holidayDate;
        Description = description ?? string.Empty;
    }

    [IgnoreMember]
    public bool IsValid => HolidayDate > DateOnly.MinValue && !string.IsNullOrEmpty(Description);
}