using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a rate of return for a symbol on a specific value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record RateOfReturnReadModel
{
    /// <summary>The underlying symbol.</summary>
    [Key(0)]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The as-of (value) date for the rate of return.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>The computed rate of return.</summary>
    [Key(2)]
    public double RateOfReturn { get; init; }

    /// <summary>Parameterless constructor for MessagePack and other serializers.</summary>
    public RateOfReturnReadModel() { }

    /// <summary>
    /// Full constructor to create a rate of return snapshot.
    /// </summary>
    /// <param name="symbol">Underlying symbol.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="rateOfReturn">Computed rate of return.</param>
    public RateOfReturnReadModel(string symbol, DateOnly valueDate, double rateOfReturn)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        RateOfReturn = rateOfReturn;
    }

    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(Symbol) && ValueDate > DateOnly.MinValue;
}