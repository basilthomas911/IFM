using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests;

/// <summary>
/// Provides sample data for BDD tests related to market data feed command handlers.
/// </summary>
public static class SampleData
{
    public static readonly DateOnly ValueDate = new(2025, 06, 15);

    public static readonly MarketDataFeedId FeedEntityId = new(ValueDate);

    public static readonly FuturesDataId FuturesClosingPriceId = new("ES", ValueDate);

    public const decimal ClosingPrice = 5448.75m;

    public static readonly FeedId StreamingFeedId = new(42);

    public static readonly FuturesEodDataId FuturesEodDataEntityId = new("ES", ValueDate);

    public static readonly FuturesEodDataId VixFuturesEodDataEntityId = new("VX", ValueDate);

    public static readonly FuturesContractV2ReadModel EsContract = new(
        contractId: "ES",
        description: "E-Mini S&P 500",
        symbol: "ES",
        localSymbol: "ESM5",
        securityType: "FUT",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        lastTradeDate: new DateOnly(2025, 06, 20),
        currentlyTraded: true);

    public static readonly FuturesTickDataV2ReadModel EsTickData = new(
        contractId: "ES",
        valueDate: ValueDate,
        tickId: 1,
        tickTime: new TimeOnly(14, 30, 0),
        price: 5450.25m,
        size: 10);

    public static readonly FuturesEodDataV2ReadModel EodDataToday = new(
        contractId: "ES",
        valueDate: ValueDate,
        symbol: "ES",
        openPrice: 5440.00m,
        highPrice: 5460.00m,
        lowPrice: 5430.00m,
        closePrice: 5448.75m,
        volume: 100000);

    public static readonly FuturesEodDataV2ReadModel[] EodDataRange = Enumerable.Range(0, 21)
        .Select(i => new FuturesEodDataV2ReadModel(
            contractId: "ES",
            valueDate: ValueDate.AddDays(-i),
            symbol: "ES",
            openPrice: 5440.00m + i * 2,
            highPrice: 5460.00m + i * 2,
            lowPrice: 5430.00m + i * 2,
            closePrice: 5448.75m + i * 3,
            volume: 100000 + i * 1000))
        .ToArray();

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
            localSymbol: "ESM5",
            securityType: "FUT",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            lastTradeDate: new DateOnly(2025, 06, 20),
            currentlyTraded: true),
        new FuturesContractV2ReadModel(
            contractId: "NQ",
            description: "E-Mini Nasdaq 100",
            symbol: "NQ",
            localSymbol: "NQM5",
            securityType: "FUT",
            currency: "USD",
            exchange: "CME",
            multiplier: "20",
            lastTradeDate: new DateOnly(2025, 06, 20),
            currentlyTraded: true)
    ];

    public static readonly FuturesBarDataReadModel FuturesBarData = new(
        contractId: "ES",
        symbol: "ES",
        valueDate: ValueDate,
        barDate: new DateTime(2025, 06, 15, 14, 30, 0, DateTimeKind.Utc),
        barRateType: BarRateType.Minute,
        barValue: 5450.25m,
        upTrendTrigger: 5460.0,
        downTrendTrigger: 5440.0);

    public static readonly FuturesBarDataReadModel FuturesBarDataAlternate = new(
        contractId: "NQ",
        symbol: "NQ",
        valueDate: ValueDate,
        barDate: new DateTime(2025, 06, 15, 14, 31, 0, DateTimeKind.Utc),
        barRateType: BarRateType.Minute,
        barValue: 19200.50m,
        upTrendTrigger: 19220.0,
        downTrendTrigger: 19180.0);

    public const int OrderId = 100;
    public const int TradeId = 200;

    public const int OptionQuoteStreamId = 500;

    public static readonly FuturesOptionQuoteReadModel[] FuturesOptionQuotes =
    [
        new FuturesOptionQuoteReadModel(OptionQuoteStreamId, "ES20250601C5500", 1001, "TestUser", new DateTime(2025, 06, 15, 10, 0, 0, DateTimeKind.Utc)),
        new FuturesOptionQuoteReadModel(OptionQuoteStreamId, "ES20250601P5400", 1002, "TestUser", new DateTime(2025, 06, 15, 10, 0, 0, DateTimeKind.Utc)),
    ];

    public static readonly FuturesOptionContractReadModel[] FuturesOptionContracts =
    [
        new FuturesOptionContractReadModel("ES20250601C5500", "ES Jun25 5500 Call", "ES", "ESM5 C5500", "OPT", "USD", "CME", "50", new DateOnly(2025, 6, 1), 5500.0, "Call"),
        new FuturesOptionContractReadModel("ES20250601P5400", "ES Jun25 5400 Put", "ES", "ESM5 P5400", "OPT", "USD", "CME", "50", new DateOnly(2025, 6, 1), 5400.0, "Put"),
    ];

    public static readonly FuturesOptionTickDataV2ReadModel EsOptionTickData = new(
        contractId: "ES20250601C5500",
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

    public static readonly FuturesOptionTickEntityId OptionTickStreamingFeedId = new("ES20250601C5500", ValueDate);

    public static readonly DateOnly OptionMaturityDate = new(2025, 06, 20);

    public const double RiskFreeRate = 0.05;

    public static readonly QuoteData AskPriceQuoteData = new(
        new DateTime(2025, 06, 15, 14, 30, 0, DateTimeKind.Utc),
        QuoteLevelType.LevelOne, 0, 1, QuoteSide.Ask, QuoteType.Price, 12.50, 0);

    public static readonly QuoteData BidPriceQuoteData = new(
        new DateTime(2025, 06, 15, 14, 30, 0, DateTimeKind.Utc),
        QuoteLevelType.LevelOne, 0, 1, QuoteSide.Bid, QuoteType.Price, 11.25, 0);
}
