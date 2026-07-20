using MathNet.Numerics.Distributions;
using Microsoft.Extensions.Logging;
using Pipelines.Sockets.Unofficial.Arenas;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;

/// <summary>
/// market data database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
/// <param name="logger"></param>
public class MarketDataDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    ISequenceIdGenerator sequenceIdGenerator,
   ILogger<DbProvider> logger)
    : ObjectDataRepository<MarketDataDbContext>(connectionSettings[MarketDataDbConnection], logger), IMarketDataDbContext
{
    public const string MarketDataDbConnection = "MarketDataDbConnection";
    readonly static Dictionary<TradingDaysKey, int> _tradingDaysMap = [];
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    static NormalCurveTableReadModel? _normalCurveTable;

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override MarketDataDbContext Database => this;

    // InsertFuturesItiSignalAsync
    public IMarketDataDbReadContext DbReader => this;
    public IMarketDataDbWriteContext DbWriter => this;

    static long MapToNextTickId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static FuturesDataId MapToFuturesDataId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1)
        );

    static FuturesTickDataV2ReadModel MapToFuturesTickData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            tickId: e.GetLong(2),
            tickTime: e.GetTimeOnly(3),
            price: e.GetDecimal(4),
            size: e.GetInt(5)
        );

    static FuturesTickDataId MapToFuturesTickDataId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            ContractId: e.GetString(0),
            ValueDate: e.GetDateOnly(1),
            TickId: e.GetLong(2)
        );

    static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            tickId: e.GetLong(2),
            tickTime: e.GetTimeOnly(3),
            optionPrice: e.GetDouble(4),
            bidPrice: e.GetDouble(5),
            askPrice: e.GetDouble(6),
            bidSize: e.GetInt(7),
            askSize: e.GetInt(8),
            impliedVolatility: e.GetDouble(9),
            underlyingPrice: e.GetDouble(10),
            delta: e.GetDouble(11),
            gamma: e.GetDouble(12),
            vega: e.GetDouble(13),
            theta: e.GetDouble(14),
            rho: e.GetDouble(15)
        );

    static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickPriceData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
    => new(
        contractId: e.GetString(0),
        valueDate: e.GetDateOnly(1),
        tickId: e.GetLong(2),
        tickTime: e.GetTimeOnly(3),
        optionPrice: e.GetDouble(4),
        bidPrice: e.GetDouble(5),
        askPrice: e.GetDouble(6),
        bidSize: e.GetInt(7),
        askSize: e.GetInt(8),
        impliedVolatility: e.GetDouble(9),
        underlyingPrice: e.GetDouble(10),
        delta: e.GetDouble(11),
        gamma: e.GetDouble(12),
        vega: e.GetDouble(13),
        theta: e.GetDouble(14),
        rho: e.GetDouble(15)
    );

    static FuturesOptionTickDataId MapToFuturesOptionTickDataId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            ContractId: e.GetString(0),
            ValueDate: e.GetDateOnly(1),
            TickId: e.GetLong(2)
        );

    static FuturesBarDataReadModel MapToFuturesBarData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            symbol: e.GetString(1),
            valueDate: e.GetDateOnly(2),
            barDate: e.GetDateTime(3),
            barRateType: e.GetEnum<BarRateType>(4),
            barValue: e.GetDecimal(5),
            upTrendTrigger: e.GetDouble(6),
            downTrendTrigger: e.GetDouble(7)
        );

    static long MapToFuturesBarDataCount<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static FuturesClosingPriceReadModel MapToFuturesClosingPrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            closingPrice: e.GetDecimal(2),
            createdOn: e.GetDateTime(3),
            createdBy: e.GetString(4)
        );

    static FuturesEodDataV2ReadModel MapToFuturesEodData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            symbol: e.GetString(2),
            openPrice: e.GetDecimal(3),
            highPrice: e.GetDecimal(4),
            lowPrice: e.GetDecimal(5),
            closePrice: e.GetDecimal(6),
            volume: e.GetInt(7),
            dailyPercentChange: e.GetDouble(8),
            dailyStdDev: e.GetDouble(9),
            dailyStdDevAmount: e.GetDouble(10),
            upperBand: e.GetDouble(11),
            mean: e.GetDouble(12),
            lowerBand: e.GetDouble(13),
            marketDirection: e.GetEnum<MarketDirectionType>(14),
            marketVolatility: e.GetEnum<MarketVolatilityType>(15),
            priceDirection: e.GetEnum<PriceDirectionType>(16),
            priceVolatility: e.GetEnum<PriceVolatilityType>(17),
            marketDirectionIndicator: e.GetInt(18),
            windowSize: e.GetInt(19)
        );

    static FuturesIntraDayDataReadModel MapToFuturesIntraDayData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            sequenceId: e.GetLong(2),
            symbol: e.GetString(3),
            openPrice: e.GetDecimal(4),
            highPrice: e.GetDecimal(5),
            lowPrice: e.GetDecimal(6),
            closePrice: e.GetDecimal(7),
            volume: e.GetInt(8),
            dailyPercentChange: e.GetDouble(9),
            dailyStdDev: e.GetDouble(10),
            dailyStdDevAmount: e.GetDouble(11),
            upperBand: e.GetDouble(12),
            mean: e.GetDouble(13),
            lowerBand: e.GetDouble(14),
            marketDirection: e.GetEnum<MarketDirectionType>(15),
            marketVolatility: e.GetEnum<MarketVolatilityType>(16),
            priceDirection: e.GetEnum<PriceDirectionType>(17),
            priceVolatility: e.GetEnum<PriceVolatilityType>(18),
            marketDirectionIndicator: e.GetInt(19),
            windowSize: e.GetInt(20)
         );

    static FuturesEodMovingAverageReadModel MapToFuturesEodMovingAverage<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            movingAverage: e.GetDouble(1)
        );

    static FuturesEodClosingPriceReadModel MapToFuturesEodClosingPrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            closingPrice: e.GetDecimal(2)
        );

    static FuturesTickHLVDataReadModel MapToFuturesTickHLVData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            ContractId: e.GetString(0),
            ValueDate: e.GetDateOnly(1),
            HighPrice: e.GetDecimal(2),
            LowPrice: e.GetDecimal(3),
            Volume: e.GetInt(4)
        );

    static FuturesEodDataIndexReadModel MapToFuturesEodDataIndex<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            ValueDate: e.GetDateOnly(0),
            ContractId: e.GetString(1)
        );

    static FuturesItiSignalV2ReadModel MapToFuturesItiSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            sequenceId: e.GetLong(3),
            intrinsicTime: e.GetDateTime(4),
            intrinsicTimeGroupId: e.GetInt(5),
            intrinsicTimeLength: e.GetDouble(6),
            intrinsicPrice: e.GetDouble(7),
            intrinsicTimeTrend: e.GetEnum<IntrinsicTimeTrendType>(8),
            intrinsicTimeMode: e.GetEnum<IntrinsicTimeModeType>(9),
            trendPrice: e.GetDouble(10),
            trendExtreme: e.GetDouble(11),
            trendReversal: e.GetDouble(12),
            trendDelta: e.GetDouble(13),
            targetDelta: e.GetDouble(14),
            lambda: e.GetDouble(15),
            tradingDays: e.GetInt(16),
            threshold: e.GetDouble(17),
            upTrendTrigger: e.GetDouble(18),
            downTrendTrigger: e.GetDouble(19),
            tradeState: e.GetEnum<IntrinsicTimeTradeState>(20)
        );

    static FuturesItiSignalMDIV2ReadModel MapToFuturesItiSignalMDI<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            intrinsicTime: e.GetDateTime(2),
            trendType: e.GetEnum<IntrinsicTimeTrendType>(3),
            mdi: e.GetDouble(4)
        );

    static DateOnly MapToMaxValueDate<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDateOnly(0);

    static int MapToMaxIntrinsicTimeGroupId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    static FuturesItiTrendDeltaDataReadModel MapToFuturesItiTrendDeltaData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timestamp: e.GetDateTime(2),
            sequenceId: e.GetLong(3),
            trendDelta: e.GetFloat(4),
            trendDirection: e.GetFloat(5),
            trendDirectionMode: e.GetInt(6),
            futuresPrice: e.GetFloat(7),
            trendExtreme: e.GetFloat(8),
            futuresRsi: e.GetFloat(9)
        );

    static FuturesItiTrendClassDataReadModel MapToFuturesItiTrendClassData(string e, int o)
        => new(
            symbol: e.GetString(ref o),
            valueDate: e.GetDateOnly(ref o),
            timestamp: e.GetDateTime(ref o),
            sequenceId: e.GetLong(ref o),
            trendClass: e.GetFloat(ref o),
            trendDirection: e.GetFloat(ref o),
            trendDirectionMode: e.GetInt(ref o),
            trendDelta: e.GetFloat(ref o),
            futuresRsi: e.GetFloat(ref o)
        );

    static FuturesItiTrendDeltaModelReadModel MapToFuturesItiTrendDeltaModel<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            startDate: e.GetDateOnly(2),
            endDate: e.GetDateOnly(3),
            count: e.GetInt(4),
            maximum: e.GetDouble(5),
            mean: e.GetDouble(6),
            median: e.GetDouble(7),
            minimum: e.GetDouble(8),
            skewness: e.GetDouble(9),
            stdDev: e.GetDouble(10),
            variance: e.GetDouble(11),
            meanAbsoluteError: e.GetDouble(12),
            meanSquaredError: e.GetDouble(13),
            rootMeanSquaredError: e.GetDouble(14),
            lossFunction: e.GetDouble(15),
            rSquared: e.GetDouble(16),
            modelData: e.GetBytes(17)
        );

    static FuturesItiTrendClassModelReadModel MapToFuturesItiTrendClassModel<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            startDate: e.GetDateOnly(2),
            endDate: e.GetDateOnly(3),
            count: e.GetInt(4),
            maximum: e.GetDouble(5),
            mean: e.GetDouble(6),
            median: e.GetDouble(7),
            minimum: e.GetDouble(8),
            skewness: e.GetDouble(9),
            stdDev: e.GetDouble(10),
            variance: e.GetDouble(11),
            accuracy: e.GetDouble(12),
            areaUnderPrecisionRecallCurve: e.GetDouble(13),
            areaUnderRocCurve: e.GetDouble(14),
            entropy: e.GetDouble(15),
            f1Score: e.GetDouble(16),
            modelData: e.GetBytes(17)
        );

    static int MapToRsiTrendCount<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    static long MapToMaxSequenceId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static FuturesTdiSignalReadModel MapToFuturesTdiSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            timestamp: e.GetTimeOnly(3),
            upTrendCount: e.GetInt(4),
            downTrendCount: e.GetInt(5),
            tdi: e.GetEnum<FuturesTrendDirectionType>(6),
            tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(7)
        );

    static FuturesMacdSignalReadModel MapToFuturesMacdSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            periodLength: e.GetInt(3),
            timestamp: e.GetTimeOnly(4),
            futuresPrice: e.GetDecimal(5),
            macdLine: e.GetDouble(6),
            signalLine: e.GetDouble(7),
            histogram: e.GetDouble(8),
            macd: e.GetEnum<FuturesTrendDirectionType>(9),
            macdStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(10)
        );

    static FuturesAtrSignalReadModel MapToFuturesAtrSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            periodLength:e.GetInt(3),
            timestamp: e.GetTimeOnly(4),
            futuresPrice: e.GetDecimal(5),
            atrValue: e.GetDouble(6),
            trueRange: e.GetDouble(7),
            atr: e.GetEnum<FuturesTrendDirectionType>(8),
            atrStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(9)
        );

    static FuturesAdxSignalReadModel MapToFuturesAdxSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            periodLength: e.GetInt(3),
            timestamp: e.GetTimeOnly(4),
            futuresPrice: e.GetDecimal(5),
            plusDI: e.GetDouble(6),
            minusDI: e.GetDouble(7),
            adxValue: e.GetDouble(8),
            adx: e.GetEnum<FuturesTrendDirectionType>(9),
            adxStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(10)
        );

    static FuturesTradeSignalV2ReadModel MapToFuturesTradeSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            sequenceId: e.GetLong(3),
            timestamp: e.GetTimeOnly(4),
            mean: e.GetDouble(5),
            stdDev: e.GetDouble(6),
            futuresPrice: e.GetDouble(7),
            priceChangePercent: e.GetDouble(8),
            fundRiskPercent: e.GetDouble(9),
            rsi: e.GetDouble(10),
            rsiSlope: e.GetDouble(11),
            trendType: e.GetEnum<FuturesTrendType>(12),
            trendStrength: e.GetEnum<FuturesTrendStrengthType>(13),
            tradeSignal: e.GetEnum<TradeSignalType>(14),
            tdi: e.GetEnum<FuturesTrendDirectionType>(15),
            tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(16),
            mdi: e.GetDouble(17),
            mdiTrend: e.GetEnum<FuturesMDITrendType>(18),
            mdiUpTrendLimit: e.GetDouble(19),
            mdiDownTrendLimit: e.GetDouble(20),
            upTrendingTrigger: e.GetDouble(21),
            downTrendingTrigger: e.GetDouble(22),
            entryTrigger: e.GetDouble(23),
            exitTrigger: e.GetDouble(24),
            trendDelta: e.GetDouble(25),
            trendExtreme: e.GetDouble(26),
            trendReversal: e.GetDouble(27),
            fiftyDMA: e.GetDecimal(28),
            twoHundredDMA: e.GetDecimal(29),
            tradeExecuteState: e.GetEnum<TradeExecuteState>(30)
        );

    static RateOfReturnReadModel MapToRateOfReturn<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            rateOfReturn: e.GetDouble(2)
        );

    static VixFuturesEodDataReadModel MapToVixFuturesEodData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            openPrice: e.GetDecimal(2),
            highPrice: e.GetDecimal(3),
            lowPrice: e.GetDecimal(4),
            closePrice: e.GetDecimal(5),
            volume: e.GetInt(6)
        );

    static long MapToMinTickId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static decimal MapToPrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDecimal(0);

    static YieldCurveRateReadModel MapToYieldCurveRate<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            valueDate: e.GetDateOnly(0),
            oneMonth: e.GetDouble(1),
            twoMonth: e.GetDouble(2),
            threeMonth: e.GetDouble(3),
            sixMonth: e.GetDouble(4),
            oneYear: e.GetDouble(5),
            twoYear: e.GetDouble(6),
            threeYear: e.GetDouble(7),
            fiveYear: e.GetDouble(8),
            sevenYear: e.GetDouble(9),
            tenYear: e.GetDouble(10),
            twentyYear: e.GetDouble(11),
            thirtyYear: e.GetDouble(12)
        );

    static MarketHolidayReadModel MapToMarketHoliday<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            currencyType: e.GetEnum<CurrencyType>(0),
            holidayDate: e.GetDateOnly(1),
            description: e.GetString(2)
        );

    static FuturesItiSignalAverageInfoDataModel MapToFuturesItiSignalAverageInfo<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            predictedDelta: e.GetDouble(2),
            futuresRSI: e.GetDouble(3)
        );

    static double MapToAveragePredictedDelta<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDouble(0);

    static NormalCurveDataReadModel MapToNormalCurveData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
    => new(
        stdDevIndex: e.GetInt(0),
        percent: e.GetDouble(1)
    );

    static FuturesTradeSignalId MapToFuturesTradeSignalId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            sequenceId: e.GetLong(3)
        );

    static FuturesRsiSignalReadModel MapToFuturesRsiSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TradeTimePeriodType>(2),
            periodLength: e.GetInt(3),
            timestamp: e.GetTimeOnly(4),
            price: e.GetDecimal(5),
            priceChange: e.GetDecimal(6),
            priceGain: e.GetDecimal(7),
            priceLoss: e.GetDecimal(8),
            averagePriceGain: e.GetDecimal(9),
            averagePriceLoss: e.GetDecimal(10),
            rs: e.GetDouble(11),
            rsi: e.GetDouble(12),
            rsiAverage: e.GetDouble(13),
            rsiSlope: e.GetDouble(14)
        );

    static FuturesContractV2ReadModel MapToFuturesContract(IObjectMapReader<FuturesContractV2ReadModel> o)
            => new(
               o.Get(e => e.ContractId),
               o.Get(e => e.Description),
               o.Get(e => e.Symbol),
               o.Get(e => e.LocalSymbol),
               o.Get(e => e.SecurityType),
               o.Get(e => e.Currency),
               o.Get(e => e.Exchange),
               o.Get(e => e.Multiplier),
               o.Get(e => e.LastTradeDate),
               o.Get(e => e.CurrentlyTraded));

    static TradeLiveFeedReadModel MapToTradeLiveFeed<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            tradeLiveFeedState: e.GetEnum<TradeLiveFeedStateType>(2)
        );

    /// <summary>
    /// Asynchronously creates the 'futures_trade_signal' table in the market data database if it does not already exist. 
    /// The table is designed to store trade signal data for futures contracts, including various technical indicators and trend information. 
    /// The primary key is a composite of contractId, valueDate, timePeriod, timestamp, and sequenceId, with clustering order specified for efficient querying by date and time. 
    /// Ensure that the database connection settings are properly configured before invoking this method.
    /// </summary>
    /// <returns></returns>
    public async Task CreateFuturesTradeSignalTableAsync()
        => await _dbFactory.MarketDataDb
                .Use("""
                    CREATE TABLE IF NOT EXISTS futures_trade_signal (
                    contractId text,
                    valueDate date,
                    timePeriod text,
                    timestamp time,
                    sequenceId bigint,
                    mean double,
                    stdDev double,
                    futuresPrice double,
                    priceChangePercent double,
                    fundRiskPercent double,
                    rsi double,
                    rsiSlope double,
                    trendType text,
                    trendStrength text,
                    tradeSignal text,
                    tdi text,
                    tdiStrength text,
                    mdi double,
                    mdiTrend text,
                    mdiUpTrendLimit double,
                    mdiDownTrendLimit double,
                    upTrendingTrigger double,
                    downTrendingTrigger double,
                    entryTrigger double,
                    exitTrigger double,
                    trendDelta double,
                    trendExtreme double,
                    trendReversal double,
                    fiftyDMA decimal,
                    twoHundredDMA decimal,
                    tradeExecuteState text,
                    PRIMARY KEY (contractId, valueDate, timePeriod, timestamp, sequenceId)
                ) WITH CLUSTERING ORDER BY (valueDate DESC, timePeriod DESC, timestamp DESC, sequenceId DESC);
                """)
                .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes the 'futures_trade_signal' table from the market data database if it exists. 
    /// This operation will remove all trade signal data for futures contracts stored in the table. 
    /// Use with caution, as this action is irreversible and will result in the loss of all data contained within the 'futures_trade_signal' table. 
    /// Ensure that you have backed up any necessary data before executing this method.
    /// </summary>
    /// <returns></returns>
    public async Task DeleteFuturesTradeSignalTableAsync()
        => await _dbFactory.MarketDataDb
                .Use("DROP TABLE IF EXISTS futures_trade_signal")
                .ExecuteCommandAsync();

    /// <summary>
    /// Deletes a futures bar data record from the database.
    /// </summary>
    /// <param name="e">The identifier of the futures bar data to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesBarDataAsync(FuturesBarDataId e)
        => await _dbFactory.MarketDataDb
                .Use(MarketDataDbCql.DeleteFuturesBarData)
                .SetParameters(new DeleteFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate))
                .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes the closing price entry for the specified futures contract on the given date.
    /// </summary>
    /// <remarks>If no closing price exists for the specified contract and date, no action is taken. Ensure
    /// that the contract identifier and date correspond to an existing entry to avoid unnecessary operations.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which to delete the closing price.</param>
    /// <param name="valueDate">The date of the closing price to delete. Must be a valid date.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesClosingPriceAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
                .Use(MarketDataDbCql.DeleteFuturesClosingPrice)
                .SetParameters(new DeleteFuturesClosingPrice(contractId, valueDate))
                .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures EOD data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task DeleteFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
                .Use(MarketDataDbCql.DeleteFuturesEodData)
                .SetParameters(new DeleteFuturesEodData(contractId, valueDate))
                .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes all futures tick data for the specified contract and value date from the market data
    /// database.
    /// </summary>
    /// <remarks>Ensure that the specified contract identifier and value date are valid before calling this
    /// method. This operation is irreversible and will remove all tick data for the given contract and date.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract whose tick data is to be deleted. Cannot be null or empty.</param>
    /// <param name="valueDate">The date for which the futures tick data should be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteFuturesTickData)
            .SetParameters(new DeleteFuturesTickData(contractId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes a trade live feed record from the database.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteTradeLiveFeed)
            .SetParameters(new DeleteTradeLiveFeed(orderId, tradeId))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures tick data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task DeleteVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
                .Use(MarketDataDbCql.DeleteVixFuturesEodData)
                .SetParameters(new DeleteVixFuturesEodData(contractId, valueDate))
                .ExecuteCommandAsync();

    /// <summary>
    /// Deletes yield curve rate data
    /// </summary>
    /// <param name="valueDate">The value date to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteYieldCurveRateAsync(DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteYieldCurveRate)
            .SetParameters(new DeleteYieldCurveRate(valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures ITI signal data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timePeriod"></param>
    /// <returns></returns>
    public async Task DeleteFuturesItiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteFuturesItiSignal)
            .SetParameters(new DeleteFuturesItiSignal(contractId, valueDate, timePeriod: timePeriod.ToStringFast()))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures option tick data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteFuturesOptionTickData)
            .SetParameters(new DeleteFuturesOptionTickData(contractId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes tick price data for a specified futures option contract on a given date.
    /// </summary>
    /// <remarks>This method removes all tick price data associated with the specified contract and date from
    /// the database. Ensure that the contract identifier and date are valid before calling this method.</remarks>
    /// <param name="contractId">The unique identifier of the futures option contract whose tick price data is to be deleted. Cannot be null or
    /// empty.</param>
    /// <param name="valueDate">The date for which the tick price data should be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteFuturesOptionTickPriceData)
            .SetParameters(new DeleteFuturesOptionTickPriceData(contractId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes a market holiday record from the database.
    /// </summary>
    /// <param name="e">The market holiday to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidayAsync(MarketHolidayReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteMarketHoliday)
            .SetParameters(new DeleteMarketHoliday(currencyType: e.CurrencyType.ToStringFast(), holidayDate: e.HolidayDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes market holiday records from the database for a given currency type within a specified date range.
    /// </summary>
    /// <param name="currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidaysAsync(CurrencyType currencyType)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteMarketHolidays)
            .SetParameters(new DeleteMarketHolidays(currencyType: currencyType.ToStringFast()))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures option quotes and their associated data from the database.
    /// </summary>
    /// <param name="quoteId"></param>
    /// <returns></returns>
    public async Task DeleteFuturesOptionQuotesAsync(int quoteId)
    {
        var db = _dbFactory.MarketDataDb;
        List<object> queuedCommands = [
             db.Use(MarketDataDbCql.DeleteFuturesOptionQuotes)
                .SetParameters(new DeleteFuturesOptionQuotes(quoteId))
                .QueueCommand(),
            db.Use(MarketDataDbCql.DeleteFuturesOptionQuoteData)
                .SetParameters(new DeleteFuturesOptionQuoteData(quoteId))
                .QueueCommand()
        ];
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    public async Task DeleteRateOfReturnAsync(string symbol, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteRateOfReturn)
            .SetParameters(new DeleteRateOfReturn(symbol, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// return next seed id by seed type
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<long> GetNextTickIdAsync(FuturesDataId e)
    {
        long nextTickId;
        var db = _dbFactory.MarketDataDb;
        try
        {
            await db.LockAsync();
            await db.Use(MarketDataDbCql.UpdateNextFuturesTickId)
               .SetParameters(new UpdateNextFuturesTickId(contractId: e.ContractId, valueDate: e.ValueDate))
               .ExecuteCommandAsync();

            nextTickId = await db.Use(MarketDataDbCql.GetNextFuturesTickId)
               .SetParameters(new GetNextFuturesTickId(contractId: e.ContractId, valueDate: e.ValueDate))
               .ExecuteScalarAsync(MapToNextTickId!);
        }
        finally
        {
            db.Unlock();
        }
        return nextTickId;
    }

    /// <summary>
    /// Gets the futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <param name="e">The identifier of the futures closing price to retrieve.</param>
    /// <returns>The <see cref="FuturesClosingPriceReadModel"/>.</returns>
    public async Task<FuturesClosingPriceReadModel?> GetFuturesClosingPriceAsync(FuturesDataId e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesClosingPrice)
            .SetParameters(new GetFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesClosingPrice!);

    /// <summary>
    /// Retrieves the closing price information
    /// </summary>
    /// <remarks>This method performs a synchronous call to an asynchronous database operation. Ensure that
    /// the provided <paramref name="e"/> contains valid identifiers to avoid unexpected results.</remarks>
    /// <param name="e">An object that identifies the futures contract and the value date for which the closing price is requested. Must
    /// contain valid contract and date values.</param>
    /// <returns>A <see cref="FuturesClosingPriceReadModel"/> containing the closing price details for the specified contract and
    /// date; or <see langword="null"/> if no data is found.</returns>
    public FuturesClosingPriceReadModel? GetFuturesClosingPrice(FuturesDataId e)
        => _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesClosingPrice)
            .SetParameters(new GetFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesClosingPrice!).Result;


    /// <summary>
    /// Gets yesterday's futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <param name="id">The identifier of the futures closing price to retrieve.</param>
    /// <returns>The <see cref="FuturesClosingPriceReadModel"/>.</returns>
    public async Task<FuturesClosingPriceReadModel?> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetYesterdaysFuturesClosingPrice)
            .SetParameters(new GetYesterdaysFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync<FuturesClosingPriceReadModel>(MapToFuturesClosingPrice!);

    /// <summary>
    /// get futures tick data   
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId e)
           => await _dbFactory.MarketDataDb
               .Use(MarketDataDbCql.GetFuturesTickData)
               .SetParameters(new GetFuturesTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
               .ExecuteSingleAsync(MapToFuturesTickData!);

    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
            => await _dbFactory.MarketDataDb
               .Use(MarketDataDbCql.GetLastFuturesTickData)
               .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
               .ExecuteSingleAsync(MapToFuturesTickData!);

    /// <summary>
	/// get last futures option tick data
	/// </summary>
	/// <param name="contractId"></param>
	/// <param name="tickDate"></param>
	/// <returns></returns>
	public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate)
            => await _dbFactory.MarketDataDb
               .Use(MarketDataDbCql.GetLastFuturesTickDataByTickTime)
               .SetParameters(new GetLastFuturesTickDataByTickTime(contractId,
                   valueDate: DateOnly.FromDateTime(tickDate),
                   tickTime: TimeOnly.FromDateTime(tickDate)))
               .ExecuteSingleAsync(MapToFuturesTickData!);

    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
            => await _dbFactory.MarketDataDb
               .Use(MarketDataDbCql.GetLastFuturesOptionTickData)
               .SetParameters(new GetLastFuturesOptionTickData(contractId, valueDate))
               .ExecuteSingleAsync(MapToFuturesOptionTickData!);

    /// <summary>
    /// Asynchronously retrieves the most recent tick price data for a specified futures option contract on a given
    /// date.
    /// </summary>
    /// <remarks>This method queries the market data database for the latest available tick price information
    /// for the given contract and date. Ensure that the contract identifier and date are valid to avoid
    /// exceptions.</remarks>
    /// <param name="contractId">The unique identifier of the futures option contract for which to retrieve tick price data. Cannot be null or
    /// empty.</param>
    /// <param name="valueDate">The date for which to retrieve the tick price data. Must be a valid date.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the last tick price data for the
    /// specified contract and date, or null if no data is found.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
           .Use(MarketDataDbCql.GetLastFuturesOptionTickPriceData)
           .SetParameters(new GetLastFuturesOptionTickPriceData(contractId, valueDate))
           .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!);
    /// <summary>
    /// get futures tick data id    
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate)
          => await _dbFactory.MarketDataDb
              .Use(MarketDataDbCql.GetLastFuturesTickData)
              .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
              .ExecuteSingleAsync(MapToFuturesTickDataId);

    /// <summary>
    /// Gets the futures bar data for a given contractId, symbol, valueDate, startDate, and endDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>A collection of <see cref="FuturesBarDataReadModel"/>.</returns>
    public async Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        var futuresBarData = await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesBarData)
            .SetParameters(new GetFuturesBarData(
                contractId,
                symbol,
                valueDate,
                startDate,
                endDate
            ))
            .ExecuteQueryAsync(MapToFuturesBarData!);
        return [.. futuresBarData.OrderBy(e => e.BarDate)];
    }

    /// <summary>
    /// gets all futures bar data.
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync()
    {
        var futuresBarData = await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesBarDataAll)
            .ExecuteQueryAsync(MapToFuturesBarData!);
        return futuresBarData;
    }

    /// <summary>
    /// gets the last futures bar data for a given contractId, symbol, and valueDate.
    /// </summary>
    /// <returns></returns>
    public async Task<FuturesBarDataReadModel> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var lastFuturesBarData = await db
            .Use(MarketDataDbCql.GetLastFuturesBarData)
            .SetParameters(new GetLastFuturesBarData(contractId, symbol, valueDate))
            .ExecuteSingleAsync(MapToFuturesBarData!);
        return lastFuturesBarData!;
    }


    /// <summary>
    /// Gets the count of futures bar data for a given FuturesBarDataId.
    /// </summary>
    /// <param name="e">The futures bar data identifier.</param>
    /// <returns>The count of futures bar data.</returns>
    public async Task<int> GetFuturesBarDataCountAsync(FuturesBarDataId e)
        => Convert.ToInt32(await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesBarDataCount)
            .SetParameters(new GetFuturesBarDataCount(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate))
            .ExecuteScalarAsync(MapToFuturesBarDataCount!));

    /// <summary>
    /// Gets a collection of futures ITI signals for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(FuturesItiSignalEntityId e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesItiSignals)
            .SetParameters(new GetFuturesItiSignals(contractId: e.ContractId, valueDate: e.ValueDate, timePeriod: e.TimePeriod.ToStringFast()))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(e => e.ContractId).ToList();
        return await db.Use(MarketDataDbCql.GetFuturesItiSignalsByDateRange)
            .SetParameters(new GetFuturesItiSignalsByDateRange(
                contractIds,
                startDate,
                endDate
            ))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);
    }

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        List<string> contractIds = [.. (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(e => e.ContractId)];
        var futuresItiSignals = await db.Use(MarketDataDbCql.GetFuturesItiSignalsByDateRange)
            .SetParameters(new GetFuturesItiSignalTrendDataByDateRange(
                contractIds,
                startDate,
                endDate,
                intrinsicTimeModes: GetIntrinsicTimeModes()
            ))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);
        return [.. futuresItiSignals.OrderBy(e => e.ValueDate).ThenBy(e => e.SequenceId)];
    }

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        List<string> contractIds = [.. (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(e => e.ContractId)];
        var futuresItiSignals = await db.Use(MarketDataDbCql.GetFuturesItiSignalsByDateRange)
            .SetParameters(new GetFuturesItiSignalTrendDataByDateRange(
                contractIds,
                startDate,
                endDate,
                intrinsicTimeModes: GetIntrinsicTimeModes()
            ))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);
        return [.. futuresItiSignals.OrderBy(e => e.ValueDate).ThenBy(e => e.SequenceId)];
    }

    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name="e">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(FuturesBarDataReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesBarData)
            .SetParameters(new InsertFuturesBarData(
                contractId: e.ContractId,
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                barDate: e.BarDate,
                barRateType: e.BarRateType.ToStringFast(),
                barValue: e.BarValue,
                upTrendTrigger: e.UpTrendTrigger,
                downTrendTrigger: e.DownTrendTrigger
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name="futuresBarData">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(ICollection<FuturesBarDataReadModel> futuresBarData)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesBarData)
            .SetParameters(futuresBarData.Select(e => new InsertFuturesBarData(
                contractId: e.ContractId,
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                barDate: e.BarDate,
                barRateType: e.BarRateType.ToStringFast(),
                barValue: e.BarValue,
                upTrendTrigger: e.UpTrendTrigger,
                downTrendTrigger: e.DownTrendTrigger
            )))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of futures bar data into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided futures bar data and inserts it into the database using a
    /// batch operation.  The <paramref name="futuresBarData"/> collection is enumerated to count the rows and prepare
    /// the data for insertion.</remarks>
    /// <param name="futuresBarData">A collection of <see cref="FuturesBarDataReadModel"/> objects representing the futures bar data to be inserted.
    /// Each object must contain valid values for contract ID, symbol, value date, bar date, bar rate type, bar value, 
    /// up trend trigger, and down trend trigger.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number  of
    /// rows processed during the insertion.</returns>
    public async Task<long> InsertFuturesBarDataAsync(IEnumerable<FuturesBarDataReadModel> futuresBarData)
    {
        long rowCount = 0;
        await _dbFactory.MarketDataDb
        .Use(MarketDataDbCql.InsertFuturesBarData)
        .SetParameters(GetFuturesBarData().Select(e => new InsertFuturesBarData(
            contractId: e.ContractId,
            symbol: e.Symbol,
            valueDate: e.ValueDate,
            barDate: e.BarDate,
            barRateType: e.BarRateType.ToStringFast(),
            barValue: e.BarValue,
            upTrendTrigger: e.UpTrendTrigger,
            downTrendTrigger: e.DownTrendTrigger
        )))
        .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FuturesBarDataReadModel> GetFuturesBarData()
        {
            foreach (var barData in futuresBarData)
            {
                rowCount++;
                yield return barData;
            }
        }
    }


    /// <summary>
    /// Inserts a collection of futures bar data records into the database. 
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesClosingPrice)
            .SetParameters(new InsertFuturesClosingPrice(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                closingPrice: e.ClosingPrice,
                createdOn: e.CreatedOn,
                createdBy: e.CreatedBy
            ))
            .ExecuteCommandAsync();



    /// <summary>
    /// 
    /// </summary>
    /// <param name="tickData"></param>
    /// <returns></returns>
    public async Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel e)
    {
        var tickId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTickData_TickId);
        await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesTickData)
            .SetParameters(new InsertFuturesTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId,
                tickTime: e.TickTime,
                price: e.Price,
                size: e.Size
            ))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// insert futures tick data collection
    /// </summary>
    /// <param name="tickData"></param>
    /// <returns></returns>
    public async Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> tickData)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesTickData)
            .SetParameters(tickData.Select(e => new InsertFuturesTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId: e.TickId,
                tickTime: e.TickTime,
                price: e.Price,
                size: e.Size
            )))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a new futures ITI signal record into the database.
    /// </summary>
    /// <param name="e">The futures ITI signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesItiSignalAsync(FuturesItiSignalV2ReadModel e)
    {
        var db = _dbFactory.MarketDataDb;
        List<object> dbCommands = [
            db.Use("""
                        INSERT INTO futures_iti_signal_index (
                            valueDate, 
                            contractId)
                        VALUES (
                            :valueDate, 
                            :contractId);
                    """)
                 .SetParameters(new {
                     valueDate = e.ValueDate,
                     contractId = e.ContractId
                 }).QueueCommand(),
             db.Use("""
                    INSERT INTO futures_iti_signal (
                        contractId, 
                        valueDate, 
                        timePeriod, 
                        sequenceId, 
                        intrinsicTime, 
                        intrinsicTimeGroupId,
                        intrinsicTimeLength,
                        intrinsicPrice, 
                        intrinsicTimeTrend, 
                        intrinsicTimeMode, 
                        trendPrice,
                        trendExtreme,
                        trendReversal, 
                        trendDelta,
                        targetDelta,
                        lambda, 
                        tradingDays,
                        threshold,
                        upTrendTrigger, 
                        downTrendTrigger, 
                        tradeState 
                    ) VALUES (
                        :contractId, 
                        :valueDate, 
                        :timePeriod, 
                        :sequenceId, 
                        :intrinsicTime, 
                        :intrinsicTimeGroupId, 
                        :intrinsicTimeLength, 
                        :intrinsicPrice, 
                        :intrinsicTimeTrend, 
                        :intrinsicTimeMode, 
                        :trendPrice, 
                        :trendExtreme, 
                        :trendReversal, 
                        :trendDelta,
                        :targetDelta, 
                        :lambda, 
                        :tradingDays, 
                        :threshold,
                        :upTrendTrigger,
                        :downTrendTrigger, 
                        :tradeState
                    );
                """)
                .SetParameters(new {
                    contractId = e.ContractId,
                    valueDate = e.ValueDate,
                    timePeriod = e.TimePeriod.ToStringFast(),
                    sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesItiSignal_SequenceId),
                    intrinsicTime = e.IntrinsicTime,
                    intrinsicTimeGroupId = e.IntrinsicTimeGroupId,
                    intrinsicTimeLength = e.IntrinsicTimeLength,
                    intrinsicPrice =  e.IntrinsicPrice,
                    intrinsicTimeTrend = e.IntrinsicTimeTrend.ToStringFast(),
                    intrinsicTimeMode = e.IntrinsicTimeMode.ToStringFast(),
                    trendPrice = e.TrendPrice,
                    trendExtreme = e.TrendExtreme,
                    trendReversal = e.TrendReversal,
                    trendDelta = e.TrendDelta,
                    targetDelta = e.TargetDelta,
                    lambda = e.Lambda,
                    tradingDays = e.TradingDays,
                    threshold = e.Threshold,
                    upTrendTrigger = e.UpTrendTrigger,
                    downTrendTrigger = e.DownTrendTrigger,
                    tradeState = e.TradeState.ToStringFast()
                }).QueueCommand() ];
        await db.ExecuteQueuedCommandsAsync(dbCommands);
    }

    /// <summary>
    /// Inserts a Futures RSI Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresRsiSignal">The Futures RSI Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesRsiSignal)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                timePeriod = e.TimePeriod.ToStringFast(),
                periodLength = e.PeriodLength,
                timestamp = e.Timestamp,
                price = e.Price,
                priceChange = e.PriceChange,
                priceGain = e.PriceGain,
                priceLoss = e.PriceLoss,
                averagePriceGain = e.AveragePriceGain,
                averagePriceLoss = e.AveragePriceLoss,
                rs = e.RS,
                rsi = e.RSI,
                rsiAverage = e.RSIAverage,
                rsiSlope = e.RSISlope
            })
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a single yield curve rate record into the database.
    /// </summary>
    /// <param name="e">The YieldCurveRateReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertYieldCurveRate)
            .SetParameters(new InsertYieldCurveRate(
                id: 1,
                valueDate: e.ValueDate,
                oneMonth: e.OneMonth,
                twoMonth: e.TwoMonth,
                threeMonth: e.ThreeMonth,
                sixMonth: e.SixMonth,
                oneYear: e.OneYear,
                twoYear: e.TwoYear,
                threeYear: e.ThreeYear,
                fiveYear: e.FiveYear,
                sevenYear: e.SevenYear,
                tenYear: e.TenYear,
                twentyYear: e.TwentyYear,
                thirtyYear: e.ThirtyYear
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of yield curve rate records into the database.
    /// </summary>
    /// <param name="e">The collection of YieldCurveRateReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertYieldCurveRatesAsync(ICollection<YieldCurveRateReadModel> e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertYieldCurveRate)
            .SetParameters(e.Select(x => new InsertYieldCurveRate(
                id: 1,
                valueDate: x.ValueDate,
                oneMonth: x.OneMonth,
                twoMonth: x.TwoMonth,
                threeMonth: x.ThreeMonth,
                sixMonth: x.SixMonth,
                oneYear: x.OneYear,
                twoYear: x.TwoYear,
                threeYear: x.ThreeYear,
                fiveYear: x.FiveYear,
                sevenYear: x.SevenYear,
                tenYear: x.TenYear,
                twentyYear: x.TwentyYear,
                thirtyYear: x.ThirtyYear
            )))
            .ExecuteCommandAsync();

    /// <summary>
    /// Gets the last FuturesOptionTickDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The last <see cref="FuturesOptionTickDataId"/>.</returns>
    public async Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesOptionTickDataId)
            .SetParameters(new GetLastFuturesOptionTickDataId(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesOptionTickDataId!);

    /// <summary>
    /// Gets the FuturesOptionTickDataV2ReadModel for a given FuturesOptionTickDataId.
    /// </summary>
    /// <param name="e">The futures option tick data identifier.</param>
    /// <returns>The <see cref="FuturesOptionTickDataV2ReadModel"/>.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId e)
         => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesOptionTickData)
            .SetParameters(new GetFuturesOptionTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
            .ExecuteSingleAsync(MapToFuturesOptionTickData!);

    /// <summary>
    /// Asynchronously retrieves the tick price data for a specified futures option.
    /// </summary>
    /// <remarks>This method queries the market data database for the latest tick price information associated
    /// with the provided identifier. Ensure that the identifier is valid to avoid unexpected results.</remarks>
    /// <param name="e">An identifier that specifies the futures option tick data to retrieve. This includes the contract ID, value
    /// date, and tick ID. Must represent a valid futures option tick.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the tick price data for the
    /// specified futures option, or null if no matching data is found.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickPriceDataAsync(FuturesOptionTickDataId e)
      => await _dbFactory.MarketDataDb
         .Use(MarketDataDbCql.GetFuturesOptionTickPriceData)
         .SetParameters(new GetFuturesOptionTickPriceData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
         .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!);

    /// <summary>
    /// Inserts a single FuturesOptionTickData into the database.
    /// </summary>
    /// <param name="e">The futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel e)
    {
        var tickId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesOptionTickData_TickId);
        await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesOptionTickData)
            .SetParameters(new InsertFuturesOptionTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId,
                tickTime: e.TickTime,
                optionPrice: e.OptionPrice,
                bidPrice: e.BidPrice,
                askPrice: e.AskPrice,
                bidSize: e.BidSize,
                askSize: e.AskSize,
                impliedVolatility: e.ImpliedVolatility,
                underlyingPrice: e.UnderlyingPrice,
                delta: e.Delta,
                gamma: e.Gamma,
                vega: e.Vega,
                theta: e.Theta,
                rho: e.Rho
            ))
            .ExecuteCommandAsync();
    }

    public async Task InsertFuturesOptionTickPriceDataAsync(FuturesOptionTickDataV2ReadModel e)
    {
        var tickId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesOptionTickPriceData_TickId);
        await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesOptionTickPriceData)
            .SetParameters(new InsertFuturesOptionTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId,
                tickTime: e.TickTime,
                optionPrice: e.OptionPrice,
                bidPrice: e.BidPrice,
                askPrice: e.AskPrice,
                bidSize: e.BidSize,
                askSize: e.AskSize,
                impliedVolatility: e.ImpliedVolatility,
                underlyingPrice: e.UnderlyingPrice,
                delta: e.Delta,
                gamma: e.Gamma,
                vega: e.Vega,
                theta: e.Theta,
                rho: e.Rho
            ))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a collection of FuturesOptionTickDataV2ReadModel into the database.
    /// </summary>
    /// <param name="tickData">The collection of futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> tickData)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesOptionTickData)
            .SetParameters(tickData.Select(e => new InsertFuturesOptionTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId: e.TickId,
                tickTime: e.TickTime,
                optionPrice: e.OptionPrice,
                bidPrice: e.BidPrice,
                askPrice: e.AskPrice,
                bidSize: e.BidSize,
                askSize: e.AskSize,
                impliedVolatility: e.ImpliedVolatility,
                underlyingPrice: e.UnderlyingPrice,
                delta: e.Delta,
                gamma: e.Gamma,
                vega: e.Vega,
                theta: e.Theta,
                rho: e.Rho
            )))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a futures option quote record into the database.
    /// </summary>
    /// <param name="futuresOptionQuotes">The futures option quote to insert.</param>
    /// <param name="futuresOptionQuoteData">Collection of futures option quote data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionQuoteAsync(
        ICollection<FuturesOptionQuoteReadModel> futuresOptionQuotes,
        ICollection<FuturesOptionQuoteDataReadModel> futuresOptionQuoteData)
    {
        var db = _dbFactory.MarketDataDb;
        List<object> queuedCommands = [];
        foreach (var e in futuresOptionQuotes)
        {
            queuedCommands.Add(
               db.Use(MarketDataDbCql.InsertFuturesOptionQuote)
                    .SetParameters(new InsertFuturesOptionQuote(
                        quoteId: e.QuoteId,
                        contractId: e.ContractId,
                        requestId: e.RequestId,
                        createdBy: e.CreatedBy,
                        createdOn: e.CreatedOn
                    ))
                    .QueueCommand());
        }
        foreach (var e in futuresOptionQuoteData)
        {
            var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionQuoteData_SequenceId);
            queuedCommands.Add(
            _dbFactory.MarketDataDb
                .Use(MarketDataDbCql.InsertFuturesOptionQuoteData)
                .SetParameters(new InsertFuturesOptionQuoteData(
                    quoteId: e.QuoteId,
                    contractId: e.ContractId,
                    requestId: e.RequestId,
                    sequenceId,
                    bidPrice: e.BidPrice,
                    bidSize: e.BidSize,
                    askPrice: e.AskPrice,
                    askSize: e.AskSize
                ))
                .QueueCommand());
        }

        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// Inserts a futures option quote data record into the database.
    /// </summary>
    /// <param name="futuresOptionQuoteData">Collection of  futures option quote data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionQuoteDataAsync(FuturesOptionQuoteDataReadModel e)
    {
        var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionQuoteData_SequenceId);
        await _dbFactory.MarketDataDb.Use(MarketDataDbCql.InsertFuturesOptionQuoteData)
            .SetParameters(new InsertFuturesOptionQuoteData(
                quoteId: e.QuoteId,
                contractId: e.ContractId,
                requestId: e.RequestId,
                sequenceId,
                bidPrice: e.BidPrice,
                bidSize: e.BidSize,
                askPrice: e.AskPrice,
                askSize: e.AskSize
            ))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a trade live feed record into the database.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertTradeLiveFeed)
            .SetParameters(new InsertTradeLiveFeed(orderId: e.OrderId, tradeId: e.TradeId, tradeLiveFeedState: e.TradeLiveFeedState.ToStringFast()))
            .ExecuteCommandAsync();

    /// <summary>
    /// Gets the FuturesDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesDataId.</returns>
    public async Task<FuturesDataId?> GetFuturesDataId(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesDataId)
            .SetParameters(new GetFuturesDataId(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesDataId); // Map the result to FuturesDataId

    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given FuturesDataId.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesTickHLVData)
            .SetParameters(new GetFuturesTickHLVData(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesTickHLVData!);

    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesTickHLVDataReadModel?> GetVixFuturesTickHLVDataAsync(VixFuturesEodDataEntityId e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesTickHLVData)
            .SetParameters(new GetFuturesTickHLVData(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesTickHLVData!);

    /// <summary>
    /// Gets the FuturesEodDataV2ReadModel for a given contractId and valueDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var futuresEodData = await db.Use(MarketDataDbCql.GetFuturesEodData)
           .SetParameters(new GetFuturesEodData(contractId, valueDate))
           .ExecuteSingleAsync(MapToFuturesEodData!);
        futuresEodData ??= await db.Use(MarketDataDbCql.GetYesterdaysFuturesEodData)
            .SetParameters(new GetYesterdaysFuturesEodData(valueDate))
            .ExecuteSingleAsync(MapToFuturesEodData!);
        return futuresEodData;
    }

    /// <summary>
    /// Asynchronously retrieves intra-day market data for a specified futures contract on a given date.
    /// </summary>
    /// <remarks>This method performs an asynchronous database query to fetch the requested data. Ensure that
    /// the provided contract identifier and date are valid to avoid exceptions.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which to retrieve intra-day data. This parameter cannot be
    /// null or empty.</param>
    /// <param name="valueDate">The date for which the intra-day market data is requested.</param>
    /// <returns>A collection of <see cref="FuturesIntraDayDataReadModel"/> objects representing the intra-day market data for
    /// the specified contract and date.</returns>
    public async Task<ICollection<FuturesIntraDayDataReadModel>> GetFuturesIntraDayDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesIntraDayData)
            .SetParameters(new GetFuturesIntraDayData(contractId, valueDate))
            .ExecuteQueryAsync(MapToFuturesIntraDayData!);

    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain the latest available futures data at
    /// the end of the trading day.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see
    /// cref="FuturesEodDataV2ReadModel"/> representing the latest end-of-day futures data, or <see langword="null"/> if
    /// no data is available.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesEodData)
            .SetParameters(new GetLastFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesEodData!);

    /// <summary>
    /// Asynchronously retrieves a collection of end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain all available end-of-day futures
    /// data.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a collection of  <see
    /// cref="FuturesEodDataV2ReadModel"/> objects representing the end-of-day futures data.</returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync()
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesEodDataAll)
            .ExecuteQueryAsync(MapToFuturesEodData!);

    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given contractId and date range.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesEodDataByDateRange)
            .SetParameters(new GetFuturesEodDataByDateRange(contractId, startDate, endDate))
            .ExecuteQueryAsync(MapToFuturesEodData!);

    /// <summary>
    /// Gets the current FuturesEodDataV2ReadModel for a given FuturesDataId.
    /// </summary>
    /// <param name="e">The FuturesDataId containing the contractId and valueDate.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetCurrentFuturesEodDataAsync(DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dataIndexes = await db.Use(MarketDataDbCql.GetCurrentFuturesEodDataIndex)
            .SetParameters(new GetCurrentFuturesEodDataIndex(valueDate))
            .ExecuteQueryAsync(MapToFuturesEodDataIndex!);

        var dataIndex = dataIndexes.OrderByDescending(e => e.ValueDate).FirstOrDefault();
        return dataIndex is not null
            ? await GetFuturesEodDataAsync(dataIndex.ContractId, dataIndex.ValueDate)
            : default;
    }

    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given date range.
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetCurrentFuturesEodDataByDateRangeAsync(DateOnly startDate, DateOnly endDate)
        => [.. (await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetCurrentFuturesEodDataByDateRange)
            .SetParameters(new GetCurrentFuturesEodDataByDateRange(startDate, endDate))
            .ExecuteQueryAsync(MapToFuturesEodData!)).OrderByDescending(o => o.ValueDate)];

    /// <summary>
    /// Gets the FuturesEodMovingAverageReadModel for a given symbol and date range.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodMovingAverageReadModel.</returns>
    public async Task<FuturesEodMovingAverageReadModel?> GetFuturesEodMovingAverageAsync(string symbol, DateTime startDate, DateTime endDate)
        => (await _dbFactory.MarketDataDb
                .Use(MarketDataDbCql.GetFuturesEodMovingAverages)
                .SetParameters(new GetFuturesEodMovingAverages(symbol, startDate, endDate))
                .ExecuteQueryAsync(MapToFuturesEodMovingAverage!)).GroupBy(e => e.Symbol)
                .Select(g => new FuturesEodMovingAverageReadModel
                (
                    symbol: g.Key,
                    movingAverage: g.Average(e => e.MovingAverage)
                ))
                .FirstOrDefault();

    /// <summary>
    /// Gets a collection of FuturesEodClosingPriceReadModel for a given symbol and date range, limited by maxDays.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="maxDays">The maximum number of days to retrieve.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of FuturesEodClosingPriceReadModel.</returns>
    public async Task<ICollection<FuturesEodClosingPriceReadModel>> GetFuturesEodClosingPricesAsync(string contractId, string symbol, DateOnly startDate, DateOnly endDate, int maxDays)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesEodClosingPrices)
            .SetParameters(new GetFuturesEodClosingPrices(
                contractId,
                symbol,
                startDate,
                endDate,
                maxDays
            ))
            .ExecuteQueryAsync(MapToFuturesEodClosingPrice);

    /// <summary>
    /// return futures iti trend delta data by date range
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<ICollection<FuturesItiTrendDeltaDataReadModel>> GetFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesItiTrendDeltaData)
            .SetParameters(new GetFuturesItiTrendDeltaData(
                symbol,
                startDate,
                endDate
            ))
            .ExecuteQueryAsync(MapToFuturesItiTrendDeltaData);

    /// <summary>
    /// return futures iti trend class data by date range
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiTrendClassDataReadModel>> GetFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesItiTrendClassData)
            .SetParameters(new GetFuturesItiTrendClassData(
                symbol,
                startDate,
                endDate
            ))
            .ExecuteQueryAsync(MapToFuturesItiTrendClassData);

    /// <summary>
    /// return futures iti trend delta model
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiTrendDeltaModelReadModel> GetFuturesItiTrendDeltaModelAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesItiTrendDeltaModelMaxValueDate)
            .SetParameters(new GetFuturesItiTrendDeltaModelMaxValueDate(
                symbol,
                valueDate
            ))
            .ExecuteScalarAsync(MapToMaxValueDate);

        return await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesItiTrendDeltaModel)
            .SetParameters(new GetFuturesItiTrendDeltaModel(
                symbol,
                valueDate: maxValueDate
            ))
            .ExecuteSingleAsync(MapToFuturesItiTrendDeltaModel!);
    }

    /// <summary>
    /// return futures iti trend class model
    /// </summary>
    /// <param name="symbol"></param>   
    /// <param name="valueDate"></param>
    public async Task<FuturesItiTrendClassModelReadModel> GetFuturesItiTrendClassModelAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await db.Use(MarketDataDbCql.GetFuturesItiTrendClassModelMaxValueDate)
            .SetParameters(new GetFuturesItiTrendClassModelMaxValueDate(symbol, valueDate))
            .ExecuteScalarAsync(MapToMaxValueDate!);

        return await db.Use(MarketDataDbCql.GetFuturesItiTrendClassModel)
            .SetParameters(new GetFuturesItiTrendClassModel(symbol, valueDate: maxValueDate))
            .ExecuteSingleAsync(MapToFuturesItiTrendClassModel!);
    }


    /// <summary>
    /// Inserts a new record into the futures_eod_data_index table if it does not already exist.
    /// </summary>
    /// <param name="e">The FuturesEodDataIndexReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesEodDataIndexAsync(FuturesEodDataIndexReadModel e)
    => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesEodDataIndex)
            .SetParameters(new InsertFuturesEodDataIndex(valueDate: e.ValueDate, contractId: e.ContractId))
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures iti trend delta model
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesItiTrendDeltaModel)
            .SetParameters(new InsertFuturesItiTrendDeltaModel(
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                startDate: e.StartDate,
                endDate: e.EndDate,
                count: e.Count,
                maximum: e.Maximum,
                mean: e.Mean,
                median: e.Median,
                minimum: e.Minimum,
                skewness: e.Skewness,
                stdDev: e.StdDev,
                variance: e.Variance,
                meanAbsoluteError: e.MeanAbsoluteError,
                meanSquaredError: e.MeanSquaredError,
                rootMeanSquaredError: e.RootMeanSquaredError,
                lossFunction: e.LossFunction,
                rSquared: e.RSquared,
                modelData: e.ModelData
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// insert funtures iti trend class model
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendClassModelAsync(FuturesItiTrendClassModelReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesItiTrendClassModel)
            .SetParameters(new InsertFuturesItiTrendClassModel(
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                startDate: e.StartDate,
                endDate: e.EndDate,
                count: e.Count,
                maximum: e.Maximum,
                mean: e.Mean,
                median: e.Median,
                minimum: e.Minimum,
                skewness: e.Skewness,
                stdDev: e.StdDev,
                variance: e.Variance,
                accuracy: e.Accuracy,
                areaUnderPrecisionRecallCurve: e.AreaUnderPrecisionRecallCurve,
                areaUnderRocCurve: e.AreaUnderRocCurve,
                entropy: e.Entropy,
                f1Score: e.F1Score,
                modelData: e.ModelData
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures TDI Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresTdiSignal">The Futures TDI Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel futuresTdiSignal)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesTdiSignal)
            .SetParameters(new InsertFuturesTdiSignal(
                contractId: futuresTdiSignal.ContractId,
                valueDate: futuresTdiSignal.ValueDate,
                timePeriod: futuresTdiSignal.TimePeriod.ToStringFast(),
                timestamp: futuresTdiSignal.Timestamp,
                upTrendCount: futuresTdiSignal.UpTrendCount,
                downTrendCount: futuresTdiSignal.DownTrendCount,
                tdi: futuresTdiSignal.TDI.ToStringFast(),
                tdiStrength: futuresTdiSignal.TDIStrength.ToStringFast()
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures MACD Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresMacdSignal">The Futures MACD Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesMacdSignalAsync(FuturesMacdSignalReadModel futuresMacdSignal)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesMacdSignal)
            .SetParameters(new InsertFuturesMacdSignal(
                contractId: futuresMacdSignal.ContractId,
                valueDate: futuresMacdSignal.ValueDate,
                timestamp: futuresMacdSignal.Timestamp,
                macdLine: futuresMacdSignal.MacdLine,
                signalLine: futuresMacdSignal.SignalLine,
                histogram: futuresMacdSignal.Histogram,
                macd: futuresMacdSignal.MACD.ToStringFast(),
                macdStrength: futuresMacdSignal.MACDStrength.ToStringFast()
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures ATR Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresAtrSignal">The Futures ATR Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesAtrSignalAsync(FuturesAtrSignalReadModel futuresAtrSignal)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesAtrSignal)
            .SetParameters(new {
                contractId = futuresAtrSignal.ContractId,
                valueDate = futuresAtrSignal.ValueDate,
                timePeriod = futuresAtrSignal.TimePeriod.ToStringFast(),
                periodLength = futuresAtrSignal.PeriodLength,
                timestamp = futuresAtrSignal.Timestamp,
                futuresPrice = futuresAtrSignal.FuturesPrice,
                atrValue =  futuresAtrSignal.AtrValue,
                trueRange = futuresAtrSignal.TrueRange,
                atr = futuresAtrSignal.ATR.ToStringFast(),
                atrStrength = futuresAtrSignal.ATRStrength.ToStringFast()
            })
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures ATR signal data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    public async Task DeleteFuturesAtrSignalAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteFuturesAtrSignal)
            .SetParameters(new DeleteFuturesAtrSignal(contractId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures ADX Signal
    /// </summary>
    /// <param name="futuresAdxSignal">The Futures ADX Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesAdxSignalAsync(FuturesAdxSignalReadModel futuresAdxSignal)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesAdxSignal)
            .SetParameters(new InsertFuturesAdxSignal(
                contractId: futuresAdxSignal.ContractId,
                valueDate: futuresAdxSignal.ValueDate,
                timestamp: futuresAdxSignal.Timestamp,
                plusDI: futuresAdxSignal.PlusDI,
                minusDI: futuresAdxSignal.MinusDI,
                adxValue: futuresAdxSignal.AdxValue,
                adx: futuresAdxSignal.ADX.ToStringFast(),
                adxStrength: futuresAdxSignal.ADXStrength.ToStringFast()
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures ADX signal data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    public async Task DeleteFuturesAdxSignalAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.DeleteFuturesAdxSignal)
            .SetParameters(new DeleteFuturesAdxSignal(contractId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a futures trade signal
    /// </summary>
    /// <param name="FuturesTradeSignalV2ReadModel">The futures trade signal view model to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel FuturesTradeSignalV2ReadModel)
    {
        var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId);
        await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertFuturesTradeSignal)
            .SetParameters(new InsertFuturesTradeSignal(
                contractId: FuturesTradeSignalV2ReadModel.ContractId,
                valueDate: FuturesTradeSignalV2ReadModel.ValueDate,
                sequenceId,
                timestamp: FuturesTradeSignalV2ReadModel.Timestamp,
                mean: FuturesTradeSignalV2ReadModel.Mean,
                stdDev: FuturesTradeSignalV2ReadModel.StdDev,
                futuresPrice: FuturesTradeSignalV2ReadModel.FuturesPrice,
                priceChangePercent: FuturesTradeSignalV2ReadModel.PriceChangePercent,
                fundRiskPercent: FuturesTradeSignalV2ReadModel.FundRiskPercent,
                rsi: FuturesTradeSignalV2ReadModel.RSI,
                rsiSlope: FuturesTradeSignalV2ReadModel.RSISlope,
                trendType: FuturesTradeSignalV2ReadModel.TrendType.ToStringFast(),
                trendStrength: FuturesTradeSignalV2ReadModel.TrendStrength.ToStringFast(),
                tradeSignal: FuturesTradeSignalV2ReadModel.TradeSignal.ToStringFast(),
                tdi: FuturesTradeSignalV2ReadModel.TDI.ToStringFast(),
                tdiStrength: FuturesTradeSignalV2ReadModel.TDIStrength.ToStringFast(),
                mdi: FuturesTradeSignalV2ReadModel.MDI,
                mdiTrend: FuturesTradeSignalV2ReadModel.MDITrend.ToStringFast(),
                mdiUpTrendLimit: FuturesTradeSignalV2ReadModel.MDIUpTrendLimit,
                mdiDownTrendLimit: FuturesTradeSignalV2ReadModel.MDIDownTrendLimit,
                upTrendingTrigger: FuturesTradeSignalV2ReadModel.UpTrendingTrigger,
                downTrendingTrigger: FuturesTradeSignalV2ReadModel.DownTrendingTrigger,
                entryTrigger: FuturesTradeSignalV2ReadModel.EntryTrigger,
                exitTrigger: FuturesTradeSignalV2ReadModel.ExitTrigger,
                trendDelta: FuturesTradeSignalV2ReadModel.TrendDelta,
                trendExtreme: FuturesTradeSignalV2ReadModel.TrendExtreme,
                trendReversal: FuturesTradeSignalV2ReadModel.TrendReversal,
                fiftyDma: FuturesTradeSignalV2ReadModel.FiftyDMA,
                twoHundredDma: FuturesTradeSignalV2ReadModel.TwoHundredDMA,
                tradeExecuteState: FuturesTradeSignalV2ReadModel.TradeExecuteState.ToStringFast()
            ))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a collection of futures trade signals into the database asynchronously.
    /// </summary>
    /// <param name="futuresTradeSignals"></param>
    /// <returns></returns>
    public async Task InsertFuturesTradeSignalsAsync(ICollection<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
    {
        var ftsQuery = GenerateFuturesTradeSignalWithSequenceId(futuresTradeSignals);
        await _dbFactory.MarketDataDb
           .Use(MarketDataDbCql.InsertFuturesTradeSignal)
           .SetParameters(ftsQuery.Select(e => new InsertFuturesTradeSignal(
               contractId: e.ContractId,
               valueDate: e.ValueDate,
               sequenceId: e.SequenceId,
               timestamp: e.Timestamp,
               mean: e.Mean,
               stdDev: e.StdDev,
               futuresPrice: e.FuturesPrice,
               priceChangePercent: e.PriceChangePercent,
               fundRiskPercent: e.FundRiskPercent,
               rsi: e.RSI,
               rsiSlope: e.RSISlope,
               trendType: e.TrendType.ToStringFast(),
               trendStrength: e.TrendStrength.ToStringFast(),
               tradeSignal: e.TradeSignal.ToStringFast(),
               tdi: e.TDI.ToStringFast(),
               tdiStrength: e.TDIStrength.ToStringFast(),
               mdi: e.MDI,
               mdiTrend: e.MDITrend.ToStringFast(),
               mdiUpTrendLimit: e.MDIUpTrendLimit,
               mdiDownTrendLimit: e.MDIDownTrendLimit,
               upTrendingTrigger: e.UpTrendingTrigger,
               downTrendingTrigger: e.DownTrendingTrigger,
               entryTrigger: e.EntryTrigger,
               exitTrigger: e.ExitTrigger,
               trendDelta: e.TrendDelta,
               trendExtreme: e.TrendExtreme,
               trendReversal: e.TrendReversal,
               fiftyDma: e.FiftyDMA,
               twoHundredDma: e.TwoHundredDMA,
               tradeExecuteState: e.TradeExecuteState.ToStringFast()
           )))
           .ExecuteCommandAsync();

        IEnumerable<FuturesTradeSignalV2ReadModel> GenerateFuturesTradeSignalWithSequenceId(ICollection<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
        {
            foreach (var e in futuresTradeSignals)
            {
                var sequenceId = _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId).Result;
                yield return e with { SequenceId = sequenceId };
            }
        }
    }

    public async Task<long> InsertFuturesTradeSignalsAsync(IEnumerable<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
    {
        var rowCount = 0l;
        var ftsQuery = GenerateFuturesTradeSignalWithSequenceId(futuresTradeSignals);
        await _dbFactory.MarketDataDb
           .Use(MarketDataDbCql.InsertFuturesTradeSignal)
           .SetParameters(ftsQuery.Select(e => new InsertFuturesTradeSignal(
               contractId: e.ContractId,
               valueDate: e.ValueDate,
               sequenceId: e.SequenceId,
               timestamp: e.Timestamp,
               mean: e.Mean,
               stdDev: e.StdDev,
               futuresPrice: e.FuturesPrice,
               priceChangePercent: e.PriceChangePercent,
               fundRiskPercent: e.FundRiskPercent,
               rsi: e.RSI,
               rsiSlope: e.RSISlope,
               trendType: e.TrendType.ToStringFast(),
               trendStrength: e.TrendStrength.ToStringFast(),
               tradeSignal: e.TradeSignal.ToStringFast(),
               tdi: e.TDI.ToStringFast(),
               tdiStrength: e.TDIStrength.ToStringFast(),
               mdi: e.MDI,
               mdiTrend: e.MDITrend.ToStringFast(),
               mdiUpTrendLimit: e.MDIUpTrendLimit,
               mdiDownTrendLimit: e.MDIDownTrendLimit,
               upTrendingTrigger: e.UpTrendingTrigger,
               downTrendingTrigger: e.DownTrendingTrigger,
               entryTrigger: e.EntryTrigger,
               exitTrigger: e.ExitTrigger,
               trendDelta: e.TrendDelta,
               trendExtreme: e.TrendExtreme,
               trendReversal: e.TrendReversal,
               fiftyDma: e.FiftyDMA,
               twoHundredDma: e.TwoHundredDMA,
               tradeExecuteState: e.TradeExecuteState.ToStringFast()
           )))
           .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FuturesTradeSignalV2ReadModel> GenerateFuturesTradeSignalWithSequenceId(IEnumerable<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
        {
            foreach (var e in futuresTradeSignals)
            {
                rowCount++;
                var sequenceId = _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId).Result;
                yield return e with { SequenceId = sequenceId };
            }
        }
    }

    /// <summary>
    /// Inserts a rate of return record into the database asynchronously.
    /// </summary>
    /// <param name="e">The rate of return data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertRateOfReturnAsync(RateOfReturnReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertRateOfReturn)
            .SetParameters(new InsertRateOfReturn(
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                rateOfReturn: e.RateOfReturn
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a market holiday record into the database asynchronously.
    /// </summary>
    /// <param name="e">The MarketHolidayReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertMarketHolidayAsync(MarketHolidayReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.InsertMarketHoliday)
            .SetParameters(new InsertMarketHoliday(
                currencyType: e.CurrencyType.ToStringFast(),
                holidayDate: e.HolidayDate,
                description: e.Description
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// load futures iti trend class  data by date range into
    /// </summary>
    /// <param name="e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var trendDeltaDataStats = new FuturesItiTrendModelDataStatistics();
        var db = _dbFactory.MarketDataDb;
        var dbReader = db as IMarketDataDbReadContext;
        var futuresItiSignals = await dbReader!.GetFuturesItiSignalTrendClassDataAsync(symbol, startDate, endDate);
        if (futuresItiSignals?.Count > 0)
        {
            var queuedCommands = new List<object>
            {
                db.Use(MarketDataDbCql.DeleteFuturesItiTrendClassData)
                  .SetParameters(new DeleteFuturesItiTrendClassData(symbol, startDate, endDate))
                  .QueueCommand()
            };
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        return trendDeltaDataStats;


    }

    /// <summary>
    /// load futures iti trend delta data by date range into
    /// </summary>
    /// <param name="e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var trendDeltaDataStats = new FuturesItiTrendModelDataStatistics();
        var db = _dbFactory.MarketDataDb;
        var dbReader = db as IMarketDataDbReadContext;
        var futuresItiSignals = await dbReader!.GetFuturesItiSignalTrendDeltaDataAsync(symbol, startDate, endDate);
        if (futuresItiSignals?.Count > 0)
        {
            var queuedCommands = new List<object>
            {
                db.Use(MarketDataDbCql.DeleteFuturesItiTrendDeltaData)
                  .SetParameters(new DeleteFuturesItiTrendDeltaData(symbol, startDate, endDate))
                  .QueueCommand()
            };

            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }
        return trendDeltaDataStats;


    }

    /// <summary>
    /// Upserts a FuturesEodDataV2ReadModel into the database.
    /// </summary>
    /// <param name="e">The futures EOD data to upsert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesEodDataAsync(FuturesEodDataV2ReadModel e)
    {
        // check if the data already exists...
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use(MarketDataDbCql.GetFuturesDataId)
            .SetParameters(new GetFuturesDataId(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesDataId!);

        if (existingData is null)
        {
            // insert new data if it doesn't exist...
            await InsertFuturesEodDataIndexAsync(new FuturesEodDataIndexReadModel(e.ValueDate, e.ContractId));
            var newFuturesDataId = FuturesDataId.Create(e.ContractId, e.ValueDate);
            var openPrice = await GetFuturesOpenPriceAsync(newFuturesDataId);
            openPrice = openPrice == 0 ? e.OpenPrice : openPrice;
            await db.Use(MarketDataDbCql.InsertFuturesEodData)
                .SetParameters(new InsertFuturesEodData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                ))
                .ExecuteCommandAsync();

            await db.Use(MarketDataDbCql.InsertFuturesIntraDayData)
                .SetParameters(new InsertFuturesIntraDayData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    sequenceId: await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId),
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                ))
                .ExecuteCommandAsync();


        }
        else
        {
            // Update existing data if it exists
            var openPrice = await _blackboardService.FuturesOpenPrice.GetAsync(existingData, GetFuturesOpenPriceAsync);
            openPrice = openPrice == 0 ? e.OpenPrice : openPrice;
            await db.Use(MarketDataDbCql.UpdateFuturesEodData)
                .SetParameters(new UpdateFuturesEodData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                ))
                .ExecuteCommandAsync();

            await db.Use(MarketDataDbCql.InsertFuturesIntraDayData)
                .SetParameters(new InsertFuturesIntraDayData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    sequenceId: await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId),
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                ))
                .ExecuteCommandAsync();

        }


        async Task<decimal> GetFuturesOpenPriceAsync(FuturesDataId e)
        {
            var futuresClosingPrice = await db.Use(MarketDataDbCql.GetYesterdaysFuturesClosingPrice)
               .SetParameters(new GetYesterdaysFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate))
               .ExecuteSingleAsync(MapToFuturesClosingPrice!);
            return futuresClosingPrice is not null ? futuresClosingPrice.ClosingPrice : 0m;
        }

    }

    public async Task InsertFuturesEodDataAsync(ICollection<FuturesEodDataV2ReadModel> futuresEodData)
        => await _dbFactory.MarketDataDb.Use(MarketDataDbCql.InsertFuturesEodData)
                .SetParameters(futuresEodData.Select(e => new InsertFuturesEodData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    symbol: e.Symbol,
                    openPrice: e.OpenPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                )))
                .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of futures end-of-day (EOD) data records into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided futures EOD data and inserts it into the database. The
    /// method ensures that all records in the collection are processed sequentially, and the total count of processed
    /// records is returned.</remarks>
    /// <param name="futuresEodData">A collection of <see cref="FuturesEodDataV2ReadModel"/> objects representing the futures EOD data to be
    /// inserted. Each object must contain valid data for all required fields.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// records processed.</returns>
    public async Task<long> InsertFuturesEodDataAsync(IEnumerable<FuturesEodDataV2ReadModel> futuresEodData)
    {
        var rowCount = 0l;
        await _dbFactory.MarketDataDb.Use(MarketDataDbCql.InsertFuturesEodData)
                .SetParameters(GetFuturesEodData().Select(e => new InsertFuturesEodData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    symbol: e.Symbol,
                    openPrice: e.OpenPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                )))
                .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FuturesEodDataV2ReadModel> GetFuturesEodData()
        {
            foreach (var e in futuresEodData)
            {
                rowCount++;
                yield return e;
            }
        }
    }

    /// <summary>
    /// Upserts a VixFuturesEodDataReadModel into the database.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel e)
    {
        // check if the data already exists...
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use(MarketDataDbCql.GetVixFuturesEodData)
            .SetParameters(new GetVixFuturesEodData(
                contractId: e.ContractId,
                valueDate: e.ValueDate
            ))
            .ExecuteSingleAsync(MapToVixFuturesEodData!);

        if (existingData == null)
        {
            await db.Use(MarketDataDbCql.InsertVixFuturesEodData)
               .SetParameters(new InsertVixFuturesEodData(
                   contractId: e.ContractId,
                   valueDate: e.ValueDate,
                   price: e.Price,
                   size: e.Size
               ))
               .ExecuteCommandAsync();
        }
        else
        {
            var entityId = existingData.EntityId;
            var openPrice = await _blackboardService.VixFuturesOpenPrice.GetAsync(entityId, GetVixFuturesOpenPriceAsync);
            var vixFuturesTickHLVData = await GetVixFuturesTickHLVDataAsync(entityId);
            await db.Use(MarketDataDbCql.UpdateVixFuturesEodData)
               .SetParameters(new UpdateVixFuturesEodData(
                   contractId: e.ContractId,
                   valueDate: e.ValueDate,
                   openPrice,
                   highPrice: vixFuturesTickHLVData!.HighPrice,
                   lowPrice: vixFuturesTickHLVData.LowPrice,
                   closePrice: e.Price,
                   volume: vixFuturesTickHLVData.Volume + e.Size
               ))
               .ExecuteCommandAsync();
        }

        async Task<decimal> GetVixFuturesOpenPriceAsync(VixFuturesEodDataEntityId e)
        {
            var futuresOpenPrice = 0.0m;
            var tickId = await db.Use(MarketDataDbCql.GetMinFuturesTickDataTickId)
               .SetParameters(new GetMinFuturesTickDataTickId(
                   contractId: e.ContractId,
                   valueDate: e.ValueDate
               ))
               .ExecuteScalarAsync(MapToMinTickId!);

            futuresOpenPrice = await db.Use(MarketDataDbCql.GetFuturesTickDataPriceByTickId)
               .SetParameters(new GetFuturesTickDataPriceByTickId(
                   contractId: e.ContractId,
                   valueDate: e.ValueDate,
                   tickId
               ))
               .ExecuteScalarAsync(MapToPrice);

            return futuresOpenPrice;
        }
    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalMDIViewModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await GetFuturesItiSignalMaxTrendValueDate(IntrinsicTimeTrendType.UpTrend);
        var maxDownTrendValueDate = await GetFuturesItiSignalMaxTrendValueDate(IntrinsicTimeTrendType.DownTrend);

        var maxUpTrendSequenceId = await GetFuturesItiSignalMaxTrendSequenceId(IntrinsicTimeTrendType.UpTrend, IntrinsicTimeModeType.TrendDirectionChanged);
        var maxDownTrendSequenceId = await GetFuturesItiSignalMaxTrendSequenceId(IntrinsicTimeTrendType.DownTrend, IntrinsicTimeModeType.TrendDirectionChanged);

        var avgUpTrendInfo = await GetFuturesItiSignalAverageInfo(maxUpTrendSequenceId, IntrinsicTimeTrendType.UpTrend, GetIntrinsicTimeModes());
        var avgDownTrendInfo = await GetFuturesItiSignalAverageInfo(maxDownTrendSequenceId, IntrinsicTimeTrendType.DownTrend, GetIntrinsicTimeModes());

        return await db.Use(MarketDataDbCql.GetFuturesItiSignalMDI)
            .SetParameters(new GetFuturesItiSignalMDI(
                contractId,
                maxValueDate,
                intrinsicTimeModes: GetIntrinsicTimeModes(),
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend.ToStringFast()
            ))
            .ExecuteQueryAsync(MapToFuturesItiSignalMDI!);

        async Task<DateOnly> GetFuturesItiSignalMaxTrendValueDate(IntrinsicTimeTrendType intrinsicTimeTrendType)
            => await db.Use(MarketDataDbCql.GetFuturesItiSignalMaxTrendValueDate)
                .SetParameters(new GetFuturesItiSignalMaxTrendValueDate(
                    contractId,
                    valueDate,
                    intrinsicTimeTrend: intrinsicTimeTrendType.ToStringFast()
                ))
                .ExecuteScalarAsync<DateOnly>(MapToMaxValueDate!);

        async Task<long> GetFuturesItiSignalMaxTrendSequenceId(IntrinsicTimeTrendType intrinsicTimeTrendType, IntrinsicTimeModeType intrinsicTimeModeType)
            => await db.Use(MarketDataDbCql.GetFuturesItiSignalMaxTrendSequenceId)
                .SetParameters(new GetFuturesItiSignalMaxTrendSequenceId(
                    contractId,
                    maxTrendValueDate: valueDate,
                    intrinsicTimeTrend: intrinsicTimeTrendType.ToStringFast(),
                    intrinsicTimeMode: intrinsicTimeModeType.ToStringFast()
                ))
                .ExecuteScalarAsync<long>(MapToMaxSequenceId!);

        async Task<FuturesItiSignalAverageInfoDataModel?> GetFuturesItiSignalAverageInfo(long maxSequenceId, IntrinsicTimeTrendType intrinsicTimeTrendType, List<string> intrinsicTimeModes)
            => await db.Use(MarketDataDbCql.GetFuturesItiSignalAverageInfo)
                    .SetParameters(new GetFuturesItiSignalAverageInfo(
                        contractId,
                        valueDate,
                        maxSequenceId,
                        intrinsicTimeTrend: intrinsicTimeTrendType.ToStringFast(),
                        intrinsicTimeModes
                    ))
                    .ExecuteSingleAsync(MapToFuturesItiSignalAverageInfo!);

    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI by trend for a given entity ID, intrinsic time trend, and intrinsic time group ID.
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate"> </param>
    /// <param name="intrinsicTimeTrend">The intrinsic time trend.</param>
    /// <param name="intrinsicTimeGroupId">The intrinsic time group ID.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalMDIViewModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await db.Use(MarketDataDbCql.GetFuturesItiSignalMaxValueDateByTrend)
            .SetParameters(new GetFuturesItiSignalMaxValueDateByTrend(
                contractId,
                valueDate,
                intrinsicTimeModes: GetIntrinsicTimeModes(),
                intrinsicTimeTrend: intrinsicTimeTrend.ToStringFast()
            ))
            .ExecuteScalarAsync(MapToMaxValueDate);

        intrinsicTimeGroupId = intrinsicTimeGroupId == -1
            ? await db.Use(MarketDataDbCql.GetFuturesItiSignalMaxTimeGroupId)
                .SetParameters(new GetFuturesItiSignalMaxTimeGroupId(
                    contractId,
                    maxValueDate,
                    intrinsicTimeModes: GetIntrinsicTimeModes(),
                    intrinsicTimeTrend: intrinsicTimeTrend.ToStringFast()
                ))
                .ExecuteScalarAsync(MapToMaxIntrinsicTimeGroupId!)
            : intrinsicTimeGroupId;

        return await db.Use(MarketDataDbCql.GetFuturesItiSignalMDIByTrend)
            .SetParameters(new GetFuturesItiSignalMDIByTrend(
                contractId,
                maxValueDate,
                intrinsicTimeModes: GetIntrinsicTimeModes(),
                intrinsicTimeTrend: intrinsicTimeTrend.ToStringFast(),
                intrinsicTimeGroupId
            ))
            .ExecuteQueryAsync(MapToFuturesItiSignalMDI);
    }

    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="timestamp"></param>
    /// <param name="lookbackInterval"></param>
    /// <param name="startTime"></param>
    /// <param name="endTime"></param>
    /// <returns></returns>
    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime)
    {
        var db = _dbFactory.MarketDataDb;
        var upTrendCount = await db.Use(MarketDataDbCql.GetFuturesRsiSignalUpTrendCount)
            .SetParameters(new GetFuturesRsiSignalUpTrendCount(
                contractId,
                valueDate,
                startTime,
                endTime
            ))
            .ExecuteScalarAsync(MapToRsiTrendCount!);

        var downTrendCount = await db.Use(MarketDataDbCql.GetFuturesRsiSignalDownTrendCount)
            .SetParameters(new GetFuturesRsiSignalDownTrendCount(
                contractId,
                valueDate,
                startTime,
                endTime
            ))
            .ExecuteScalarAsync(MapToRsiTrendCount!);

        var trendDirection = default(FuturesTrendType) switch
        {
            _ when upTrendCount > downTrendCount => FuturesTrendType.UpTrending,
            _ when upTrendCount < downTrendCount => FuturesTrendType.DownTrending,
            _ when upTrendCount == downTrendCount => FuturesTrendType.RangeBound,
            _ => FuturesTrendType.RangeBound
        };
        return new FuturesTrendDirectionReadModel(
            ContractId: contractId,
            ValueDate: valueDate,
            Timestamp: TimeOnly.FromDateTime(DateTime.Now),
            LookbackInterval: lookbackInterval,
            UpTrendCount: upTrendCount,
            DownTrendCount: downTrendCount,
            TrendDirection: trendDirection);
    }

    /// <summary>
    /// return futures iti signal average predicted trend delta
    /// </summary>
    public async Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel> GetFuturesItiSignalAveragePredictedTrendDeltaAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;

        var maxUpTrendValueDate = await GetFuturesItiSignalMaxTrendValueDate(IntrinsicTimeTrendType.UpTrend);
        var maxDownTrendValueDate = await GetFuturesItiSignalMaxTrendValueDate(IntrinsicTimeTrendType.DownTrend);

        var maxUpTrendSequenceId = await GetFuturesItiSignalMaxTrendSequenceId(IntrinsicTimeTrendType.UpTrend, IntrinsicTimeModeType.TrendDirectionChanged);
        var maxDownTrendSequenceId = await GetFuturesItiSignalMaxTrendSequenceId(IntrinsicTimeTrendType.DownTrend, IntrinsicTimeModeType.TrendDirectionChanged);

        var avgUpTrendInfo = await GetFuturesItiSignalAverageInfo(maxUpTrendSequenceId, IntrinsicTimeTrendType.UpTrend, GetIntrinsicTimeModes());
        var avgDownTrendInfo = await GetFuturesItiSignalAverageInfo(maxDownTrendSequenceId, IntrinsicTimeTrendType.DownTrend, GetIntrinsicTimeModes());

        return new FuturesItiSignalAveragePredictedTrendDeltaDataModel(
            ContractId: contractId,
            ValueDate: valueDate,
            PredictedUpTrendDelta: avgUpTrendInfo?.PredictedDelta ?? 0,
            PredictedDownTrendDelta: avgDownTrendInfo?.PredictedDelta ?? 0,
            UpTrendFuturesRSI: avgUpTrendInfo?.FuturesRSI ?? 0,
            DownTrendFuturesRSI: avgDownTrendInfo?.FuturesRSI ?? 0);

        async Task<DateOnly> GetFuturesItiSignalMaxTrendValueDate(IntrinsicTimeTrendType intrinsicTimeTrendType)
            => await db.Use(MarketDataDbCql.GetFuturesItiSignalMaxTrendValueDate)
                .SetParameters(new GetFuturesItiSignalMaxTrendValueDate(
                    contractId,
                    valueDate,
                    intrinsicTimeTrend: intrinsicTimeTrendType.ToStringFast()
                ))
                .ExecuteScalarAsync(MapToMaxValueDate!);

        async Task<long> GetFuturesItiSignalMaxTrendSequenceId(IntrinsicTimeTrendType intrinsicTimeTrendType, IntrinsicTimeModeType intrinsicTimeModeType)
            => await db.Use(MarketDataDbCql.GetFuturesItiSignalMaxTrendSequenceId)
                .SetParameters(new GetFuturesItiSignalMaxTrendSequenceId(
                    contractId,
                    maxTrendValueDate: valueDate,
                    intrinsicTimeTrend: intrinsicTimeTrendType.ToStringFast(),
                    intrinsicTimeMode: intrinsicTimeModeType.ToStringFast()
                ))
                .ExecuteScalarAsync(MapToMaxSequenceId!);

        async Task<FuturesItiSignalAverageInfoDataModel?> GetFuturesItiSignalAverageInfo(long maxSequenceId, IntrinsicTimeTrendType intrinsicTimeTrendType, List<string> intrinsicTimeModes)
            => await db.Use(MarketDataDbCql.GetFuturesItiSignalAverageInfo)
                    .SetParameters(new GetFuturesItiSignalAverageInfo(
                        contractId,
                        valueDate,
                        maxSequenceId,
                        intrinsicTimeTrend: intrinsicTimeTrendType.ToStringFast(),
                        intrinsicTimeModes
                    ))
                    .ExecuteSingleAsync(MapToFuturesItiSignalAverageInfo!);
    }

    /// <summary>
    /// return futures iti signal average predicted trend delta by date range
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel> GetFuturesItiSignalAveragePredictedTrendDeltaRangeAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var dbSec = _dbFactory.SecuritiesDb;
        List<string> contractIds = [.. (await dbSec.Use(SecuritiesDbCql.GetFuturesContracts)
            .ExecuteQueryAsync<FuturesContractV2ReadModel>(MapToFuturesContract)).
                Where(e => e.Symbol == symbol
                    && e.LastTradeDate >= startDate
                    &&e.LastTradeDate <= endDate).Select(e => e.ContractId)];
        var db = _dbFactory.MarketDataDb;
        var predictedUpTrendDelta = await GetFuturesItiSignalAvgPredictedDelta(IntrinsicTimeTrendType.UpTrend);
        var predictedDownTrendDelta = await GetFuturesItiSignalAvgPredictedDelta(IntrinsicTimeTrendType.DownTrend);

        return new FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel(
            Symbol: symbol,
            StartDate: startDate,
            EndDate: endDate,
            PredictedUpTrendDelta: predictedUpTrendDelta,
            PredictedDownTrendDelta: predictedDownTrendDelta);

        async Task<double> GetFuturesItiSignalAvgPredictedDelta(IntrinsicTimeTrendType intrinsicTimeTrend)
            => await db.Use(MarketDataDbCql.GetFuturesItiSignalAvgPredictedDelta)
                .SetParameters(new GetFuturesItiSignalAvgPredictedDelta(
                    contractIds,
                    startDate,
                    endDate,
                    intrinsicTimeTrend: intrinsicTimeTrend.ToStringFast(),
                    intrinsicTimeModes: GetIntrinsicTimeModes()
                ))
                .ExecuteScalarAsync(MapToAveragePredictedDelta!);
    }


    /// <summary>
    /// return last futures intrinsic time indicator signal
    /// </summary>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesItiSignal)
            .SetParameters(new GetLastFuturesItiSignal(
                contractId,
                valueDate
            ))
            .ExecuteSingleAsync(MapToFuturesItiSignal!);

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend direction change
    /// </summary>\
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
        .Use(MarketDataDbCql.GetLastFuturesItiSignalTrendDirectionChange)
            .SetParameters(new GetLastFuturesItiSignalTrendDirectionChange(
                contractId,
                valueDate
            ))
            .ExecuteSingleAsync(MapToFuturesItiSignal!);

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend extreme change
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var lastTrendDirectionChangedSequenceId = await db.Use(MarketDataDbCql.GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged)
            .SetParameters(new GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged(
                contractId,
                valueDate
            ))
            .ExecuteScalarAsync(MapToMaxSequenceId!);

        return await db.Use(MarketDataDbCql.GetLastFuturesItiSignalTrendExtremeChange)
            .SetParameters(new GetLastFuturesItiSignalTrendExtremeChange(
                contractId,
                valueDate,
                lastTrendDirectionChangedSequenceId
            ))
            .ExecuteSingleAsync(MapToFuturesItiSignal!);
    }

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend reversal change
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var lastTrendDirectionChangedSequenceId = await db.Use(MarketDataDbCql.GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged)
           .SetParameters(new GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged(
               contractId,
               valueDate
           ))
           .ExecuteScalarAsync(MapToMaxSequenceId!);

        return await db.Use(MarketDataDbCql.GetLastFuturesItiSignalTrendReversalChange)
            .SetParameters(new GetLastFuturesItiSignalTrendReversalChange(
                contractId,
                valueDate,
                lastTrendDirectionChangedSequenceId
            ))
            .ExecuteSingleAsync(MapToFuturesItiSignal!);
    }

    /// <summary>
    /// Gets the last Futures RSI signal by value date.
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="timePeriod">The time period.</param>
    /// <param name="periodLength">The period length.</param>
    /// <returns></returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesRsiSignal)
            .SetParameters(new { contractId, valueDate, timePeriod, periodLength })
            .ExecuteSingleAsync(MapToFuturesRsiSignal);

    /// <summary>
    /// Gets the last Futures RSI signal by time period and period length.
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="timePeriod">The time period.</param>
    /// <param name="periodLength">The period length.</param>
    /// <returns></returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesRsiDailySignal)
            .SetParameters(new { contractId, timePeriod, periodLength })
            .ExecuteSingleAsync(MapToFuturesRsiSignal);

    /// <summary>
    /// Gets the last Futures TDI signal for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesTdiSignalReadModel"/>.</returns>
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesTdiSignal)
            .SetParameters(new GetLastFuturesTdiSignal(
                contractId,
                valueDate
            ))
            .ExecuteSingleAsync(MapToFuturesTdiSignal!);

    /// <summary>
    /// Gets the last Futures MACD signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesMacdSignalReadModel"/>.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesMacdSignal)
            .SetParameters(new
            {
                contractId,
                valueDate,
                timePeriod = timePeriod.ToStringFast(),
                periodLength
            })
            .ExecuteSingleAsync(MapToFuturesMacdSignal!);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <returns></returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesMacdDailySignal)
            .SetParameters(new
            {
                contractId,
                timePeriod = timePeriod.ToStringFast(),
                periodLength
            })
            .ExecuteSingleAsync(MapToFuturesMacdSignal!);

    /// <summary>
    /// Gets the last Futures ATR signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesAtrSignalReadModel"/>.</returns>
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesAtrSignal)
            .SetParameters(new {
                contractId,
                valueDate,
                timePeriod = timePeriod.ToStringFast(),
                periodLength
            })
            .ExecuteSingleAsync(MapToFuturesAtrSignal!);

    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId,  TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesDailyAtrSignal)
            .SetParameters(new
            {
                contractId,
                timePeriod = timePeriod.ToStringFast(),
                periodLength
            })
            .ExecuteSingleAsync(MapToFuturesAtrSignal!);

    /// <summary>
    /// Gets the last Futures ADX signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesAdxSignalReadModel"/>.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesAdxSignal)
            .SetParameters(new GetLastFuturesAdxSignal(
                contractId,
                valueDate
            ))
            .ExecuteSingleAsync(MapToFuturesAdxSignal!);

    /// <summary>
    /// Gets the last Futures ADX daily signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="timePeriod">The value date.</param>
    /// <param name="periodLength"></param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesAdxSignalReadModel"/>.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TradeTimePeriodType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesAdxDailySignal)
            .SetParameters(new
            {
                contractId,
                timePeriod = timePeriod.ToStringFast(),
                periodLength
            })
            .ExecuteSingleAsync(MapToFuturesAdxSignal!);

    /// <summary>
    /// Gets the last futures trade signal
    /// </summary>
    /// <param name="contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name="valueDate"> The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesTradeSignalV2ReadModel"/>.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate) 
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesTradeSignalById)
            .SetParameters(new GetLastFuturesTradeSignalById(
                contractId,
                valueDate 
            ))
            .ExecuteSingleAsync(MapToFuturesTradeSignal!);

    /// <summary>
    /// gets the last futures trade signal asynchronously.
    /// </summary>
    /// <returns></returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync()
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastFuturesTradeSignal)
            .ExecuteSingleAsync(MapToFuturesTradeSignal);

    /// <summary>
    /// gets all futures trade signals asynchronously.
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalsAsync()
    {
        ICollection<FuturesTradeSignalV2ReadModel> resultSet = [];
        await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesTradeSignalAll)
            .ExecuteMapReduceAsync(MapToFuturesTradeSignal, reducer => resultSet = [.. reducer]);
        return resultSet;
    }

    /// <summary>
    /// Gets the last futures trade signal for a given symbol and value date asynchronously.    
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        List<string> contractIds = [.. (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(e => e.ContractId)];
        return await db.Use(MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate))
            .ExecuteSingleAsync(MapToFuturesTradeSignal);
    }

    /// <summary>
    /// Gets the last rate of return for a given symbol asynchronously.
    /// </summary>
    /// <param name="symbol">The symbol to get the rate of return for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="RateOfReturnReadModel"/>.</returns>
    public async Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastRateOfReturn)
            .SetParameters(new GetLastRateOfReturn(symbol))
            .ExecuteSingleAsync(MapToRateOfReturn);

    /// <summary>
    /// Gets the last VIX futures EOD data for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="VixFuturesEodDataReadModel"/>.</returns>
    public async Task<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetLastVixFuturesEodData)
            .SetParameters(new GetLastVixFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToVixFuturesEodData);

	/// <summary>
	/// Gets the VIX futures EOD data for a given VixFuturesEodDataEntityId.
	/// </summary>
	/// <param name="contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name="valueDate"></param>
	public async Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetVixFuturesEodData)
            .SetParameters(new GetVixFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToVixFuturesEodData);

	/// <summary>
	/// Gets the VIX futures EOD data for a given by value date asynchronously.
	/// </summary>
	/// <param name="valueDate"></param>
	/// <returns></returns>
	public async Task<ICollection<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
	    => await _dbFactory.MarketDataDb
		    .Use(MarketDataDbCql.GetVixFuturesEodDataByValueDate)
			.SetParameters(new GetVixFuturesEodDataByValueDate(valueDate))
			.ExecuteQueryAsync(MapToVixFuturesEodData);

	/// <summary>
	/// Gets the last updated yield curve rate from the database.
	/// </summary>
	/// <returns>A task representing the asynchronous operation, containing the <see cref="YieldCurveRateReadModel"/>.</returns>
	public async Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync()
        => (await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetAllYieldCurveRates)
            .ExecuteQueryAsync(MapToYieldCurveRate))
            ?.OrderByDescending(e => e.ValueDate)?.FirstOrDefault();    

    /// <summary>
    /// Gets the yield curve rate for a given value date.
    /// </summary>
    /// <param name="valueDate">The value date to retrieve the yield curve rate for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="YieldCurveRateReadModel"/> if found; otherwise, null.</returns>
    public async Task<YieldCurveRateReadModel?> GetYieldCurveRateAsync(DateOnly valueDate) 
        =>  await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate);

    /// <summary>
    /// Gets the collection of YieldCurveRateReadModel for a given start date and end date.
    /// </summary>
    /// <param name="startDate">The start value date.</param>
    /// <param name="endDate">The end value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the collection of YieldCurveRateReadModel.</returns>
    public async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetYieldCurveRates)
            .SetParameters(new GetYieldCurveRates(startDate, endDate))
            .ExecuteQueryAsync(MapToYieldCurveRate);

    /// <summary>
    /// Gets a collection of integer values representing the years for yield curve rates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the collection of integer years.</returns>
    public async Task<ICollection<int>> GetYieldCurveRateYearsAsync()
        => [.. (await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetAllYieldCurveRates)
            .ExecuteQueryAsync(MapToYieldCurveRate)).Select(e => e.ValueDate.Year)];

    /// <summary>
    /// Retrieves market holidays for a given currency type.
    /// </summary>
    /// <param name="currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetMarketHolidays)
            .SetParameters(new GetMarketHolidays(currencyType: currencyType.ToStringFast()))
            .ExecuteQueryAsync(MapToMarketHoliday);

    /// <summary>
    /// Asynchronously retrieves the live feed data for a specific trade identified by the provided order and trade
    /// identifiers.
    /// </summary>
    /// <remarks>This method queries the market data database for the specified trade. Ensure that both
    /// orderId and tradeId are valid to avoid unexpected results.</remarks>
    /// <param name="orderId">The unique identifier of the order associated with the trade. Must be a positive integer.</param>
    /// <param name="tradeId">The unique identifier of the trade for which to retrieve live feed data. Must be a positive integer.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a TradeLiveFeedReadModel object with
    /// the live feed data if found; otherwise, null.</returns>
    public async Task<TradeLiveFeedReadModel?> GetTradeLiveFeedAsync(int orderId, int tradeId)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetTradeLiveFeed)
            .SetParameters(new GetTradeLiveFeed(orderId, tradeId))
            .ExecuteSingleAsync(MapToTradeLiveFeed!);

    /// <summary>
    /// Retrieves market holidays for a given currency type within a specified date range.
    /// </summary>
    /// <param name="currencyType">The currency type.</param>
    /// <param name="startDate">The start date of the range.</param>
    /// <param name="endDate">The end date of the range.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysByDateRangeAsync(CurrencyType currencyType, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetMarketHolidaysByDateRange)
            .SetParameters(new GetMarketHolidaysByDateRange(currencyType: currencyType.ToStringFast(), startDate, endDate))
            .ExecuteQueryAsync(MapToMarketHoliday);

    /// <summary>
    /// return number of trading days...
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="marketType"></param>
    /// <param name="currencyType"  ></param>
    /// <returns></returns>
    public async Task<int> GetTradingDaysAsync(
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType = MarketType.Futures,
        CurrencyType currencyType = CurrencyType.USD)
    {
        var key = new TradingDaysKey(
            StartDate: startDate,
            EndDate: endDate,
            MarketType: marketType,
            CurrencyType: currencyType);

        if (_tradingDaysMap.TryGetValue(key, out int value))
            return value;

        // load market holidays by currency type..
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType)!;

        // build holiday map...
        var holidayMap = new Dictionary<DateOnly, MarketHolidayReadModel>();
        foreach (var e in marketHolidays)
            holidayMap.Add(e.HolidayDate, e);

        // calculate trading days based on total number of days from start date to end date
        // that do not fall on a weekend or holiday...
        var dateIndex = 0;
        var tradingDays = 0;
        while (startDate.AddDays(dateIndex) <= endDate)
        {
            var tradeDate = startDate.AddDays(dateIndex++);
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday
                || tradeDate.DayOfWeek == DayOfWeek.Sunday
                || holidayMap.ContainsKey(tradeDate))
                continue;
            tradingDays++;
        }
        _tradingDaysMap.Add(key, tradingDays);
        return tradingDays;
    }

    /// <summary>
    /// return all normal curve data
    /// </summary>
    public async Task<ICollection<NormalCurveDataReadModel>> GetNormalCurveDataAsync()
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetNormalCurveData)
            .ExecuteQueryAsync(MapToNormalCurveData!);

	/// <summary>
	/// return futures trade signal id by value date
	/// </summary>
	/// <param name="valueDate"></param>
	/// <returns></returns>
	public async Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate)
	     => await _dbFactory.MarketDataDb
			    .Use(MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)
                .ExecuteQueryAsync(MapToFuturesTradeSignalId);        

    /// <summary>
    /// return normal curve table
    /// </summary>
    /// <returns></returns>
    public async Task<NormalCurveTableReadModel> GetNormalCurveTableAsync()
    {
        _normalCurveTable ??= new NormalCurveTableReadModel( [.. await GetNormalCurveDataAsync()]);
        return _normalCurveTable!;
    }
    /// <summary>
    /// return trading dates...
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="marketType"></param>
    /// <param name="currencyType"></param>
    /// <returns></returns>
    public async Task<DateOnly[]> GetTradingDatesAsync(
       DateOnly startDate,
       DateOnly endDate,
       MarketType marketType = MarketType.Futures,
       CurrencyType currencyType = CurrencyType.USD)
    {

        // load market holidays by currency type..
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType)!;

        // build holiday map...
        var holidayMap = new Dictionary<DateOnly, MarketHolidayReadModel>();
        foreach (var e in marketHolidays)
            holidayMap.Add(e.HolidayDate, e);

        // calculate trading days based on total number of days from start date to end date
        // that do not fall on a weekend or holiday...
        var dateIndex = 0;
        var tradingDates = new List<DateOnly>();
        while (startDate.AddDays(dateIndex) <= endDate)
        {
            var tradeDate = startDate.AddDays(dateIndex++);
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday
                || tradeDate.DayOfWeek == DayOfWeek.Sunday
                || holidayMap.ContainsKey(tradeDate))
                continue;
            tradingDates.Add(tradeDate);
        }
        return [.. tradingDates];
    }

    /// <summary>
    /// Checks if yield curve rate data exists for a given value date.
    /// </summary>
    /// <param name="valueDate">The value date to check.</param>
    /// <returns>A task representing the asynchronous operation, containing a boolean indicating whether the data exists.</returns>
    public async Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate)
        => (await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate!)) is not null;

    /// <summary>
    /// Retrieves a list of intrinsic time mode types as string representations.
    /// </summary>
    /// <remarks>The returned list includes the following intrinsic time mode types: <see
    /// cref="IntrinsicTimeModeType.TrendExtremeChanged"/>,  <see cref="IntrinsicTimeModeType.TrendReversalChanged"/>,
    /// and  <see cref="IntrinsicTimeModeType.TrendDirectionChanged"/>.</remarks>
    /// <returns>A list of strings, where each string represents an intrinsic time mode type.</returns>
    static List<string> GetIntrinsicTimeModes()
              => [IntrinsicTimeModeType.TrendExtremeChanged.ToStringFast(),
                IntrinsicTimeModeType.TrendReversalChanged.ToStringFast(),
                IntrinsicTimeModeType.TrendDirectionChanged.ToStringFast()];

    /// <summary>
    /// return stream request id
    /// </summary>
    /// <param name="streamId"></param>
    /// <returns></returns>
    public async Task<int> GetStreamingRequestIdAsync()
        => Convert.ToInt32(await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId));

    /// <summary>
    /// return option quote id
    /// </summary>
    /// <param name="streamId"></param>
    /// <returns></returns>
    public async Task<int> GetOptionQuoteIdAsync()
        => Convert.ToInt32(await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionQuote_QuoteId));

    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate)
    {
        var futuresItiSignals = await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetFuturesItiTrendDirectionChangedSignals)
            .SetParameters(new GetFuturesItiTrendDirectionChangedSignals(
                contractId,
                valueDate
            ))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);
        return [.. futuresItiSignals.OrderByDescending(e => e.SequenceId)];
    }

}

/// <summary>
/// Represents a unique key for identifying a range of trading days within a specific market and currency context.
/// </summary>
/// <remarks>This record is used to encapsulate the start and end dates of a trading period, along with the
/// associated market type and currency type. It is primarily intended for scenarios where trading day ranges need to be
/// uniquely identified or compared.</remarks>
/// <param name="StartDate"></param>
/// <param name="EndDate"></param>
/// <param name="MarketType"></param>
/// <param name="CurrencyType"></param>
record TradingDaysKey(
            DateOnly StartDate,
            DateOnly EndDate,
            MarketType MarketType,
            CurrencyType CurrencyType)
{
    public override string ToString() => $"{StartDate:yyyy-MM-dd}|{EndDate:yyyy-MM-dd}|{MarketType}|{CurrencyType}";
}
