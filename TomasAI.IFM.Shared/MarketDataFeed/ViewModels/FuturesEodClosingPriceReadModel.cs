using MessagePack;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing an end-of-day (EOD) closing price
/// for a futures symbol on a specific value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodClosingPriceReadModel
{
    /// <summary>Underlying futures symbol.</summary>
    [Key(0)]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>As-of (value) date for the closing price.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Closing price recorded for the value date.</summary>
    [Key(2)]
    public decimal ClosingPrice { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesEodClosingPriceReadModel() { }

    /// <summary>
    /// Full constructor to create a futures EOD closing price snapshot.
    /// </summary>
    /// <param name="symbol">Futures symbol.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="closingPrice">Closing price.</param>
    public FuturesEodClosingPriceReadModel(string symbol, DateOnly valueDate, decimal closingPrice)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        ClosingPrice = closingPrice;
    }

    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(Symbol) && ValueDate > DateOnly.MinValue;
}