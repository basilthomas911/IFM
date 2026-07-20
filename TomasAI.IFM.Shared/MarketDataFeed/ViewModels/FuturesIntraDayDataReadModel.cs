using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents a comprehensive view model for intra-day futures market data, encapsulating contract identifiers, price
/// metrics, volume, and statistical indicators relevant to trading activities.
/// </summary>
/// <remarks>This view model is designed to support analysis and visualization of futures market behavior by
/// providing both raw and derived metrics, such as price bands, moving averages, and classified market states. It is
/// intended for use in applications that require detailed, structured access to intra-day futures data, including
/// analytics dashboards and trading systems. The type is compatible with serialization frameworks such as MessagePack
/// and JSON, and includes constructors for both deserialization and explicit initialization.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesIntraDayDataReadModel
{
    /// <summary>Full contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date of the market data (trading / settlement date).</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public long SequenceId { get; init; } 

    /// <summary>Symbol root (e.g. ES, NQ, CL, VX).</summary>
    [Key(3)]
    public string Symbol { get; init; }

    /// <summary>Open price.</summary>
    [Key(4)]
    public decimal OpenPrice { get; init; }

    /// <summary>Session high price.</summary>
    [Key(5)]
    public decimal HighPrice { get; init; }

    /// <summary>Session low price.</summary>
    [Key(6)]
    public decimal LowPrice { get; init; }

    /// <summary>Settlement / close price.</summary>
    [Key(7)]
    public decimal ClosePrice { get; init; }

    /// <summary>Session volume.</summary>
    [Key(8)]
    public int Volume { get; init; }

    /// <summary>Percent change over the analysis window (daily or rolling).</summary>
    [Key(9)]
    public double DailyPercentChange { get; init; }

    /// <summary>Rolling standard deviation (percent form).</summary>
    [Key(10)]
    public double DailyStdDev { get; init; }

    /// <summary>Rolling standard deviation expressed in price units.</summary>
    [Key(11)]
    public double DailyStdDevAmount { get; init; }

    /// <summary>Upper Bollinger / volatility band (if applicable).</summary>
    [Key(12)]
    public double UpperBand { get; init; }

    /// <summary>Rolling mean / middle band.</summary>
    [Key(13)]
    public double Mean { get; init; }

    /// <summary>Lower Bollinger / volatility band (if applicable).</summary>
    [Key(14)]
    public double LowerBand { get; init; }

    /// <summary>Classified overall market direction.</summary>
    [Key(15)]
    public MarketDirectionType MarketDirection { get; init; }

    /// <summary>Classified overall market volatility regime.</summary>
    [Key(16)]
    public MarketVolatilityType MarketVolatility { get; init; }

    /// <summary>Price direction classification.</summary>
    [Key(17)]
    public PriceDirectionType PriceDirection { get; init; }

    /// <summary>Price volatility classification.</summary>
    [Key(18)]
    public PriceVolatilityType PriceVolatility { get; init; }

    /// <summary>Directional indicator (MDI / custom composite).</summary>
    [Key(19)]
    public double MarketDirectionIndicator { get; init; }

    /// <summary>Window size used to compute rolling stats.</summary>
    [Key(20)]
    public int WindowSize { get; init; }

    /// <summary>50-day moving average (optional if pre-computed upstream).</summary>
    [Key(21)]
    public decimal FiftyDMA { get; set; }

    /// <summary>200-day moving average (optional if pre-computed upstream).</summary>
    [Key(22)]
    public decimal TwoHundredDMA { get; set; }

    /// <summary>Composite futures data identifier (not serialized).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesIntraDayDataId Id => new(ContractId ?? string.Empty, ValueDate, SequenceId);

    /// <summary>
    /// Parses and returns the strongly typed futures contract identifier (not serialized).
    /// </summary>
    public FuturesContractId GetContractId() => new FuturesContractIdParser(ContractId ?? string.Empty).Id;

    /// <summary>
    /// Parameterless constructor required by MessagePack and some serializers. Initializes numeric fields to zero and enums to defaults.
    /// </summary>
    public FuturesIntraDayDataReadModel() { }

    /// <summary>
    /// Full constructor enabling initialization of all serialized properties.
    /// </summary>
    public FuturesIntraDayDataReadModel(
        string contractId,
        DateOnly valueDate,
        long sequenceId,
        string symbol,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        int volume,
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
        SequenceId = sequenceId;
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
}