using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a country code used by the economic calendar.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarCountryCodeReadModel
{
    /// <summary>ISO country code.</summary>
    [Key(0)]
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>Parameterless constructor for serializers.</summary>
    public EconomicCalendarCountryCodeReadModel() { }

    /// <summary>
    /// Creates a new economic calendar country code model.
    /// </summary>
    /// <param name="countryCode">ISO country code.</param>
    public EconomicCalendarCountryCodeReadModel(string countryCode)
    {
        CountryCode = countryCode ?? string.Empty;
    }

    /// <summary>Returns a compact JSON representation of the model.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}