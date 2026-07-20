using System;
using System.Text;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Domain.Securities.BDDTests;

public class SampleData
{
    public static FuturesContractV2ReadModel FuturesContract
         => new(
             contractId: "ES20251010",
             description: "Test Description",
             symbol: "TEST",
             localSymbol: "TEST_LOCAL",
             securityType: "FUT",
             currency: "USD",
             exchange: "NYSE",
             multiplier: "100",
             lastTradeDate: DateOnly.FromDateTime(DateTime.UtcNow),
             currentlyTraded: true);

    /// <summary>
    /// Alternate sample futures contract view model for BDD testing.
    /// </summary>
    public static FuturesContractV2ReadModel FuturesContractAlternate
         => new(
             contractId: "NQ20251215",
             description: "Alternate Test Description",
             symbol: "ALT",
             localSymbol: "ALT_LOCAL",
             securityType: "FUT",
             currency: "USD",
             exchange: "CME",
             multiplier: "50",
             lastTradeDate: new DateOnly(2025, 12, 15),
             currentlyTraded: true);

    public static FuturesOptionContractReadModel FuturesOptionContract
        => new(
            contractId: "ES20251010C2525",
            description: "Test Option Description",
            symbol: "TEST_OPT",
            localSymbol: "TEST_LOCAL_OPT",
            securityType: "FOP",
            currency: "USD",
            exchange: "NYSE",
            multiplier: "50",
            contractMonth: DateOnly.FromDateTime(DateTime.UtcNow),
            strikePrice: 1000,
            optionType: "Call");
    
    // Sample FuturesBarDataReadModel instance
    public static FuturesBarDataReadModel FuturesBarData => new (
        contractId: "TestContract",
        symbol: "TestSymbol",
        valueDate:  DateOnly.FromDateTime(DateTime.Now.Date),
        barDate: DateTime.Now,
        barRateType: BarRateType.Minute,
        barValue: 100.0m,
        upTrendTrigger: 1.0,
        downTrendTrigger: -1.0
    );

    public static FuturesClosingPriceReadModel FuturesClosingPrice => new (
        contractId: "SampleContractId",
        valueDate: new DateOnly(2023, 10, 10),
        closingPrice: 123.45m,
        createdOn: DateTime.UtcNow,
        createdBy: "TestUser"
    );

    public static FuturesClosingPriceReadModel YesterdaysFuturesClosingPrice => new (
        contractId: "SampleContractId",
        valueDate: new DateOnly(2023, 10, 9),
        closingPrice: 120.00m,
        createdOn: DateTime.UtcNow.AddDays(-1),
        createdBy: "TestUser"
    );

    public static FuturesTickDataV2ReadModel FuturesTickData => new (
        contractId: "SampleContractId",
        valueDate: new DateOnly(2023, 10, 10),
        tickId: 1234,
        tickTime: new TimeOnly(10,10,2),
        price: 456.34m,
        size: 10
    );

    public static FuturesTickDataV2ReadModel FuturesTickDataLowPrice => new (
        contractId: "SampleContractId",
        valueDate: new DateOnly(2023, 10, 10),
        tickId: 1235,
        tickTime: new TimeOnly(10, 10, 1),
        price: 123.45m,
        size: 10
    );

    public static FuturesTickDataV2ReadModel FuturesTickDataHighPrice => new (
        contractId: "SampleContractId",
        valueDate: new DateOnly(2023, 10, 10),
        tickId: 1236,
        tickTime: new TimeOnly(10, 10, 3),
        price: 5593.22m,
        size: 10
    );

    public static FuturesEodDataId FuturesEodDataId => new ("SampleContractId", new DateOnly(2023, 10, 10));
    public static FuturesDataId FuturesClosingPriceId => new ("SampleContractId", new DateOnly(2023, 10, 10));
    public static decimal FuturesOpenPrice => 123.45m;
    public static decimal FuturesHighPrice => 130.98m;

    // Sample FuturesEodDataV2ReadModel instance
    public static FuturesEodDataV2ReadModel FuturesEodData => new (
        contractId: "SampleContractId",
        valueDate: new DateOnly(2023, 10, 10),
        symbol: "SampleSymbol",
        openPrice: 100m,
        highPrice: 105m,
        lowPrice: 95.0m,
        closePrice: 102.0m,
        volume: 1000,
        dailyPercentChange: 0.5,
        dailyStdDev: 0.2,
        dailyStdDevAmount: 0.1,
        upperBand: 110.0,
        mean: 100.0,
        lowerBand: 90.0,
        marketDirection: MarketDirectionType.NeutralUp,
        marketVolatility: MarketVolatilityType.Normal,
        priceDirection: PriceDirectionType.Flat,
        priceVolatility: PriceVolatilityType.Unknown,
        marketDirectionIndicator: 0.0,
        windowSize: 20
    )
    {
        FiftyDMA = 50.0m,
        TwoHundredDMA = 200.0m
    };

    public static FuturesEodDataV2ReadModel YesterdaysFuturesEodData => new (
        contractId: "SampleContractId",
        valueDate: new DateOnly(2023, 10, 9),
        symbol: "SampleSymbol",
        openPrice: 100.0m,
        highPrice: 105.0m,
        lowPrice: 95.0m,
        closePrice: 102.0m,
        volume: 1000,
        dailyPercentChange: 0.5,
        dailyStdDev: 0.2,
        dailyStdDevAmount: 0.1,
        upperBand: 110.0,
        mean: 100.0,
        lowerBand: 90.0,
        marketDirection: MarketDirectionType.NeutralUp,
        marketVolatility: MarketVolatilityType.Normal,
        priceDirection: PriceDirectionType.Flat,
        priceVolatility: PriceVolatilityType.Unknown,
        marketDirectionIndicator: 0.0,
        windowSize: 20
    )
    {
        FiftyDMA = 50.0m,
        TwoHundredDMA = 200.0m
    };

    public static FuturesItiSignalV2ReadModel FuturesItiSignal1 => new (
        contractId: "SYM20251215",
        valueDate: new DateOnly(2025, 1, 15),
        timePeriod: TradeTimePeriodType.FifteenSeconds,
        sequenceId: 1,
        intrinsicTime: DateTime.Now,
        intrinsicTimeGroupId: 1,
        intrinsicTimeLength: 1,
        intrinsicPrice: 1,
        intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
        intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
        trendPrice: 1,
        trendExtreme: 1,
        trendReversal: 1,
        trendDelta: 1,
        targetDelta: 1,
        lambda: 1,
        tradingDays: 0,
        threshold: 0,
        upTrendTrigger: 1,
        downTrendTrigger: 1,
        tradeState: IntrinsicTimeTradeState.Ready
    );

    public static FuturesItiSignalV2ReadModel FuturesItiSignal2 => new (
        contractId: "SYM20251230",
        valueDate: new DateOnly(2025, 1, 20),
        timePeriod: TradeTimePeriodType.FifteenSeconds,
        sequenceId: 2,
        intrinsicTime: DateTime.Now,
        intrinsicTimeGroupId: 2,
        intrinsicTimeLength: 2,
        intrinsicPrice: 2,
        intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
        intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
        trendPrice: 2,
        trendExtreme: 2,
        trendReversal: 2,
        trendDelta: 2,
        targetDelta: 2,
        lambda: 2,
        tradingDays: 0,
        threshold: 0,
        upTrendTrigger: 2,
        downTrendTrigger: 2,
        tradeState: IntrinsicTimeTradeState.Ready
    );

    public static FuturesContractV2ReadModel FuturesContract1 = new ("SYM20251215", "Description1", "SYM", "LocalSymbol1", "FUT", "USD", "CME", "50", new DateOnly(2025, 12, 15), true);
    public static FuturesContractV2ReadModel FuturesContract2 = new ("SYM20251230", "Description2", "SYM", "LocalSymbol2", "FUT", "USD", "CME", "50", new DateOnly(2025, 12,30), true);

    public static FuturesOptionContractReadModel FuturesOptionContract1 = new (
        contractId: "ES20251215C5000",
        description: "E-mini S&P 500 Dec 2025 Call 5000",
        symbol: "ES",
        localSymbol: "ES 25Dec25 C5000",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: new DateOnly(2025, 12, 15),
        strikePrice: 5000.0,
        optionType: "Call"
    );

    public static FuturesOptionContractReadModel FuturesOptionContract2 = new (
        contractId: "ES20251215P4900",
        description: "E-mini S&P 500 Dec 2025 Put 4900",
        symbol: "ES",
        localSymbol: "ES 25Dec25 P4900",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: new DateOnly(2025, 12, 15),
        strikePrice: 4900.0,
        optionType: "Put"
    );

    public static FuturesItiTrendClassModelReadModel FuturesItiTrendClassModel => new 
    (
        symbol: "SYM",
        valueDate: new DateOnly(2025, 2, 17),
        startDate: new DateOnly(2025, 1, 1),
        endDate: new DateOnly(2025, 1, 31),
        count: 100,
        maximum: 10.0,
        mean: 5.0,
        median: 5.0,
        minimum: 1.0,
        skewness: 0.5,
        stdDev: 2.0,
        variance: 4.0,
        accuracy: 0.95,
        areaUnderPrecisionRecallCurve: 0.85,
        areaUnderRocCurve: 0.9,
        entropy: 0.1,
        f1Score: 0.8,
        modelData: Encoding.UTF8.GetBytes("SampleModelData")
    );

    public static FuturesItiTrendDeltaModelReadModel FuturesItiTrendDeltaModel => new 
    (
        symbol: "SYM",
        valueDate: new DateOnly(2025, 2, 17),
        startDate: new DateOnly(2025, 1, 1),
        endDate: new DateOnly(2025, 1, 31),
        count: 100,
        maximum: 10.0,
        mean: 5.0,
        median: 5.0,
        minimum: 1.0,
        skewness: 0.5,
        stdDev: 2.0,
        variance: 4.0,
        meanAbsoluteError: 0.1,
        meanSquaredError: 0.2,
        rootMeanSquaredError: 0.3,
        lossFunction: 0.4,
        rSquared: 0.95,
        modelData: Encoding.UTF8.GetBytes("SampleModelData")
    );

    public static FuturesOptionTickDataV2ReadModel FuturesOptionTickData => new 
   (
       contractId: "ES20180511P2525",
       valueDate: new DateOnly(2025, 2, 20),
       tickId: 123456,
       tickTime: new TimeOnly(14, 30, 0),
       optionPrice: 100.5,
       bidPrice: 100.0,
       askPrice: 101.0,
       bidSize: 10,
       askSize: 15,
       impliedVolatility: 0.25,
       underlyingPrice: 150.0,
       delta: 0.5,
       gamma: 0.1,
       vega: 0.2,
       theta: -0.01,
       rho: 0.05
   );

    public static FuturesRsiSignalReadModel FuturesRsiSignal => new (
        contractId: "SYM20251230",
        valueDate: new DateOnly(2025, 2, 20),
        timePeriod: TradeTimePeriodType.OneMinute,
        periodLength: 14,
        timestamp: new TimeOnly(14, 30, 0),
        price: 150.0m,
        priceChange: 1.5m,
        priceGain: 1.5m,
        priceLoss: 0.0m,
        averagePriceGain: 1.0m,
        averagePriceLoss: 0.5m,
        rs: 2.0,
        rsi: 66.67,
        rsiAverage: 65.0,
        rsiSlope: 0.5
    );

    public static FuturesTdiSignalReadModel FuturesTdiSignal => new (
        contractId: "SYM20251230",
        valueDate: new DateOnly(2025, 2, 20),
        timePeriod: TradeTimePeriodType.FifteenSeconds,
        timestamp: TimeOnly.FromDateTime(DateTime.Now),
        upTrendCount: 5,
        downTrendCount: 3,
        tdi: FuturesTrendDirectionType.UpTrending,
        tdiStrength: FuturesTrendDirectionStrengthType.High
    );

    public static FuturesTradeSignalV2ReadModel FuturesTradeSignal => new (
        contractId: "SYM20251230",
        valueDate: new DateOnly(2025, 2, 20),
        timePeriod: TradeTimePeriodType.FifteenSeconds,
        sequenceId: 123456,
        timestamp: new TimeOnly(14, 30, 0),
        mean: 100.5,
        stdDev: 10.0,
        futuresPrice: 150.0,
        priceChangePercent: 1.5,
        fundRiskPercent: 0.5,
        rsi: 70.0,
        rsiSlope: 0.5,
        trendType: FuturesTrendType.UpTrend,
        trendStrength: FuturesTrendStrengthType.High,
        tradeSignal: TradeSignalType.Buy,
        tdi: FuturesTrendDirectionType.UpTrending,
        tdiStrength: FuturesTrendDirectionStrengthType.High,
        mdi: 1.0,
        mdiTrend: FuturesMDITrendType.UpTrending,
        mdiUpTrendLimit: 1.5,
        mdiDownTrendLimit: 0.5,
        upTrendingTrigger: 1.2,
        downTrendingTrigger: 0.8,
        entryTrigger: 1.1,
        exitTrigger: 0.9,
        trendDelta: 0.5,
        trendExtreme: 1.0,
        trendReversal: 0.7,
        fiftyDMA: 50.0m,
        twoHundredDMA: 200.0m,
        tradeExecuteState: TradeExecuteState.Enter
    );

    public static RateOfReturnReadModel RateOfReturn => new (
        symbol: "SYM",
        valueDate: new DateOnly(2025, 2, 20),
        rateOfReturn: 0.05
    );

    public static VixFuturesEodDataReadModel VixFuturesEodData => new (
        contractId  : "VX20201216",
        valueDate: new DateOnly(2023, 10, 10),
        openPrice: 20.0m,
        highPrice: 22.0m,
        lowPrice: 19.0m,
        closePrice: 21.0m,
        volume: 1500
    );

    public static FuturesTickDataV2ReadModel VixFuturesTickData => new (
        contractId: "VX20201216",
        valueDate: new DateOnly(2023, 10, 10),
        tickId: 1234,
        tickTime: new TimeOnly(10, 10, 2),
        price: 20.5m,
        size: 100
    );

    public static FuturesTickDataV2ReadModel VixFuturesTickDataLowPrice => new (
       contractId: "VX20201216",
       valueDate: new DateOnly(2023, 10, 10),
       tickId: 1234,
       tickTime: new TimeOnly(10, 10, 2),
       price: 10.0m,
       size: 100
   );

    public static FuturesTickDataV2ReadModel VixFuturesTickDataHighPrice => new (
       contractId: "VX20201216",
       valueDate: new DateOnly(2023, 10, 10),
       tickId: 1234,
       tickTime: new TimeOnly(10, 10, 2),
       price: 130.75m,
       size: 100
   );

    public static YieldCurveRateReadModel YieldCurveRate => new (
        valueDate: new DateOnly(2025, 3, 1),
        oneMonth: 0.01,
        twoMonth: 0.015,
        threeMonth: 0.02,
        sixMonth: 0.025,
        oneYear: 0.03,
        twoYear: 0.035,
        threeYear: 0.04,
        fiveYear: 0.045,
        sevenYear: 0.05,
        tenYear: 0.055,
        twentyYear: 0.06,
        thirtyYear: 0.065
    );

    public static MarketHolidayReadModel MarketHoliday1 => new (
        currencyType: CurrencyType.USD,
        holidayDate: new DateOnly(2025, 1, 1),
        description: "New Year's Day"
    );

    public static MarketHolidayReadModel MarketHoliday2 => new (
        currencyType: CurrencyType.USD,
        holidayDate: new DateOnly(2025, 12, 25),
        description: "Christmas Day"
    );
}
