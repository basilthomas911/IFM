using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests;

public static class SampleData
{
    public const string ContractId = "ESU25";
    public static readonly DateOnly ValueDate = new(2025, 6, 20);
    public static readonly TradeTimePeriodType TimePeriod = TradeTimePeriodType.Daily;
    public static readonly int PeriodLength = 14;
    public static readonly DateTime Timestamp = new(2025, 6, 20, 10, 0, 0);
    public const string Symbol = "ES";
    public const decimal FuturesPrice = 5500.0m;
    public const double Lambda = 0.01;
    public const double FuturesPercentChange = 0.5;
    public const double FuturesMean = 5480.0;
    public const double FuturesStdDev = 20.0;
    public const double FuturesMDI = 0.6;
    public const double FuturesRSI = 55.0;
    public const double FuturesRSISlope = 0.1;
    public const decimal FuturesFiftyDMA = 5450.0m;
    public const decimal FuturesTwoHundredDMA = 5300.0m;
    public const double PredictedDelta = 10.0;

    public static FuturesItiSignalEntityId EntityId
        => new(ContractId, ValueDate, TimePeriod);

    /// <summary>
    /// Builds a <see cref="FuturesItiSignalEntityId"/> for the given time period, allowing ITI tests to exercise
    /// Daily, Weekly, and Monthly variants without mutating the shared <see cref="TimePeriod"/> default.
    /// </summary>
    public static FuturesItiSignalEntityId EntityIdFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod);

    public static FuturesItiTrendCoastLineCountersReadModel CoastLineCounters
        => new(upTrendCount: 3, downTrendCount: 3);

    public static GenerateFuturesItiSignalCommand GenerateCommand
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            timestamp: Timestamp,
            futuresPrice: (double)FuturesPrice,
            vixFuturesPrice: 0);

    /// <summary>
    /// Builds a <see cref="GenerateFuturesItiSignalCommand"/> for the given time period.
    /// </summary>
    public static GenerateFuturesItiSignalCommand GenerateCommandFor(TradeTimePeriodType timePeriod)
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: timePeriod,
            timestamp: Timestamp,
            futuresPrice: (double)FuturesPrice,
            vixFuturesPrice: 0);

    public static SetFuturesItiSignalHoldTradeCommand SetHoldTradeCommand
        => new(ContractId, ValueDate, TimePeriod, Timestamp);

    /// <summary>
    /// Builds a <see cref="SetFuturesItiSignalHoldTradeCommand"/> for the given time period.
    /// </summary>
    public static SetFuturesItiSignalHoldTradeCommand SetHoldTradeCommandFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, Timestamp);

    public static ClearFuturesItiSignalHoldTradeCommand ClearHoldTradeCommand
        => new(ContractId, ValueDate, TimePeriod, Timestamp);

    /// <summary>
    /// Builds a <see cref="ClearFuturesItiSignalHoldTradeCommand"/> for the given time period.
    /// </summary>
    public static ClearFuturesItiSignalHoldTradeCommand ClearHoldTradeCommandFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, Timestamp);

    public static FuturesItiSignalGeneratedEvent StartOfDayEvent
        => new()
        {
            EntityId = EntityId,
            FuturesItiSignal = new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: 0,
                intrinsicTime: Timestamp,
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 0,
                intrinsicPrice: (double)FuturesPrice,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
                trendPrice: (double)FuturesPrice,
                trendExtreme: (double)FuturesPrice,
                trendReversal: (double)FuturesPrice,
                trendDelta: PredictedDelta + (((double)FuturesPrice * Lambda) / 2),
                targetDelta: (double)FuturesPrice * Lambda,
                lambda: Lambda,
                tradingDays: 0,
                threshold: 0,
                upTrendTrigger: (double)FuturesPrice,
                downTrendTrigger: (double)FuturesPrice - ((double)FuturesPrice * Lambda),
                tradeState: IntrinsicTimeTradeState.Ready),
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    public static FuturesItiSignalGeneratedEvent HoldTradeEvent
        => new()
        {
            EntityId = EntityId,
            FuturesItiSignal = StartOfDayEvent.FuturesItiSignal! with
            {
                TradeState = IntrinsicTimeTradeState.Hold,
                IntrinsicTimeMode = IntrinsicTimeModeType.HoldTradeChanged
            },
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    // ─── MACD Signal sample data ───────────────────────────────────────

    public static FuturesMacdSignalId FuturesMacdSignalId => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timePeriod: TradeTimePeriodType.Daily,
        periodLength: 14,
        timestamp: new TimeOnly(18, 50, 10, 451)
    );

    /// <summary>
    /// Rising prices series – fast EMA rises faster than slow EMA → positive MACD line,
    /// producing an UpTrending model direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] UpTrendingRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4000m + (i * 5m),
            priceChange: 5m,
            priceGain: 5m,
            priceLoss: 0m,
            averagePriceGain: 5m,
            averagePriceLoss: 0m,
            rs: 100,
            rsi: 100,
            rsiAverage: 100,
            rsiSlope: 1.0))
        .ToArray();

    /// <summary>
    /// Falling prices series – fast EMA falls faster than slow EMA → negative MACD line,
    /// producing a DownTrending model direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] DownTrendingRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4200m - (i * 5m),
            priceChange: -5m,
            priceGain: 0m,
            priceLoss: 5m,
            averagePriceGain: 0m,
            averagePriceLoss: 5m,
            rs: 0,
            rsi: 0,
            rsiAverage: 0,
            rsiSlope: -1.0))
        .ToArray();

    /// <summary>
    /// Flat prices series – same price throughout → MACD line near zero → RangeBound model direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] FlatRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4100m,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 1.0,
            rsi: 50,
            rsiAverage: 50,
            rsiSlope: 0))
        .ToArray();

    /// <summary>
    /// Minimal single-element RSI signal array for edge-case testing.
    /// </summary>
    public static FuturesRsiSignalReadModel[] SingleRsiSignal => [
        new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(18, 50, 10, 451),
            price: 4022,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 1.0,
            rsi: 50,
            rsiAverage: 50,
            rsiSlope: 0)
    ];

    public static FuturesRsiSignalEntityId RsiEntityIdFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, 14);

    public static FuturesRsiSignalId RsiSignalIdFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, 14, TimeOnly.FromDateTime(Timestamp));

    public static FuturesRsiSignalId RsiSignalId
        => RsiSignalIdFor(TradeTimePeriodType.Daily);

    public static GenerateFuturesRsiSignalCommand RsiGenerateCommandFor(TradeTimePeriodType timePeriod, decimal price = FuturesPrice)
        => new(RsiSignalIdFor(timePeriod), price);

    public static GenerateFuturesRsiSignalCommand RsiGenerateCommand
        => RsiGenerateCommandFor(TradeTimePeriodType.Daily);

    /// <summary>
    /// Builds a <see cref="FuturesRsiSignalGeneratedEvent"/> that can be applied directly to a
    /// FuturesRsiSignalCommandState (via ReplayEvents) to seed prior RSI signal history for testing
    /// state transitions, without invoking the command handler.
    /// </summary>
    public static FuturesRsiSignalGeneratedEvent CreateRsiHistoryEvent(FuturesRsiSignalReadModel signal)
        => new()
        {
            EntityId = signal.EntityId,
            FuturesRsiSignal = signal,
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    public static FuturesMacdSignalEntityId MacdEntityIdFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, 14);

    public static FuturesMacdSignalId MacdSignalIdFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, 14, TimeOnly.FromDateTime(Timestamp));

    public static GenerateFuturesMacdSignalCommand MacdGenerateCommandFor(TradeTimePeriodType timePeriod)
        => new(MacdSignalIdFor(timePeriod), FuturesPrice);

    public static GenerateFuturesMacdSignalCommand MacdGenerateCommand
        => MacdGenerateCommandFor(TradeTimePeriodType.Daily);

    /// <summary>
    /// Builds a <see cref="FuturesMacdSignalGeneratedEvent"/> that can be applied directly to a
    /// FuturesMacdSignalCommandState to seed prior MACD signal history for testing state transitions.
    /// </summary>
    public static FuturesMacdSignalGeneratedEvent CreateMacdHistoryEvent(
        decimal price,
        TradeTimePeriodType timePeriod,
        double macdLine = 0,
        double signalLine = 0,
        double histogram = 0,
        FuturesTrendDirectionType direction = FuturesTrendDirectionType.Init,
        FuturesTrendDirectionStrengthType strength = FuturesTrendDirectionStrengthType.Low)
        => new()
        {
            EntityId = MacdEntityIdFor(timePeriod),
            FuturesMacdSignal = new FuturesMacdSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: timePeriod,
                periodLength: 14,
                timestamp: TimeOnly.FromDateTime(Timestamp),
                futuresPrice: price,
                macdLine: macdLine,
                signalLine: signalLine,
                histogram: histogram,
                macd: direction,
                macdStrength: strength),
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    /// <summary>
    /// Steadily rising price series (30 points) whose fast EMA rises faster than the slow EMA,
    /// producing a positive MACD line/histogram (up-trending directional bias).
    /// </summary>
    public static decimal[] MacdRisingPrices
        => [.. Enumerable.Range(0, 30).Select(i => 4000m + (i * 15m))];

    /// <summary>
    /// Steadily falling price series (30 points) whose fast EMA falls faster than the slow EMA,
    /// producing a negative MACD line/histogram (down-trending directional bias).
    /// </summary>
    public static decimal[] MacdFallingPrices
        => [.. Enumerable.Range(0, 30).Select(i => 4900m - (i * 15m))];

    /// <summary>
    /// Flat price series (30 points, all identical) that yields a MACD line/histogram at (or near) zero
    /// (a range-bound / flat directional bias).
    /// </summary>
    public static decimal[] MacdFlatPrices
        => [.. Enumerable.Repeat(4500m, 30)];

    /// <summary>
    /// A single-element price series, too short for FuturesMacdSignalCompute to derive a meaningful
    /// MACD line from (fast/slow EMA collapse to the single seed value).
    /// </summary>
    public static decimal[] MacdSinglePrice => [4500m];

    // ─── TDI Signal sample data

    public static FuturesTdiSignalId FuturesTdiSignalId => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timestamp: new TimeOnly(18, 50, 10, 451)
    );

    /// <summary>
    /// Rising RSI series – RSI >= 50 with positive slope → UpTrending TDI model direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] TdiUpTrendingRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4000m + (i * 5m),
            priceChange: 5m,
            priceGain: 5m,
            priceLoss: 0m,
            averagePriceGain: 5m,
            averagePriceLoss: 0m,
            rs: 100,
            rsi: 65,
            rsiAverage: 60,
            rsiSlope: 1.0))
        .ToArray();

    /// <summary>
    /// Falling RSI series – RSI &lt; 50 with negative slope → DownTrending TDI model direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] TdiDownTrendingRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4200m - (i * 5m),
            priceChange: -5m,
            priceGain: 0m,
            priceLoss: 5m,
            averagePriceGain: 0m,
            averagePriceLoss: 5m,
            rs: 0,
            rsi: 30,
            rsiAverage: 35,
            rsiSlope: -1.0))
        .ToArray();

    /// <summary>
    /// Flat RSI series – RSI = 50 with zero slope → RangeBound TDI model direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] TdiFlatRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4100m,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 1.0,
            rsi: 50,
            rsiAverage: 50,
            rsiSlope: 0))
        .ToArray();

    /// <summary>
    /// Minimal single-element RSI signal array for TDI edge-case testing.
    /// </summary>
    public static FuturesRsiSignalReadModel[] TdiSingleRsiSignal => [
        new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(18, 50, 10, 451),
            price: 4022,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 1.0,
            rsi: 50,
            rsiAverage: 50,
            rsiSlope: 0)
    ];

    /// <summary>
    /// RSI &lt; 50 with positive slope → RangeBound TDI model direction
    /// (neither UpTrending nor DownTrending since conditions require both RSI and slope to align).
    /// </summary>
    public static FuturesRsiSignalReadModel[] TdiRangeBoundRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4100m,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 1.0,
            rsi: 45,
            rsiAverage: 45,
            rsiSlope: 0.5))
        .ToArray();

    // ─── ATR Signal sample data ────────────────────────────────────────

    public static FuturesAtrSignalId FuturesAtrSignalItiId => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timePeriod: TradeTimePeriodType.Daily,
        periodLength: 14,
        timestamp: new TimeOnly(18, 50, 10, 451)
    );

    public static FuturesAtrSignalId FuturesAtrSignalIntraDayId => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timePeriod: TradeTimePeriodType.Daily,
        periodLength: 14,
        timestamp: new TimeOnly(18, 50, 10, 451)
    );

    /// <summary>
    /// Rising price ITI signals with increasing step sizes so that the last TrueRange exceeds the ATR
    /// → the ATR model produces an <see cref="FuturesTrendType.UpTrending"/> direction.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AtrUpTrendingItiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesItiSignalV2ReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = i,
            IntrinsicPrice = 5500.0 + (i * (5.0 + i * 0.5)),
            IntrinsicTime = Timestamp.AddSeconds(i * 15),
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
            Lambda = Lambda,
            TradeState = IntrinsicTimeTradeState.Ready
        })
        .ToArray();

    /// <summary>
    /// Price ITI signals with large initial moves tapering to a tiny final move so that the last
    /// TrueRange is well below the ATR → the ATR model produces a DownTrending direction.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AtrDownTrendingItiSignals
    {
        get
        {
            var prices = new double[30];
            prices[0] = 5500.0;
            for (int i = 1; i < 29; i++)
                prices[i] = prices[i - 1] - 10.0;
            prices[29] = prices[28] - 0.1;

            return Enumerable.Range(0, 30)
                .Select(i => new FuturesItiSignalV2ReadModel
                {
                    ContractId = ContractId,
                    ValueDate = ValueDate,
                    SequenceId = i,
                    IntrinsicPrice = prices[i],
                    IntrinsicTime = Timestamp.AddSeconds(i * 15),
                    IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend,
                    IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
                    Lambda = Lambda,
                    TradeState = IntrinsicTimeTradeState.Ready
                })
                .ToArray();
        }
    }

    /// <summary>
    /// Flat price ITI signals where all prices are identical → TR = 0, ATR = 0 → RangeBound.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AtrFlatItiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesItiSignalV2ReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = i,
            IntrinsicPrice = 5500.0,
            IntrinsicTime = Timestamp.AddSeconds(i * 15),
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
            Lambda = Lambda,
            TradeState = IntrinsicTimeTradeState.Ready
        })
        .ToArray();

    /// <summary>
    /// Minimal single-element ITI signal array for ATR edge-case testing.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AtrSingleItiSignal => [
        new FuturesItiSignalV2ReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = 0,
            IntrinsicPrice = 5500.0,
            IntrinsicTime = Timestamp,
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
            Lambda = Lambda,
            TradeState = IntrinsicTimeTradeState.Ready
        }
    ];

    /// <summary>
    /// Rising close-price IntraDay data with increasing step sizes so that the last TrueRange exceeds the ATR
    /// → the ATR model produces an UpTrending direction.
    /// </summary>
    public static FuturesIntraDayDataReadModel[] AtrUpTrendingIntraDayData => Enumerable.Range(0, 30)
        .Select(i => new FuturesIntraDayDataReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = i,
            Symbol = Symbol,
            ClosePrice = 5500m + (i * (5m + i * 0.5m))
        })
        .ToArray();

    /// <summary>
    /// IntraDay data with large initial moves tapering to a tiny final move so that the last
    /// TrueRange is well below the ATR → the ATR model produces a DownTrending direction.
    /// </summary>
    public static FuturesIntraDayDataReadModel[] AtrDownTrendingIntraDayData
    {
        get
        {
            var prices = new decimal[30];
            prices[0] = 5500m;
            for (int i = 1; i < 29; i++)
                prices[i] = prices[i - 1] - 10m;
            prices[29] = prices[28] - 0.1m;

            return Enumerable.Range(0, 30)
                .Select(i => new FuturesIntraDayDataReadModel
                {
                    ContractId = ContractId,
                    ValueDate = ValueDate,
                    SequenceId = i,
                    Symbol = Symbol,
                    ClosePrice = prices[i]
                })
                .ToArray();
        }
    }

    /// <summary>
    /// Flat close-price IntraDay data where all prices are identical → TR = 0, ATR = 0 → RangeBound.
    /// </summary>
    public static FuturesIntraDayDataReadModel[] AtrFlatIntraDayData => Enumerable.Range(0, 30)
        .Select(i => new FuturesIntraDayDataReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = i,
            Symbol = Symbol,
            ClosePrice = 5500m
        })
        .ToArray();

    /// <summary>
    /// Minimal single-element IntraDay data array for ATR edge-case testing.
    /// </summary>
    public static FuturesIntraDayDataReadModel[] AtrSingleIntraDayData => [
        new FuturesIntraDayDataReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = 0,
            Symbol = Symbol,
            ClosePrice = 5500m
        }
    ];

    /// <summary>
    /// Rising price series with increasing true-range steps so that the last TR exceeds the ATR
    /// → the ATR model produces an <see cref="FuturesTrendType.UpTrending"/> direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] AtrUpTrendingRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4000m + (i * (5m + i * 0.5m)),
            priceChange: 5m + i * 0.5m,
            priceGain: 5m + i * 0.5m,
            priceLoss: 0m,
            averagePriceGain: 5m,
            averagePriceLoss: 0m,
            rs: 100,
            rsi: 100,
            rsiAverage: 100,
            rsiSlope: 1.0))
        .ToArray();

    /// <summary>
    /// Price series with large initial moves tapering to a tiny final move so that the last
    /// TrueRange is well below the ATR → the ATR model produces a
    /// <see cref="FuturesTrendType.DownTrending"/> direction (TrueRange &lt; AtrValue).
    /// </summary>
    public static FuturesRsiSignalReadModel[] AtrDownTrendingRsiSignals
    {
        get
        {
            // Build prices: large moves early (10 pts each) then the last move is only 0.1 pt.
            // This guarantees TR[last] << ATR.
            var prices = new decimal[30];
            prices[0] = 4200m;
            for (int i = 1; i < 29; i++)
                prices[i] = prices[i - 1] - 10m;
            prices[29] = prices[28] - 0.1m;

            return Enumerable.Range(0, 30)
                .Select(i => new FuturesRsiSignalReadModel(
                    contractId: ContractId,
                    valueDate: ValueDate,
                    timePeriod: TradeTimePeriodType.Daily,
                    periodLength: 14,
                    timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
                    price: prices[i],
                    priceChange: i == 0 ? 0m : prices[i] - prices[i - 1],
                    priceGain: 0m,
                    priceLoss: i == 0 ? 0m : Math.Abs(prices[i] - prices[i - 1]),
                    averagePriceGain: 0m,
                    averagePriceLoss: 5m,
                    rs: 0,
                    rsi: 0,
                    rsiAverage: 0,
                    rsiSlope: -1.0))
                .ToArray();
        }
    }

    /// <summary>
    /// Flat price series where all prices are identical → TR = 0, ATR = 0 → RangeBound model direction.
    /// </summary>
    public static FuturesRsiSignalReadModel[] AtrFlatRsiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(9, 30, 0).Add(TimeSpan.FromMinutes(i)),
            price: 4100m,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 1.0,
            rsi: 50,
            rsiAverage: 50,
            rsiSlope: 0))
        .ToArray();

    /// <summary>
    /// Minimal single-element RSI signal array for ATR edge-case testing.
    /// </summary>
    public static FuturesRsiSignalReadModel[] AtrSingleRsiSignal => [
        new FuturesRsiSignalReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            periodLength: 14,
            timestamp: new TimeOnly(18, 50, 10, 451),
            price: 4022,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 1.0,
            rsi: 50,
            rsiAverage: 50,
            rsiSlope: 0)
    ];

    // ─── ADX Signal sample data ───────────────────────────────────────

    public static FuturesAdxSignalId FuturesAdxSignalId => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timePeriod: TradeTimePeriodType.Daily,
        periodLength: 14,
        timestamp: new TimeOnly(18, 50, 10, 451)
    );

    /// <summary>
    /// Rising price ITI signals with increasing step sizes so that +DI exceeds -DI
    /// → the ADX model produces an <see cref="FuturesTrendType.UpTrending"/> direction.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AdxUpTrendingItiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesItiSignalV2ReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = i,
            IntrinsicPrice = 5500.0 + (i * (5.0 + i * 0.5)),
            IntrinsicTime = Timestamp.AddSeconds(i * 15),
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
            Lambda = Lambda,
            TradeState = IntrinsicTimeTradeState.Ready
        })
        .ToArray();

    /// <summary>
    /// Falling price ITI signals so that -DI exceeds +DI
    /// → the ADX model produces a <see cref="FuturesTrendType.DownTrending"/> direction.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AdxDownTrendingItiSignals
    {
        get
        {
            var prices = new double[30];
            prices[0] = 5500.0;
            for (int i = 1; i < 30; i++)
                prices[i] = prices[i - 1] - (5.0 + i * 0.5);

            return Enumerable.Range(0, 30)
                .Select(i => new FuturesItiSignalV2ReadModel
                {
                    ContractId = ContractId,
                    ValueDate = ValueDate,
                    SequenceId = i,
                    IntrinsicPrice = prices[i],
                    IntrinsicTime = Timestamp.AddSeconds(i * 15),
                    IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend,
                    IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
                    Lambda = Lambda,
                    TradeState = IntrinsicTimeTradeState.Ready
                })
                .ToArray();
        }
    }

    /// <summary>
    /// Flat price ITI signals where all prices are identical → +DI = -DI = 0 → RangeBound.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AdxFlatItiSignals => Enumerable.Range(0, 30)
        .Select(i => new FuturesItiSignalV2ReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = i,
            IntrinsicPrice = 5500.0,
            IntrinsicTime = Timestamp.AddSeconds(i * 15),
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
            Lambda = Lambda,
            TradeState = IntrinsicTimeTradeState.Ready
        })
        .ToArray();

    /// <summary>
    /// Minimal single-element ITI signal array for ADX edge-case testing.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AdxSingleItiSignal => [
        new FuturesItiSignalV2ReadModel
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            SequenceId = 0,
            IntrinsicPrice = 5500.0,
            IntrinsicTime = Timestamp,
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
            Lambda = Lambda,
            TradeState = IntrinsicTimeTradeState.Ready
        }
    ];

    // ─── Trade Signal sample data ──────────────────────────────────────

    public static FuturesTradeSignalEntityId TradeSignalEntityId
        => new(ContractId, ValueDate, TradeTimePeriodType.FifteenSeconds);

    /// <summary>
    /// Standard EOD data used as core input for trade signal tests.
    /// </summary>
    public static FuturesEodDataV2ReadModel TradeSignalEodData => new(
        contractId: ContractId,
        valueDate: ValueDate,
        symbol: "ES",
        openPrice: 5480m,
        highPrice: 5520m,
        lowPrice: 5470m,
        closePrice: 5500m,
        volume: 100000,
        dailyPercentChange: 0.5,
        dailyStdDev: 20.0,
        dailyStdDevAmount: 110.0,
        upperBand: 5600.0,
        mean: 5480.0,
        lowerBand: 5360.0,
        marketDirection: MarketDirectionType.Up,
        marketDirectionIndicator: 65.0,
        windowSize: 20,
        fiftyDMA: 5450.0m,
        twoHundredDMA: 5300.0m);

    /// <summary>
    /// EOD data with a different close price for change-detection tests.
    /// </summary>
    public static FuturesEodDataV2ReadModel TradeSignalEodDataChanged => TradeSignalEodData with
    {
        ClosePrice = 5550m,
        DailyPercentChange = 1.0
    };

    /// <summary>
    /// RSI signal used in trade signal tests.
    /// </summary>
    public static FuturesRsiSignalReadModel TradeSignalRsiSignal => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timePeriod: TradeTimePeriodType.FifteenSeconds,
        periodLength: 14,
        timestamp: new TimeOnly(10, 0, 0),
        price: 5500m,
        priceChange: 5m,
        priceGain: 5m,
        priceLoss: 0m,
        averagePriceGain: 5m,
        averagePriceLoss: 1m,
        rs: 5.0,
        rsi: 65.0,
        rsiAverage: 60.0,
        rsiSlope: 0.5);

    /// <summary>
    /// TDI signal with UpTrending direction for trade signal tests.
    /// </summary>
    public static FuturesTdiSignalReadModel TradeSignalTdiSignalUpTrending => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timePeriod: TradeTimePeriodType.FifteenSeconds,
        timestamp: new TimeOnly(10, 0, 0),
        upTrendCount: 5,
        downTrendCount: 2,
        tdi: FuturesTrendDirectionType.UpTrending,
        tdiStrength: FuturesTrendDirectionStrengthType.Medium);

    /// <summary>
    /// TDI signal with DownTrending direction for trade signal tests.
    /// </summary>
    public static FuturesTdiSignalReadModel TradeSignalTdiSignalDownTrending => new(
        contractId: ContractId,
        valueDate: ValueDate,
        timePeriod: TradeTimePeriodType.FifteenSeconds,
        timestamp: new TimeOnly(10, 0, 0),
        upTrendCount: 2,
        downTrendCount: 5,
        tdi: FuturesTrendDirectionType.DownTrending,
        tdiStrength: FuturesTrendDirectionStrengthType.Medium);

    /// <summary>
    /// ITI signal data with UpTrend direction change for trade signal tests.
    /// </summary>
    public static FuturesItiSignalDataReadModel TradeSignalItiDataUpTrend => new(
        trendDirectionChange: new FuturesItiSignalV2ReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            sequenceId: 1,
            intrinsicTime: Timestamp,
            intrinsicTimeGroupId: 1,
            intrinsicTimeLength: 15.0,
            intrinsicPrice: (double)FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: (double)FuturesPrice,
            trendExtreme: (double)FuturesPrice + 20.0,
            trendReversal: (double)FuturesPrice - 10.0,
            trendDelta: 20.0,
            targetDelta: (double)FuturesPrice * Lambda,
            lambda: Lambda,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: (double)FuturesPrice + 55.0,
            downTrendTrigger: (double)FuturesPrice - 55.0,
            tradeState: IntrinsicTimeTradeState.Ready),
        trendExtremeChange: new FuturesItiSignalV2ReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            sequenceId: 2,
            intrinsicTime: Timestamp.AddSeconds(15),
            intrinsicTimeGroupId: 1,
            intrinsicTimeLength: 15.0,
            intrinsicPrice: (double)FuturesPrice + 20.0,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendExtremeChanged,
            trendPrice: (double)FuturesPrice,
            trendExtreme: (double)FuturesPrice + 20.0,
            trendReversal: (double)FuturesPrice - 10.0,
            trendDelta: 20.0,
            targetDelta: (double)FuturesPrice * Lambda,
            lambda: Lambda,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: (double)FuturesPrice + 55.0,
            downTrendTrigger: (double)FuturesPrice - 55.0,
            tradeState: IntrinsicTimeTradeState.Ready),
        trendReversalChange: null);

    /// <summary>
    /// ITI signal data with DownTrend direction change for trade signal tests.
    /// </summary>
    public static FuturesItiSignalDataReadModel TradeSignalItiDataDownTrend => new(
        trendDirectionChange: new FuturesItiSignalV2ReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            sequenceId: 1,
            intrinsicTime: Timestamp,
            intrinsicTimeGroupId: 1,
            intrinsicTimeLength: 15.0,
            intrinsicPrice: (double)FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.DownTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: (double)FuturesPrice,
            trendExtreme: (double)FuturesPrice - 20.0,
            trendReversal: (double)FuturesPrice + 10.0,
            trendDelta: -20.0,
            targetDelta: (double)FuturesPrice * Lambda,
            lambda: Lambda,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: (double)FuturesPrice + 55.0,
            downTrendTrigger: (double)FuturesPrice - 55.0,
            tradeState: IntrinsicTimeTradeState.Ready),
        trendExtremeChange: new FuturesItiSignalV2ReadModel(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            sequenceId: 2,
            intrinsicTime: Timestamp.AddSeconds(15),
            intrinsicTimeGroupId: 1,
            intrinsicTimeLength: 15.0,
            intrinsicPrice: (double)FuturesPrice - 20.0,
            intrinsicTimeTrend: IntrinsicTimeTrendType.DownTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendExtremeChanged,
            trendPrice: (double)FuturesPrice,
            trendExtreme: (double)FuturesPrice - 20.0,
            trendReversal: (double)FuturesPrice + 10.0,
            trendDelta: -20.0,
            targetDelta: (double)FuturesPrice * Lambda,
            lambda: Lambda,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: (double)FuturesPrice + 55.0,
            downTrendTrigger: (double)FuturesPrice - 55.0,
            tradeState: IntrinsicTimeTradeState.Ready),
        trendReversalChange: null);

    /// <summary>
    /// Empty ITI signal data (all nulls) for RangeBound / minimal tests.
    /// </summary>
    public static FuturesItiSignalDataReadModel TradeSignalItiDataEmpty => new(
        trendDirectionChange: null,
        trendExtremeChange: null,
        trendReversalChange: null);

    // ───── ADX Signal sample data ──────────────────────────────────────────

    public static FuturesAdxSignalEntityId AdxEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public static FuturesAdxSignalId AdxSignalId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength, TimeOnly.FromDateTime(Timestamp));

    public static GenerateFuturesAdxSignalCommand AdxGenerateCommand
        => new(AdxSignalId, FuturesPrice);

    /// <summary>
    /// Builds a <see cref="FuturesAdxSignalGeneratedEvent"/> that can be applied directly to a
    /// FuturesAdxSignalCommandState to seed prior ADX signal history for testing state transitions.
    /// </summary>
    public static FuturesAdxSignalGeneratedEvent CreateAdxHistoryEvent(
        decimal price,
        double plusDI = 0,
        double minusDI = 0,
        double adxValue = 0,
        FuturesTrendDirectionType direction = FuturesTrendDirectionType.Init,
        FuturesTrendDirectionStrengthType strength = FuturesTrendDirectionStrengthType.Low)
        => new()
        {
            EntityId = AdxEntityId,
            FuturesAdxSignal = new FuturesAdxSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: TimeOnly.FromDateTime(Timestamp),
                futuresPrice: price,
                plusDI: plusDI,
                minusDI: minusDI,
                adxValue: adxValue,
                adx: direction,
                adxStrength: strength),
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    /// <summary>
    /// Steadily rising price series (16 points) that, once fed through FuturesAdxSignalCompute,
    /// yields PlusDI &gt; MinusDI (an up-trending directional bias).
    /// </summary>
    public static decimal[] AdxRisingPrices
        => [.. Enumerable.Range(0, 16).Select(i => 5500m + (i * 5m))];

    /// <summary>
    /// Steadily falling price series (16 points) that, once fed through FuturesAdxSignalCompute,
    /// yields MinusDI &gt; PlusDI (a down-trending directional bias).
    /// </summary>
    public static decimal[] AdxFallingPrices
        => [.. Enumerable.Range(0, 16).Select(i => 5500m - (i * 5m))];

    /// <summary>
    /// Flat price series (16 points, all identical) that yields PlusDI == MinusDI == 0
    /// (a range-bound / no-trend directional bias).
    /// </summary>
    public static decimal[] AdxFlatPrices
        => [.. Enumerable.Repeat(5500m, 16)];

    /// <summary>
    /// A single-element price series, which is too short for FuturesAdxSignalCompute to derive
    /// directional indicators from (returns PlusDI == MinusDI == AdxValue == 0).
    /// </summary>
    public static decimal[] AdxSinglePrice => [5500m];

    // ───── ATR Signal sample data ──────────────────────────────────────────

    public static FuturesAtrSignalEntityId AtrEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public static FuturesAtrSignalId AtrSignalId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength, TimeOnly.FromDateTime(Timestamp));

    public static GenerateFuturesAtrSignalCommand AtrGenerateCommand
        => new(AtrSignalId, FuturesPrice);

    public static GenerateFuturesAtrDailySignalCommand AtrGenerateDailyCommand
        => new(AtrSignalId, FuturesPrice);

    /// <summary>
    /// Builds a <see cref="FuturesAtrSignalGeneratedEvent"/> that can be applied directly to a
    /// FuturesAtrSignalCommandState to seed prior ATR signal history for testing state transitions.
    /// </summary>
    public static FuturesAtrSignalGeneratedEvent CreateAtrHistoryEvent(
        decimal price,
        double atrValue = 0,
        double trueRange = 0,
        FuturesTrendDirectionType direction = FuturesTrendDirectionType.Init,
        FuturesTrendDirectionStrengthType strength = FuturesTrendDirectionStrengthType.Low)
        => new()
        {
            EntityId = AtrEntityId,
            FuturesAtrSignal = new FuturesAtrSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: TimeOnly.FromDateTime(Timestamp),
                futuresPrice: price,
                atrValue: atrValue,
                trueRange: trueRange,
                atr: direction,
                atrStrength: strength),
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    /// <summary>
    /// Builds a <see cref="FuturesAtrDailySignalGeneratedEvent"/> that can be applied directly to a
    /// FuturesAtrSignalCommandState to seed prior ATR signal history for testing state transitions.
    /// </summary>
    public static FuturesAtrDailySignalGeneratedEvent CreateAtrDailyHistoryEvent(
        decimal price,
        double atrValue = 0,
        double trueRange = 0,
        FuturesTrendDirectionType direction = FuturesTrendDirectionType.Init,
        FuturesTrendDirectionStrengthType strength = FuturesTrendDirectionStrengthType.Low)
        => new()
        {
            EntityId = AtrSignalId.ToDailyEntityId(),
            FuturesAtrSignal = new FuturesAtrSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: TimeOnly.FromDateTime(Timestamp),
                futuresPrice: price,
                atrValue: atrValue,
                trueRange: trueRange,
                atr: direction,
                atrStrength: strength),
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    /// <summary>
    /// A price series (17 points) whose last step is a large jump (+50) after many small (+1) steps,
    /// so the final True Range far exceeds the smoothed Average True Range (an up-trending / rising
    /// volatility bias, per FuturesAtrSignalCompute.TrendDirection).
    /// </summary>
    public static decimal[] AtrRisingPrices
        => [.. Enumerable.Range(0, 16).Select(i => 5500m + i), 5565m];

    /// <summary>
    /// A price series (17 points) that oscillates by a large amount (+/-50) for most steps, then
    /// settles with a small final step (+1), so the final True Range falls far below the smoothed
    /// Average True Range (a down-trending / falling volatility bias).
    /// </summary>
    public static decimal[] AtrFallingPrices
        => [.. Enumerable.Range(0, 16).Select(i => 5500m + (i % 2 == 0 ? 0m : 50m)), 5551m];

    /// <summary>
    /// Flat price series (17 points, all identical) that yields TrueRange == AtrValue == 0
    /// (a range-bound / no volatility-trend bias).
    /// </summary>
    public static decimal[] AtrFlatPrices
        => [.. Enumerable.Repeat(5500m, 17)];

    /// <summary>
    /// A single-element price series, which is too short for FuturesAtrSignalCompute to derive
    /// True Range / ATR from (returns TrueRange == AtrValue == 0).
    /// </summary>
    public static decimal[] AtrSinglePrice => [5500m];
}
