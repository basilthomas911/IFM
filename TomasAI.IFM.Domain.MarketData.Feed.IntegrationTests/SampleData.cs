using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;


namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests;

public static class SampleData
{
    public const string Symbol = "ES";
    public static readonly DateOnly ValueDate = DateOnly.FromDateTime(DateTime.UtcNow);

    public static RateOfReturnReadModel RateOfReturn => new(Symbol, ValueDate, 0.05);

    public static MarketHolidayReadModel MarketHoliday => new(CurrencyType.USD, new DateOnly(2025, 7, 4), "Independence Day");

    public static FuturesOptionContractReadModel FuturesOptionContract => new(
        contractId: "ES20251010C5000",
        description: "Test Futures Option Contract",
        symbol: "ES",
        localSymbol: "EW1K6 C5000",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
        strikePrice: 5000.0,
        optionType: "Call"
    );

    public static FuturesOptionContractReadModel ShortOptionContract => new(
        contractId: "ES20251219P5400",
        description: "E-mini S&P 500 Dec 2025 Put 5400",
        symbol: Symbol,
        localSymbol: "ESZ5 P5400",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: new DateOnly(2025, 12, 1),
        strikePrice: 5400.0,
        optionType: "Put");

    public static FuturesOptionContractReadModel LongOptionContract => new(
        contractId: "ES20251219P5300",
        description: "E-mini S&P 500 Dec 2025 Put 5300",
        symbol: Symbol,
        localSymbol: "ESZ5 P5300",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: new DateOnly(2025, 12, 1),
        strikePrice: 5300.0,
        optionType: "Put");

    public static FuturesOptionTickDataV2ReadModel ShortOptionTickData => new(
        contractId: ShortOptionContract.ContractId,
        valueDate: ValueDate,
        tickId: 1,
        tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
        optionPrice: 25.50,
        bidPrice: 25.25,
        askPrice: 25.75,
        bidSize: 10,
        askSize: 15,
        impliedVolatility: 0.18,
        underlyingPrice: 5450.0,
        delta: -0.35,
        gamma: 0.008,
        vega: 12.5,
        theta: -1.25,
        rho: -0.05);

    public static FuturesOptionTickDataV2ReadModel LongOptionTickData => new(
        contractId: LongOptionContract.ContractId,
        valueDate: ValueDate,
        tickId: 1,
        tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
        optionPrice: 15.75,
        bidPrice: 15.50,
        askPrice: 16.00,
        bidSize: 8,
        askSize: 12,
        impliedVolatility: 0.20,
        underlyingPrice: 5450.0,
        delta: -0.22,
        gamma: 0.006,
        vega: 10.0,
        theta: -0.95,
        rho: -0.03);

    public static FuturesOptionContractReadModel ShortCallOptionContract => new(
        contractId: "ES20251219C5500",
        description: "E-mini S&P 500 Dec 2025 Call 5500",
        symbol: Symbol,
        localSymbol: "ESZ5 C5500",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: new DateOnly(2025, 12, 1),
        strikePrice: 5500.0,
        optionType: "Call");

    public static FuturesOptionContractReadModel LongCallOptionContract => new(
        contractId: "ES20251219C5600",
        description: "E-mini S&P 500 Dec 2025 Call 5600",
        symbol: Symbol,
        localSymbol: "ESZ5 C5600",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: new DateOnly(2025, 12, 1),
        strikePrice: 5600.0,
        optionType: "Call");

    public static FuturesOptionTickDataV2ReadModel ShortCallOptionTickData => new(
        contractId: ShortCallOptionContract.ContractId,
        valueDate: ValueDate,
        tickId: 1,
        tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
        optionPrice: 18.00,
        bidPrice: 17.75,
        askPrice: 18.25,
        bidSize: 12,
        askSize: 14,
        impliedVolatility: 0.17,
        underlyingPrice: 5450.0,
        delta: 0.30,
        gamma: 0.007,
        vega: 11.5,
        theta: -1.10,
        rho: 0.04);

    public static FuturesOptionTickDataV2ReadModel LongCallOptionTickData => new(
        contractId: LongCallOptionContract.ContractId,
        valueDate: ValueDate,
        tickId: 1,
        tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
        optionPrice: 10.25,
        bidPrice: 10.00,
        askPrice: 10.50,
        bidSize: 6,
        askSize: 10,
        impliedVolatility: 0.19,
        underlyingPrice: 5450.0,
        delta: 0.18,
        gamma: 0.005,
        vega: 9.0,
        theta: -0.80,
        rho: 0.02);

    public static readonly string FuturesContractId = "ES20251010";

    public static FuturesContractV2ReadModel FuturesContract => new(
        contractId: FuturesContractId,
        description: "E-mini S&P 500 Dec 2025",
        symbol: Symbol,
        localSymbol: "ESZ5",
        securityType: "FUT",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        lastTradeDate: new DateOnly(2025, 12, 19),
        currentlyTraded: true);

    public static FuturesTickDataV2ReadModel UnderlyingFuturesTickData => new(
        contractId: FuturesContractId,
        valueDate: ValueDate,
        tickId: 1,
        tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
        price: 5497.25m,
        size: 200);

    public static FuturesBarDataReadModel FuturesBarData => new(
        contractId: FuturesContractId,
        symbol: Symbol,
        valueDate: ValueDate,
        barDate: DateTime.UtcNow,
        barRateType: BarRateType.Minute,
        barValue: 5450.25m,
        upTrendTrigger: 0.65,
        downTrendTrigger: 0.35);

    public static FuturesEodDataIndexReadModel FuturesEodDataIndex => new(ValueDate, FuturesContractId);

    public static FuturesEodDataV2ReadModel FuturesEodData => new(
        contractId: FuturesContractId,
        valueDate: ValueDate,
        symbol: Symbol,
        openPrice: 5400m,
        highPrice: 5475m,
        lowPrice: 5380m,
        closePrice: 5450m,
        volume: 150000,
        dailyPercentChange: 0.5,
        dailyStdDev: 0.012,
        dailyStdDevAmount: 65.0,
        upperBand: 5550.0,
        mean: 5425.0,
        lowerBand: 5300.0,
        marketDirection: MarketDirectionType.NeutralUp,
        marketVolatility: MarketVolatilityType.Normal,
        priceDirection: PriceDirectionType.Rising,
        priceVolatility: PriceVolatilityType.Falling);

    public static List<FuturesEodDataV2ReadModel> FuturesEodDataRange => Enumerable.Range(0, 20)
        .Select(i =>
        {
            var date = ValueDate.AddDays(-i);
            var baseClose = 5450m - i * 10m;
            return new FuturesEodDataV2ReadModel(
                contractId: FuturesContractId,
                valueDate: date,
                symbol: Symbol,
                openPrice: baseClose - 50m,
                highPrice: baseClose + 25m,
                lowPrice: baseClose - 70m,
                closePrice: baseClose,
                volume: 150000 - i * 1000,
                dailyPercentChange: 0.5 - i * 0.02,
                dailyStdDev: 0.012,
                dailyStdDevAmount: 65.0,
                upperBand: (double)(baseClose + 100m),
                mean: (double)(baseClose - 25m),
                lowerBand: (double)(baseClose - 150m),
                marketDirection: MarketDirectionType.NeutralUp,
                marketVolatility: MarketVolatilityType.Normal,
                priceDirection: PriceDirectionType.Rising,
                priceVolatility: PriceVolatilityType.Falling);
        })
        .ToList();

    public static QuoteData AskPriceQuoteData => new(
        quoteTime: DateTime.UtcNow,
        levelType: QuoteLevelType.LevelOne,
        position: 0,
        operation: 1,
        side: QuoteSide.Ask,
        quoteType: QuoteType.Price,
        price: 12.50,
        size: 0);
}
