using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

public static class SampleData
{
    public const string Symbol = "ES";
    public static readonly DateOnly ValueDate = new(2024, 6, 15);
    public static readonly DateOnly StartDate = new(2024, 1, 1);
    public static readonly DateOnly EndDate = new(2024, 6, 30);
    public const MarketType Market = MarketType.Futures;
    public const CurrencyType Currency = CurrencyType.USD;

    public static readonly MarketDataFeedId FeedEntityId = new(ValueDate);
    public static readonly FeedId StreamingFeedId = new(42);
    public const int OrderId = 100;
    public const int TradeId = 200;

    // FuturesEodData sample data
    public static readonly FuturesEodDataId FuturesEodDataEntityId = new("ES20240621", ValueDate);
    public static readonly FuturesEodDataId VixFuturesEodDataEntityId = new("VX", ValueDate);

    public static readonly FuturesContractV2ReadModel EsContract = new(
        contractId: "ESM4",
        description: "E-Mini S&P 500",
        symbol: "ES",
        localSymbol: "ESM4",
        securityType: "FUT",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        lastTradeDate: new DateOnly(2024, 06, 21),
        currentlyTraded: true);

    public static readonly FuturesTickDataV2ReadModel EsTickData = new(
        contractId: "ESM4",
        valueDate: ValueDate,
        tickId: 1,
        tickTime: new TimeOnly(14, 30, 0),
        price: 5450.25m,
        size: 10);

    public static readonly FuturesEodDataV2ReadModel EodDataToday = new(
        contractId: "ES20240621",
        valueDate: ValueDate,
        symbol: "ES",
        openPrice: 5440.00m,
        highPrice: 5460.00m,
        lowPrice: 5430.00m,
        closePrice: 5448.75m,
        volume: 100000);

    public static readonly FuturesEodDataV2ReadModel[] EodDataRange = Enumerable.Range(0, 21)
        .Select(i => new FuturesEodDataV2ReadModel(
            contractId: "ES20240621",
            valueDate: ValueDate.AddDays(-i),
            symbol: "ES",
            openPrice: 5440.00m + i * 2,
            highPrice: 5460.00m + i * 2,
            lowPrice: 5430.00m + i * 2,
            closePrice: 5448.75m + i * 3,
            volume: 100000 + i * 1000))
        .ToArray();

    public static readonly FuturesEodClosingPriceReadModel[] EodClosingPrices =
    [
        new(Symbol, ValueDate.AddDays(-2), 5430.00m),
        new(Symbol, ValueDate.AddDays(-1), 5440.00m),
        new(Symbol, ValueDate, 5450.00m)
    ];

    public static readonly NormalCurveTableReadModel NormCurveData = new(
        Enumerable.Range(0, 101)
            .Select(i => new NormalCurveDataReadModel(i / 100.0 * 6.0 - 3.0, 50.0 - Math.Abs(i - 50)))
            .ToArray());

    public const int WindowSize = 20;

    public static readonly VixFuturesEodDataReadModel[] VixEodData =
    [
        new VixFuturesEodDataReadModel(
            contractId: "VX",
            valueDate: ValueDate,
            openPrice: 18.50m,
            highPrice: 19.00m,
            lowPrice: 18.00m,
            closePrice: 18.75m,
            volume: 50000)
    ];

    public static readonly FuturesTickDataV2ReadModel VixTickData = new(
        contractId: "VX",
        valueDate: ValueDate,
        tickId: 1,
        tickTime: new TimeOnly(14, 30, 0),
        price: 18.75m,
        size: 5);

    public static readonly FuturesContractV2ReadModel[] FuturesContracts =
    [
        new FuturesContractV2ReadModel(
            contractId: "ES",
            description: "E-Mini S&P 500",
            symbol: "ES",
            localSymbol: "ESM4",
            securityType: "FUT",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            lastTradeDate: new DateOnly(2024, 06, 21),
            currentlyTraded: true),
        new FuturesContractV2ReadModel(
            contractId: "NQ",
            description: "E-Mini Nasdaq 100",
            symbol: "NQ",
            localSymbol: "NQM4",
            securityType: "FUT",
            currency: "USD",
            exchange: "CME",
            multiplier: "20",
            lastTradeDate: new DateOnly(2024, 06, 21),
            currentlyTraded: true)
    ];

    public static readonly FuturesBarDataReadModel FuturesBarData1 = new(
        contractId: "ESM4",
        symbol: "ES",
        valueDate: ValueDate,
        barDate: new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
        barRateType: BarRateType.Minute,
        barValue: 5450.25m,
        upTrendTrigger: 0.75,
        downTrendTrigger: -0.50);

    public static readonly FuturesBarDataId FuturesBarDataId1 = new("ESM4", "ES", ValueDate);

    public static readonly DateTime FuturesBarWindowStart = FuturesBarData1.BarDate;
    public static readonly DateTime FuturesBarWindowEnd = FuturesBarData1.BarDate.AddMinutes(1);

    public static readonly FuturesDataId FuturesClosingPriceId1 = new("ESM4", ValueDate);
    public const decimal ClosingPrice1 = 5450.25m;

    // FuturesOptionQuoteData sample data
    public const int OptionQuoteStreamId = 500;

    public static readonly FuturesOptionQuoteReadModel[] FuturesOptionQuotes =
    [
        new FuturesOptionQuoteReadModel(OptionQuoteStreamId, "ES20240601C5500", 1001, "TestUser", new DateTime(2024, 06, 15, 10, 0, 0, DateTimeKind.Utc)),
        new FuturesOptionQuoteReadModel(OptionQuoteStreamId, "ES20240601P5400", 1002, "TestUser", new DateTime(2024, 06, 15, 10, 0, 0, DateTimeKind.Utc)),
    ];

    public static readonly FuturesOptionContractReadModel[] FuturesOptionContracts =
    [
        new FuturesOptionContractReadModel("ES20240601C5500", "ES Jun24 5500 Call", "ES", "ESM4 C5500", "FOP", "USD", "CME", "50", new DateOnly(2024, 6, 1), 5500.0, "Call"),
        new FuturesOptionContractReadModel("ES20240601P5400", "ES Jun24 5400 Put", "ES", "ESM4 P5400", "FOP", "USD", "CME", "50", new DateOnly(2024, 6, 1), 5400.0, "Put"),
    ];

    public static readonly QuoteData AskPriceQuoteData = new(
        new DateTime(2024, 06, 15, 14, 30, 0, DateTimeKind.Utc),
        QuoteLevelType.LevelOne, 0, 1, QuoteSide.Ask, QuoteType.Price, 12.50, 0);

    public static readonly QuoteData BidPriceQuoteData = new(
        new DateTime(2024, 06, 15, 14, 30, 0, DateTimeKind.Utc),
        QuoteLevelType.LevelOne, 0, 1, QuoteSide.Bid, QuoteType.Price, 11.25, 0);

    // FuturesOptionTickData sample data
    public static readonly FuturesOptionTickEntityId OptionTickStreamingFeedId = new("ES20240601C5500", ValueDate);
    public static readonly DateOnly OptionMaturityDate = new(2024, 06, 21);
    public const double RiskFreeRate = 0.05;

    public static readonly FuturesOptionTickDataV2ReadModel EsOptionTickData = new(
        contractId: "ES20240601C5500",
        valueDate: ValueDate,
        tickId: 1,
        tickTime: new TimeOnly(14, 30, 0),
        optionPrice: 12.50,
        bidPrice: 12.25,
        askPrice: 12.75,
        bidSize: 100,
        askSize: 150,
        impliedVolatility: 0.15,
        underlyingPrice: 5450.25,
        delta: 0.55,
        gamma: 0.02,
        vega: 15.5,
        theta: -2.5,
        rho: 0.10);
}
