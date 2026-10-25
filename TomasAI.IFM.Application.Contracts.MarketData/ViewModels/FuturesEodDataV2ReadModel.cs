using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Represents end-of-day (EOD) futures market data enriched with derived statistical and classification metrics,
/// used by analytics and signal generation components.
/// </summary>
/// <remarks>
/// This view model is MessagePack serializable for efficient transport. Only primitive / enum fields are serialized.
/// Derived identifiers and helper methods are excluded via <see cref="IgnoreMemberAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodDataV2ReadModel
{
    /// <summary>Full contract identifier (root + month/year code).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1721", Justification = "The read-model property is serialized data; the method implements an existing conversion/provider contract.")]
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date of the market data (trading / settlement date).</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Symbol root (e.g. ES, NQ, CL, VX).</summary>
    [Key(2)]
    public string Symbol { get; init; }

    /// <summary>Open price.</summary>
    [Key(3)]
    public decimal OpenPrice { get; init; }

    /// <summary>Session high price.</summary>
    [Key(4)]
    public decimal HighPrice { get; init; }

    /// <summary>Session low price.</summary>
    [Key(5)]
    public decimal LowPrice { get; init; }

    /// <summary>Latest accepted trade price while the session is live; final close after the session.</summary>
    [Key(6)]
    public decimal ClosePrice { get; init; }

    /// <summary>Session volume.</summary>
    [Key(7)]
    public long Volume { get; init; }

    /// <summary>
    /// Decimal ratio of the latest accepted close relative to the trading-session open:
    /// <c>(ClosePrice - OpenPrice) / OpenPrice</c>.
    /// </summary>
    [Key(8)]
    public double DailyPercentChange { get; init; }

    /// <summary>Rolling standard deviation (percent form).</summary>
    [Key(9)]
    public double DailyStdDev { get; init; }

    /// <summary>Rolling standard deviation expressed in price units.</summary>
    [Key(10)]
    public double DailyStdDevAmount { get; init; }

    /// <summary>Upper Bollinger / volatility band (if applicable).</summary>
    [Key(11)]
    public double UpperBand { get; init; }

    /// <summary>Rolling mean / middle band.</summary>
    [Key(12)]
    public double Mean { get; init; }

    /// <summary>Lower Bollinger / volatility band (if applicable).</summary>
    [Key(13)]
    public double LowerBand { get; init; }

    /// <summary>Classified overall market direction.</summary>
    [Key(14)]
    public MarketDirectionType MarketDirection { get; init; }

    /// <summary>Classified overall market volatility regime.</summary>
    [Key(15)]
    public MarketVolatilityType MarketVolatility { get; init; }

    /// <summary>Price direction classification.</summary>
    [Key(16)]
    public PriceDirectionType PriceDirection { get; init; }

    /// <summary>Price volatility classification.</summary>
    [Key(17)]
    public PriceVolatilityType PriceVolatility { get; init; }

    /// <summary>Directional indicator (MDI / custom composite).</summary>
    [Key(18)]
    public double MarketDirectionIndicator { get; init; }

    /// <summary>Window size used to compute rolling stats.</summary>
    [Key(19)]
    public int WindowSize { get; init; }

    /// <summary>50-day moving average (optional if pre-computed upstream).</summary>
    [Key(20)]
    public decimal FiftyDMA { get; set; }

    /// <summary>200-day moving average (optional if pre-computed upstream).</summary>
    [Key(21)]
    public decimal TwoHundredDMA { get; set; }

    /// <summary>Composite futures data identifier (not serialized).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesDataId DataId => new(ContractId ?? string.Empty, ValueDate);

    /// <summary>
    /// Parses and returns the strongly typed futures contract identifier (not serialized).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024", Justification = "Preserve the existing conversion method API used by callers.")]
    public FuturesContractId GetContractId() => new FuturesContractIdParser(ContractId ?? string.Empty).Id;

    /// <summary>
    /// Parameterless constructor required by MessagePack and some serializers. Initializes numeric fields to zero and enums to defaults.
    /// </summary>
    public FuturesEodDataV2ReadModel() { }

    /// <summary>
    /// Full constructor enabling initialization of all serialized properties.
    /// </summary>
    public FuturesEodDataV2ReadModel(
        string contractId,
        DateOnly valueDate,
        string symbol,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        long volume,
        double dailyPercentChange = 0,
        double dailyStdDev = 0,
        double dailyStdDevAmount = 0,
        double upperBand = 0,
        double mean = 0,
        double lowerBand = 0,
        MarketDirectionType marketDirection = MarketDirectionType.NeutralUp,
        MarketVolatilityType marketVolatility = MarketVolatilityType.Normal,
        PriceDirectionType priceDirection = PriceDirectionType.Flat,
        PriceVolatilityType priceVolatility = PriceVolatilityType.Unknown,
        double marketDirectionIndicator = 0,
        int windowSize = 0,
        decimal fiftyDMA = 0,
        decimal twoHundredDMA = 0)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        Symbol = symbol;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        DailyPercentChange = dailyPercentChange;
        DailyStdDev = dailyStdDev;
        DailyStdDevAmount = dailyStdDevAmount;
        UpperBand = upperBand;
        Mean = mean;
        LowerBand = lowerBand;
        MarketDirection = marketDirection;
        MarketVolatility = marketVolatility;
        PriceDirection = priceDirection;
        PriceVolatility = priceVolatility;
        MarketDirectionIndicator = marketDirectionIndicator;
        WindowSize = windowSize;
        FiftyDMA = fiftyDMA;
        TwoHundredDMA = twoHundredDMA;
    }

    /// <summary>
    /// Returns a JSON string representation of the view model (for diagnostics).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(ContractId) && ValueDate > DateOnly.MinValue;
}
