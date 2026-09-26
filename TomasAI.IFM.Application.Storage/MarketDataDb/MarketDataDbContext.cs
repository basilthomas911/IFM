using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using MathNet.Numerics.Distributions;
using Microsoft.Extensions.Logging;
using System.Globalization;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
/// <summary>
/// market data database constructor
/// </summary>
/// <param name = "connectionSettings"></param>
/// <param name = "dbFactory"></param>
/// <param name = "logger"></param>
public class MarketDataDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, IBlackboardService blackboardService, ISequenceIdGenerator sequenceIdGenerator, ILogger<DbProvider> logger) : ObjectDataRepository<MarketDataDbContext>(connectionSettings[MarketDataDbConnection], logger), IMarketDataDbContext
{
    public const string MarketDataDbConnection = "MarketDataDbConnection";
    internal const string FuturesTickByTimeProjection = "futures_tick_data_by_time";
    internal const string FuturesEodProjection = "futures_eod_data_by_month";
    internal const string VixFuturesContractIndexProjection = "vix_futures_contract_index";
    internal const int ProjectionWriteBatchSize = 256;
    internal const int TickAtomicBatchRowCount = 24;
    internal const int VixContractBucketCount = 32;
    internal const int ProjectionGuardScopeCount = 32;
    internal const string ProjectionGuardScopePrefix = "$guard:";
    internal const int ProjectionReadConcurrency = 8;
    internal const int ProjectionScopeStateReadBatchSize = 32;
    internal const int YieldCurveMaximumRangeDays = 3_660;
    internal const int YieldCurveMaximumRows = 5_000;
    internal const int YieldCurveMaximumYears = 200;
    internal const int YieldCurveLookupId = 1;
    internal readonly static Dictionary<TradingDaysKey, int> _tradingDaysMap = [];
    internal readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    internal readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    internal readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    internal static NormalCurveTableReadModel? _normalCurveTable;
    // Deterministic integration-test seams for the two sides of the online-backfill
    // fence. They remain null in production and do not expose migration state publicly.
    internal Func<Func<Task>, Task>? TickProjectionGuardRegistrationForTestingAsync { get; set; }
    internal Func<Task>? TickProjectionGuardRegisteredForTestingAsync { get; set; }
    internal Func<Func<Task>, Task>? MaintainedProjectionScopeActivationForTestingAsync { get; set; }
    internal Func<Task>? MaintainedProjectionMutationSubmittingForTestingAsync { get; set; }
    internal Func<Task>? FuturesEodProjectionMonthSubmittingForTestingAsync { get; set; }
    internal Func<Func<Task>, Task>? ProjectionBackfillGlobalActivationForTestingAsync { get; set; }
    internal Func<Func<Task>, Task>? ProjectionBackfillScopeActivationForTestingAsync { get; set; }
    internal Func<Task>? ProjectionBackfillTargetMutationSubmittingForTestingAsync { get; set; }
    internal Func<Task>? ProjectionBackfillReconciledForTestingAsync { get; set; }
    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override MarketDataDbContext Database => this;
    /// <summary>Gets the Market Data database read capability.</summary>
    public IMarketDataDbReadContext DbReader => this;
    /// <summary>Gets the Market Data database write capability.</summary>
    public IMarketDataDbWriteContext DbWriter => this;

    internal static int MapToYearMonth<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetInt(0);
    internal static string MapToString<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetString(0);
    internal static Guid MapToGuid<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetGuid(0);
    internal static bool MapToBoolean<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetBool(0);
    internal static MarketDataImportOwnership MapToMarketDataImportOwnership<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => new(row.GetGuid(0), row.GetBool(1));
    internal static MarketDataProjectionMutationReadModel MapToProjectionMutation<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetGuid(0), e.GetDateTime(1));
    internal static MarketDataProjectionScopeMutationReadModel MapToProjectionScopeMutation<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetString(0), e.GetString(1), e.GetGuid(2), e.GetDateTime(3));
    internal static VixFuturesContractIndexReadModel MapToVixFuturesContractIndex<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetInt(0), e.GetString(1));
    internal static MarketDataProjectionStateReadModel MapToProjectionState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetString(0), e.GetGuid(1), e.GetBool(2));
    internal static MarketDataProjectionScopeStateReadModel MapToProjectionScopeState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetString(0), e.GetString(1), e.GetGuid(2), e.GetBool(3), e.GetBool(4), e.IsCollectionEmpty(5));
    internal static string MapToFuturesTickProjectionScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => MarketDataDbContextExtensions.GetFuturesTickScopeKey(e.GetString(0), e.GetDateOnly(1));
    internal static string MapToFuturesEodProjectionSourceScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.GetDateOnly(0));
    internal static string MapToFuturesEodProjectionTargetScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.GetInt(0));
    public async Task<MarketDataProjectionReadinessReadModel> GetQueryProjectionReadinessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(await this.GetProjectionScopeReadStampAsync(FuturesTickByTimeProjection, this.GetProjectionGuardScopeKeys()) is not null, await this.GetProjectionScopeReadStampAsync(FuturesEodProjection, this.GetProjectionGuardScopeKeys()) is not null, await this.GetProjectionScopeReadStampAsync(VixFuturesContractIndexProjection, this.GetProjectionGuardScopeKeys()) is not null, await this.GetProjectionScopeReadStampAsync(FuturesItiSignalQueryProjection, this.GetProjectionGuardScopeKeys()) is not null);
    }

    internal static FuturesDataId MapToFuturesDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1));
    internal static FuturesTickDataV2ReadModel MapToFuturesTickData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), price: e.GetDecimal(4), size: e.GetInt(5));
    internal static FuturesTickDataId MapToFuturesTickDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), TickId: e.GetLong(2));
    internal static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), optionPrice: e.GetDouble(4), bidPrice: e.GetDouble(5), askPrice: e.GetDouble(6), bidSize: e.GetInt(7), askSize: e.GetInt(8), impliedVolatility: e.GetDouble(9), underlyingPrice: e.GetDouble(10), delta: e.GetDouble(11), gamma: e.GetDouble(12), vega: e.GetDouble(13), theta: e.GetDouble(14), rho: e.GetDouble(15));
    internal static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickPriceData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), optionPrice: e.GetDouble(4), bidPrice: e.GetDouble(5), askPrice: e.GetDouble(6), bidSize: e.GetInt(7), askSize: e.GetInt(8), impliedVolatility: e.GetDouble(9), underlyingPrice: e.GetDouble(10), delta: e.GetDouble(11), gamma: e.GetDouble(12), vega: e.GetDouble(13), theta: e.GetDouble(14), rho: e.GetDouble(15));
    internal static FuturesOptionTickDataId MapToFuturesOptionTickDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), TickId: e.GetLong(2));
    internal static FuturesBarDataReadModel MapToFuturesBarData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), symbol: e.GetString(1), valueDate: e.GetDateOnly(2), barDate: e.GetDateTime(3), barRateType: e.GetEnum<BarRateType>(4), barValue: e.GetDecimal(5), upTrendTrigger: e.GetDouble(6), downTrendTrigger: e.GetDouble(7));
    internal static long MapToFuturesBarDataCount<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetLong(0);
    internal static FuturesClosingPriceReadModel MapToFuturesClosingPrice<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), closingPrice: e.GetDecimal(2), createdOn: e.GetDateTime(3), createdBy: e.GetString(4));
    internal static FuturesEodDataV2ReadModel MapToFuturesEodData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), symbol: e.GetString(2), openPrice: e.GetDecimal(3), highPrice: e.GetDecimal(4), lowPrice: e.GetDecimal(5), closePrice: e.GetDecimal(6), volume: e.GetLong(7), dailyPercentChange: e.GetDouble(8), dailyStdDev: e.GetDouble(9), dailyStdDevAmount: e.GetDouble(10), upperBand: e.GetDouble(11), mean: e.GetDouble(12), lowerBand: e.GetDouble(13), marketDirection: e.GetEnum<MarketDirectionType>(14), marketVolatility: e.GetEnum<MarketVolatilityType>(15), priceDirection: e.GetEnum<PriceDirectionType>(16), priceVolatility: e.GetEnum<PriceVolatilityType>(17), marketDirectionIndicator: e.GetDouble(18), windowSize: e.GetInt(19), fiftyDMA: e.IsNull(20) ? 0m : e.GetDecimal(20), twoHundredDMA: e.IsNull(21) ? 0m : e.GetDecimal(21));
    internal static FuturesIntraDayDataReadModel MapToFuturesIntraDayData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), sequenceId: e.GetLong(2), symbol: e.GetString(3), openPrice: e.GetDecimal(4), highPrice: e.GetDecimal(5), lowPrice: e.GetDecimal(6), closePrice: e.GetDecimal(7), volume: e.GetLong(8), dailyPercentChange: e.GetDouble(9), dailyStdDev: e.GetDouble(10), dailyStdDevAmount: e.GetDouble(11), upperBand: e.GetDouble(12), mean: e.GetDouble(13), lowerBand: e.GetDouble(14), marketDirection: e.GetEnum<MarketDirectionType>(15), marketVolatility: e.GetEnum<MarketVolatilityType>(16), priceDirection: e.GetEnum<PriceDirectionType>(17), priceVolatility: e.GetEnum<PriceVolatilityType>(18), marketDirectionIndicator: e.GetInt(19), windowSize: e.GetInt(20));
    internal static FuturesEodClosingPriceReadModel MapToFuturesEodClosingPrice<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), closingPrice: e.GetDecimal(2));
    internal static FuturesTickHLVDataReadModel MapToFuturesTickHLVData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), HighPrice: e.GetDecimal(2), LowPrice: e.GetDecimal(3), Volume: e.GetLong(4));
    internal static FuturesItiSignalV2ReadModel MapToFuturesItiSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3), intrinsicTime: e.GetDateTime(4), intrinsicTimeGroupId: e.GetInt(5), intrinsicTimeLength: e.GetDouble(6), intrinsicPrice: e.GetDouble(7), intrinsicTimeTrend: e.GetEnum<IntrinsicTimeTrendType>(8), intrinsicTimeMode: e.GetEnum<IntrinsicTimeModeType>(9), trendPrice: e.GetDouble(10), trendExtreme: e.GetDouble(11), trendReversal: e.GetDouble(12), trendDelta: e.GetDouble(13), targetDelta: e.GetDouble(14), lambda: e.GetDouble(15), tradingDays: e.GetInt(16), threshold: e.GetDouble(17), upTrendTrigger: e.GetDouble(18), downTrendTrigger: e.GetDouble(19), tradeState: e.GetEnum<IntrinsicTimeTradeState>(20), bandLevel: e.GetDouble(21), reversalLevel: e.GetDouble(22));
    internal static FuturesItiSignalMDIV2ReadModel MapToFuturesItiSignalMDI<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), intrinsicTime: e.GetDateTime(2), trendType: e.GetEnum<IntrinsicTimeTrendType>(3), mdi: e.GetDouble(4));
    internal static DateOnly MapToMaxValueDate<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDateOnly(0);
    internal static int MapToMaxIntrinsicTimeGroupId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetInt(0);
    internal static FuturesItiTrendDeltaDataReadModel MapToFuturesItiTrendDeltaData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), timestamp: e.GetDateTime(2), sequenceId: e.GetLong(3), trendDelta: e.GetFloat(4), trendDirection: e.GetFloat(5), trendDirectionMode: e.GetInt(6), futuresPrice: e.GetFloat(7), trendExtreme: e.GetFloat(8), futuresRsi: e.GetFloat(9));
    internal static FuturesItiTrendClassDataReadModel MapToFuturesItiTrendClassData(IObjectDataRecord e) => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), timestamp: e.GetDateTime(2), sequenceId: e.GetLong(3), trendClass: e.GetFloat(4), trendDirection: e.GetFloat(5), trendDirectionMode: e.GetInt(6), trendDelta: e.GetFloat(7), futuresRsi: e.GetFloat(8));
    internal static FuturesItiSignalV2ReadModel MapToFuturesItiTimeFrameState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3), intrinsicTime: e.GetDateTime(4), intrinsicTimeGroupId: e.GetInt(5), intrinsicTimeLength: e.GetDouble(6), intrinsicPrice: e.GetDouble(7), intrinsicTimeTrend: e.GetEnum<IntrinsicTimeTrendType>(8), intrinsicTimeMode: e.GetEnum<IntrinsicTimeModeType>(9), trendPrice: e.GetDouble(10), trendExtreme: e.GetDouble(11), trendReversal: e.GetDouble(12), trendDelta: e.GetDouble(13), targetDelta: e.GetDouble(14), lambda: e.GetDouble(15), tradingDays: e.GetInt(16), threshold: e.GetDouble(17), upTrendTrigger: e.GetDouble(18), downTrendTrigger: e.GetDouble(19), tradeState: e.GetEnum<IntrinsicTimeTradeState>(20), timeFrameStartValueDate: e.GetDateOnly(21), bandAnchorPrice: e.GetDouble(22), bandPercentage: e.GetDouble(23), bandSize: e.GetDouble(24), bandLevel: e.GetDouble(25), reversalLevel: e.GetDouble(26));
    internal static FuturesItiTrendDeltaModelReadModel MapToFuturesItiTrendDeltaModel<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), startDate: e.GetDateOnly(2), endDate: e.GetDateOnly(3), count: e.GetInt(4), maximum: e.GetDouble(5), mean: e.GetDouble(6), median: e.GetDouble(7), minimum: e.GetDouble(8), skewness: e.GetDouble(9), stdDev: e.GetDouble(10), variance: e.GetDouble(11), meanAbsoluteError: e.GetDouble(12), meanSquaredError: e.GetDouble(13), rootMeanSquaredError: e.GetDouble(14), lossFunction: e.GetDouble(15), rSquared: e.GetDouble(16), modelData: e.GetBytes(17));
    internal static FuturesItiTrendClassModelReadModel MapToFuturesItiTrendClassModel<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), startDate: e.GetDateOnly(2), endDate: e.GetDateOnly(3), count: e.GetInt(4), maximum: e.GetDouble(5), mean: e.GetDouble(6), median: e.GetDouble(7), minimum: e.GetDouble(8), skewness: e.GetDouble(9), stdDev: e.GetDouble(10), variance: e.GetDouble(11), accuracy: e.GetDouble(12), areaUnderPrecisionRecallCurve: e.GetDouble(13), areaUnderRocCurve: e.GetDouble(14), entropy: e.GetDouble(15), f1Score: e.GetDouble(16), modelData: e.GetBytes(17));
    internal static double MapToRsi<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDouble(0);
    internal static long MapToMaxSequenceId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetLong(0);
    internal static FuturesTdiSignalReadModel MapToFuturesTdiSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new()
        {
            ContractId = e.GetString(0),
            ValueDate = e.GetDateOnly(1),
            TimePeriod = e.GetEnum<TimeFrameType>(2),
            Timestamp = e.GetTimeOnly(3),
            SchemaVersion = e.GetInt(4),
            ConfigurationId = e.GetString(5),
            RsiPeriod = e.GetInt(6),
            PriceLinePeriod = e.GetInt(7),
            SignalLinePeriod = e.GetInt(8),
            MarketBasePeriod = e.GetInt(9),
            VolatilityBandPeriod = e.GetInt(10),
            VolatilityBandDeviation = e.GetDouble(11),
            Price = e.GetDecimal(12),
            Rsi = e.GetDouble(13),
            PriceLine = e.GetDouble(14),
            SignalLine = e.GetDouble(15),
            MarketBaseLine = e.GetDouble(16),
            UpperVolatilityBand = e.GetDouble(17),
            LowerVolatilityBand = e.GetDouble(18),
            BandWidth = e.GetDouble(19),
            PriceSignalDivergence = e.GetDouble(20),
            Cross = e.GetEnum<FuturesTdiCrossType>(21),
            MarketState = e.GetEnum<FuturesTdiMarketStateType>(22),
            TDI = e.GetEnum<FuturesTrendDirectionType>(23),
            TDIStrength = e.GetEnum<FuturesTrendDirectionStrengthType>(24),
            SourceSequence = e.GetLong(25),
            SourceEventTimestamp = e.GetDateTime(26)
        };
    internal static FuturesMacdSignalReadModel MapToFuturesMacdSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
    {
        var isWarm = !e.IsNull(15) && e.GetBool(15);
        var signalEmaPeriod = e.GetInt(3);
        var slowEmaPeriod = e.GetInt(5);
        var signal = new FuturesMacdSignalReadModel(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), signalEmaPeriod: signalEmaPeriod, fastEmaPeriod: e.GetInt(4), slowEmaPeriod: slowEmaPeriod, timestamp: e.GetTimeOnly(6), futuresPrice: e.GetDecimal(7), fastEma: e.GetDouble(8), slowEma: e.GetDouble(9), macdLine: e.GetDouble(10), signalLine: e.GetDouble(11), histogram: e.GetDouble(12), macd: e.GetEnum<FuturesTrendDirectionType>(13), macdStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(14))
        {
            IsWarm = isWarm,
            ObservationCount = e.IsNull(16) ? isWarm ? slowEmaPeriod + signalEmaPeriod : 0 : e.GetInt(16)
        };
        if (e.IsNull(17))
            return signal;
        var marketDataAsOf = new DateTimeOffset(DateTime.SpecifyKind(e.GetDateTime(19), DateTimeKind.Utc));
        return signal with
        {
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(MarketSeriesIdentity.ForContract(signal.ContractId), MarketAnalyticsSignalKind.Macd, signal.TimePeriod, e.GetString(17)),
                ContractId = signal.ContractId,
                ValueDate = signal.ValueDate,
                ObservationId = new FuturesTradeSessionBarId(e.GetGuid(18)),
                MarketDataAsOfUtc = marketDataAsOf,
                CalculatedAtUtc = marketDataAsOf,
                SourceSequence = e.GetLong(20),
                CalculationVersion = e.GetString(21),
                CalculationMethod = e.GetEnum<MarketSignalCalculationMethod>(22),
                SchemaVersion = checked((ushort)e.GetInt(23)),
                IsValid = e.GetBool(24)
            }
        };
    }

    internal static FuturesAtrSignalReadModel MapToFuturesAtrSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
    {
        var signal = new FuturesAtrSignalReadModel(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), periodLength: e.GetInt(3), timestamp: e.GetTimeOnly(4), futuresPrice: e.GetDecimal(5), atrValue: e.GetDouble(6), trueRange: e.GetDouble(7), atr: e.GetEnum<FuturesTrendDirectionType>(8), atrStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(9))
        {
            PreviousAtrValue = e.IsNull(10) ? null : e.GetDouble(10),
            AtrBaseline = e.IsNull(11) ? null : e.GetDouble(11),
            AtrRatio = e.IsNull(12) ? null : e.GetDouble(12),
            IsWarm = !e.IsNull(13) && e.GetBool(13)
        };
        if (e.IsNull(14))
            return signal;
        var marketDataAsOf = new DateTimeOffset(DateTime.SpecifyKind(e.GetDateTime(16), DateTimeKind.Utc));
        return signal with
        {
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(MarketSeriesIdentity.ForContract(signal.ContractId), MarketAnalyticsSignalKind.Atr, signal.TimePeriod, e.GetString(14)),
                ContractId = signal.ContractId,
                ValueDate = signal.ValueDate,
                ObservationId = new FuturesTradeSessionBarId(e.GetGuid(15)),
                MarketDataAsOfUtc = marketDataAsOf,
                CalculatedAtUtc = marketDataAsOf,
                SourceSequence = e.GetLong(17),
                CalculationVersion = e.GetString(18),
                CalculationMethod = e.GetEnum<MarketSignalCalculationMethod>(19),
                SchemaVersion = checked((ushort)e.GetInt(20)),
                IsValid = e.GetBool(21)
            }
        };
    }

    internal static FuturesAdxSignalReadModel MapToFuturesAdxSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
    {
        var signal = new FuturesAdxSignalReadModel(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), periodLength: e.GetInt(3), timestamp: e.GetTimeOnly(4), futuresPrice: e.GetDecimal(5), plusDI: e.GetDouble(6), minusDI: e.GetDouble(7), adxValue: e.GetDouble(8), adx: e.GetEnum<FuturesTrendDirectionType>(9), adxStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(10));
        if (e.IsNull(11))
            return signal;
        var marketDataAsOf = new DateTimeOffset(DateTime.SpecifyKind(e.GetDateTime(13), DateTimeKind.Utc));
        return signal with
        {
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(MarketSeriesIdentity.ForContract(signal.ContractId), MarketAnalyticsSignalKind.Adx, signal.TimePeriod, e.GetString(11)),
                ContractId = signal.ContractId,
                ValueDate = signal.ValueDate,
                ObservationId = new FuturesTradeSessionBarId(e.GetGuid(12)),
                MarketDataAsOfUtc = marketDataAsOf,
                CalculatedAtUtc = marketDataAsOf,
                SourceSequence = e.GetLong(14),
                CalculationVersion = e.GetString(15),
                CalculationMethod = e.GetEnum<MarketSignalCalculationMethod>(16),
                SchemaVersion = checked((ushort)e.GetInt(17)),
                IsValid = e.GetBool(18)
            }
        };
    }

    internal static FuturesTradeSignalV2ReadModel MapToFuturesTradeSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3), timestamp: e.GetTimeOnly(4), mean: e.GetDouble(5), stdDev: e.GetDouble(6), futuresPrice: e.GetDouble(7), priceChangePercent: e.GetDouble(8), fundRiskPercent: e.GetDouble(9), rsi: e.GetDouble(10), rsiSlope: e.GetDouble(11), trendType: e.GetEnum<FuturesTrendType>(12), trendStrength: e.GetEnum<FuturesTrendStrengthType>(13), tradeSignal: e.GetEnum<TradeSignalType>(14), tdi: e.GetEnum<FuturesTrendDirectionType>(15), tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(16), mdi: e.GetDouble(17), mdiTrend: e.GetEnum<FuturesMDITrendType>(18), mdiUpTrendLimit: e.GetDouble(19), mdiDownTrendLimit: e.GetDouble(20), upTrendingTrigger: e.GetDouble(21), downTrendingTrigger: e.GetDouble(22), entryTrigger: e.GetDouble(23), exitTrigger: e.GetDouble(24), trendDelta: e.GetDouble(25), trendExtreme: e.GetDouble(26), trendReversal: e.GetDouble(27), fiftyDMA: e.GetDecimal(28), twoHundredDMA: e.GetDecimal(29), tradeExecuteState: e.GetEnum<TradeExecuteState>(30));
    internal static RateOfReturnReadModel MapToRateOfReturn<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), rateOfReturn: e.GetDouble(2));
    internal static VixFuturesEodDataReadModel MapToVixFuturesEodData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), openPrice: e.GetDecimal(2), highPrice: e.GetDecimal(3), lowPrice: e.GetDecimal(4), closePrice: e.GetDecimal(5), volume: e.GetLong(6));
    internal static long MapToMinTickId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetLong(0);
    internal static decimal MapToPrice<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDecimal(0);
    internal static YieldCurveRateReadModel MapToYieldCurveRate<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(valueDate: e.GetDateOnly(0), oneMonth: e.GetDouble(1), twoMonth: e.GetDouble(2), threeMonth: e.GetDouble(3), sixMonth: e.GetDouble(4), oneYear: e.GetDouble(5), twoYear: e.GetDouble(6), threeYear: e.GetDouble(7), fiveYear: e.GetDouble(8), sevenYear: e.GetDouble(9), tenYear: e.GetDouble(10), twentyYear: e.GetDouble(11), thirtyYear: e.GetDouble(12));
    internal static MarketHolidayReadModel MapToMarketHoliday<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(currencyType: e.GetEnum<CurrencyType>(0), holidayDate: e.GetDateOnly(1), description: e.GetString(2));
    internal static FuturesItiSignalAverageInfoDataModel MapToFuturesItiSignalAverageInfo<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), predictedDelta: e.GetDouble(2), futuresRSI: e.GetDouble(3));
    internal static double MapToAveragePredictedDelta<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDouble(0);
    internal static NormalCurveDataReadModel MapToNormalCurveData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(stdDevIndex: e.GetInt(0), percent: e.GetDouble(1));
    internal static FuturesTradeSignalId MapToFuturesTradeSignalId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3));
    internal static FuturesRsiSignalReadModel MapToFuturesRsiSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
    {
        var signal = new FuturesRsiSignalReadModel(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), periodLength: e.GetInt(3), timestamp: e.GetTimeOnly(4), price: e.GetDecimal(5), priceChange: e.GetDecimal(6), priceGain: e.GetDecimal(7), priceLoss: e.GetDecimal(8), averagePriceGain: e.GetDecimal(9), averagePriceLoss: e.GetDecimal(10), rs: e.GetDouble(11), rsi: e.GetDouble(12), rsiAverage: e.GetDouble(13), rsiSlope: e.GetDouble(14), sourceSequence: e.GetLong(15), sourceEventTimestamp: e.GetDateTime(16))
        {
            PreviousRsi = e.IsNull(24) ? null : e.GetDouble(24),
            RegimeSlope = e.IsNull(25) ? null : e.GetDouble(25),
            IsWarm = !e.IsNull(26) ? e.GetBool(26) : !e.IsNull(23) && e.GetBool(23)
        };
        if (e.IsNull(17))
            return signal;
        var marketDataAsOf = new DateTimeOffset(DateTime.SpecifyKind(e.GetDateTime(19), DateTimeKind.Utc));
        return signal with
        {
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(MarketSeriesIdentity.ForContract(signal.ContractId), MarketAnalyticsSignalKind.Rsi, signal.TimePeriod, e.GetString(17)),
                ContractId = signal.ContractId,
                ValueDate = signal.ValueDate,
                ObservationId = new FuturesTradeSessionBarId(e.GetGuid(18)),
                MarketDataAsOfUtc = marketDataAsOf,
                CalculatedAtUtc = marketDataAsOf,
                SourceSequence = signal.SourceSequence,
                CalculationVersion = e.GetString(20),
                CalculationMethod = e.GetEnum<MarketSignalCalculationMethod>(21),
                SchemaVersion = checked((ushort)e.GetInt(22)),
                IsValid = e.GetBool(23),
                ValidationIssues = e.GetBool(23) ? [] : [MarketSignalValidationIssue.InvalidCalculation]
            }
        };
    }

    internal static FuturesContractV3ReadModel MapToFuturesContract(IObjectDataRecord o) => new(o.GetString(0), o.GetString(1), o.GetString(2), o.GetString(3), o.GetString(4), o.GetString(5), o.GetString(6), o.GetString(7), o.GetDateOnly(8), o.GetBool(9));
    internal static TradeLiveFeedReadModel MapToTradeLiveFeed<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), tradeLiveFeedState: e.GetEnum<TradeLiveFeedStateType>(2));
    /// <summary>
    /// Deletes a futures bar data record from the database.
    /// </summary>
    /// <param name = "e">The identifier of the futures bar data to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesBarDataAsync(FuturesBarDataId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesBarData)}", MarketDataDbCql.DeleteFuturesBarData)
        .SetParameters(new DeleteFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Asynchronously deletes the closing price entry for the specified futures contract on the given date.
    /// </summary>
    /// <remarks>If no closing price exists for the specified contract and date, no action is taken. Ensure
    /// that the contract identifier and date correspond to an existing entry to avoid unnecessary operations.</remarks>
    /// <param name = "contractId">The unique identifier of the futures contract for which to delete the closing price.</param>
    /// <param name = "valueDate">The date of the closing price to delete. Must be a valid date.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesClosingPriceAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesClosingPrice)}", MarketDataDbCql.DeleteFuturesClosingPrice)
        .SetParameters(new DeleteFuturesClosingPrice(contractId, valueDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Deletes futures EOD data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task DeleteFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        await this.ExecuteMaintainedProjectionMutationAsync(FuturesEodProjection, new[] { MarketDataDbContextExtensions.GetFuturesEodScopeKey(valueDate) }, async () =>
        {
            var db = _dbFactory.MarketDataDb;
            List<object> commands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesEodData)}", MarketDataDbCql.DeleteFuturesEodData)
                .SetParameters(new DeleteFuturesEodData(contractId, valueDate))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesEodDataByMonth)}", MarketDataDbCql.DeleteFuturesEodDataByMonth)
                .SetParameters(new DeleteFuturesEodDataByMonth(MarketDataDbContextExtensions.ToYearMonth(valueDate), valueDate, contractId))
                .QueueCommand()];
            await db.ExecuteQueuedCommandsAsync(commands);
        });
    }

    /// <summary>
    /// Asynchronously deletes all futures tick data for the specified contract and value date from the market data
    /// database.
    /// </summary>
    /// <remarks>Ensure that the specified contract identifier and value date are valid before calling this
    /// method. This operation is irreversible and will remove all tick data for the given contract and date.</remarks>
    /// <param name = "contractId">The unique identifier of the futures contract whose tick data is to be deleted. Cannot be null or empty.</param>
    /// <param name = "valueDate">The date for which the futures tick data should be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
        var scopeKey = MarketDataDbContextExtensions.GetFuturesTickScopeKey(contractId, valueDate);
        await this.ExecuteGuardedAtomicTickMutationAsync(scopeKey, db => [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesTickData)}", MarketDataDbCql.DeleteFuturesTickData)
            .SetParameters(new DeleteFuturesTickData(contractId, valueDate))
            .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesTickDataByTime)}", MarketDataDbCql.DeleteFuturesTickDataByTime)
            .SetParameters(new DeleteFuturesTickDataByTime(contractId, valueDate))
            .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)}", MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)
            .SetParameters(new MarkMarketDataProjectionScopeAtomicWriteV3(FuturesTickByTimeProjection, scopeKey, Guid.NewGuid()))
            .QueueCommand()]);
    }

    /// <summary>
    /// Deletes a trade live feed record from the database.
    /// </summary>
    /// <param name = "orderId">The order identifier.</param>
    /// <param name = "tradeId">The trade identifier.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteTradeLiveFeed)}", MarketDataDbCql.DeleteTradeLiveFeed)
        .SetParameters(new DeleteTradeLiveFeed(orderId, tradeId))
        .ExecuteCommandAsync();
    /// <summary>
    /// Deletes futures tick data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task DeleteVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        await this.ExecuteMaintainedProjectionMutationAsync(VixFuturesContractIndexProjection, new[] { MarketDataDbContextExtensions.GetVixContractIndexScopeKey(contractId) }, async () =>
        {
            var db = _dbFactory.MarketDataDb;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteVixFuturesEodData)}", MarketDataDbCql.DeleteVixFuturesEodData)
                .SetParameters(new DeleteVixFuturesEodData(contractId, valueDate))
                .ExecuteCommandAsync();
            var remaining = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastVixFuturesEodData)}", MarketDataDbCql.GetLastVixFuturesEodData)
                .SetParameters(new GetLastVixFuturesEodData(contractId, DateOnly.MaxValue))
                .ExecuteSingleAsync(MapToVixFuturesEodData);
            if (remaining is null)
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteVixFuturesContractIndex)}", MarketDataDbCql.DeleteVixFuturesContractIndex)
                    .SetParameters(new DeleteVixFuturesContractIndex(MarketDataDbContextExtensions.GetVixContractBucket(contractId), contractId))
                    .ExecuteCommandAsync();
            }
        });
    }

    /// <summary>
    /// Deletes yield curve rate data
    /// </summary>
    /// <param name = "valueDate">The value date to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteYieldCurveRateAsync(DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        await db.ExecuteQueuedCommandsAsync([db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteYieldCurveRate)}", MarketDataDbCql.DeleteYieldCurveRate)
            .SetParameters(new DeleteYieldCurveRate(valueDate))
            .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteYieldCurveRateByDate)}", MarketDataDbCql.DeleteYieldCurveRateByDate)
            .SetParameters(new DeleteYieldCurveRate(valueDate))
            .QueueCommand()]);
    }

    /// <summary>
    /// Deletes futures ITI signal data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    /// <param name = "timePeriod"></param>
    /// <returns></returns>
    public async Task DeleteFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        var existing = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignals)}", MarketDataDbCql.GetFuturesItiSignals)
            .SetParameters(new GetFuturesItiSignals(contractId, valueDate, timePeriod.ToStringFast()))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);
        var scopes = new[]
        {
            MarketDataDbContextExtensions.GetFuturesItiDayScopeKey(contractId, valueDate),
            MarketDataDbContextExtensions.GetFuturesItiMonthScopeKey(contractId, MarketDataDbContextExtensions.ToYearMonth(valueDate))
        }.Concat(existing.Select(row => MarketDataDbContextExtensions.GetFuturesItiTimelineScopeKey(row.ContractId, row.IntrinsicTimeTrend.ToStringFast(), row.IntrinsicTimeMode.ToStringFast(), MarketDataDbContextExtensions.ToYearMonth(row.ValueDate))));
        await this.ExecuteMaintainedProjectionMutationAsync(FuturesItiSignalQueryProjection, scopes, async () =>
        {
            var db = _dbFactory.MarketDataDb;
            List<object> commands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignal)}", MarketDataDbCql.DeleteFuturesItiSignal)
                .SetParameters(new DeleteFuturesItiSignal(contractId, valueDate, timePeriod.ToStringFast()))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiTimeFrameState)}", MarketDataDbCql.DeleteFuturesItiTimeFrameState)
                .SetParameters(new GetFuturesItiTimeFrameState(contractId, timePeriod.ToStringFast(), MarketDataDbContextExtensions.GetFuturesItiCalendarBucketStart(valueDate, timePeriod)))
                .QueueCommand()];
            foreach (var row in existing)
            {
                var rowTrend = row.IntrinsicTimeTrend.ToStringFast();
                var rowMode = row.IntrinsicTimeMode.ToStringFast();
                var rowTimePeriod = row.TimePeriod.ToStringFast();
                var yearMonth = MarketDataDbContextExtensions.ToYearMonth(row.ValueDate);
                commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignalByContractDay)}", MarketDataDbCql.DeleteFuturesItiSignalByContractDay)
                    .SetParameters(new DeleteFuturesItiSignalByContractDay(row.ContractId, row.ValueDate, rowMode, row.SequenceId, rowTimePeriod, rowTrend, row.IntrinsicTimeGroupId))
                    .QueueCommand());
                commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignalByContractMonth)}", MarketDataDbCql.DeleteFuturesItiSignalByContractMonth)
                    .SetParameters(new DeleteFuturesItiSignalByContractMonth(row.ContractId, yearMonth, row.ValueDate, row.SequenceId, rowTimePeriod, rowMode, rowTrend, row.IntrinsicTimeGroupId))
                    .QueueCommand());
                commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.DeleteFuturesItiSignalByTrendModeMonth)
                    .SetParameters(new DeleteFuturesItiSignalByTrendModeMonth(row.ContractId, rowTrend, rowMode, yearMonth, row.ValueDate, row.SequenceId, rowTimePeriod, row.IntrinsicTimeGroupId))
                    .QueueCommand());
            }

            await db.ExecuteQueuedCommandsAsync(commands);
        });
    }

    /// <summary>
    /// Deletes futures option tick data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesOptionTickData)}", MarketDataDbCql.DeleteFuturesOptionTickData)
        .SetParameters(new DeleteFuturesOptionTickData(contractId, valueDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Asynchronously deletes tick price data for a specified futures option contract on a given date.
    /// </summary>
    /// <remarks>This method removes all tick price data associated with the specified contract and date from
    /// the database. Ensure that the contract identifier and date are valid before calling this method.</remarks>
    /// <param name = "contractId">The unique identifier of the futures option contract whose tick price data is to be deleted. Cannot be null or
    /// empty.</param>
    /// <param name = "valueDate">The date for which the tick price data should be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesOptionTickPriceData)}", MarketDataDbCql.DeleteFuturesOptionTickPriceData)
        .SetParameters(new DeleteFuturesOptionTickPriceData(contractId, valueDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Deletes a market holiday record from the database.
    /// </summary>
    /// <param name = "e">The market holiday to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidayAsync(MarketHolidayReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketHoliday)}", MarketDataDbCql.DeleteMarketHoliday)
        .SetParameters(new DeleteMarketHoliday(currencyType: e.CurrencyType.ToStringFast(), holidayDate: e.HolidayDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Deletes market holiday records from the database for a given currency type within a specified date range.
    /// </summary>
    /// <param name = "currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidaysAsync(CurrencyType currencyType) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketHolidays)}", MarketDataDbCql.DeleteMarketHolidays)
        .SetParameters(new DeleteMarketHolidays(currencyType: currencyType.ToStringFast()))
        .ExecuteCommandAsync();
    public async Task DeleteRateOfReturnAsync(string symbol, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteRateOfReturn)}", MarketDataDbCql.DeleteRateOfReturn)
        .SetParameters(new DeleteRateOfReturn(symbol, valueDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Gets the futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <param name = "e">The identifier of the futures closing price to retrieve.</param>
    /// <returns>The <see cref = "FuturesClosingPriceReadModel"/>.</returns>
    public async Task<FuturesClosingPriceReadModel?> GetFuturesClosingPriceAsync(FuturesDataId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesClosingPrice)}", MarketDataDbCql.GetFuturesClosingPrice)
        .SetParameters(new GetFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate))
        .ExecuteSingleAsync(MapToFuturesClosingPrice!);
    /// <summary>
    /// Gets yesterday's futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <param name = "id">The identifier of the futures closing price to retrieve.</param>
    /// <returns>The <see cref = "FuturesClosingPriceReadModel"/>.</returns>
    public async Task<FuturesClosingPriceReadModel?> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYesterdaysFuturesClosingPrice)}", MarketDataDbCql.GetYesterdaysFuturesClosingPrice)
        .SetParameters(new GetYesterdaysFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate)).ExecuteSingleAsync<FuturesClosingPriceReadModel>(MapToFuturesClosingPrice!);
    /// <summary>
    /// get futures tick data   
    /// </summary>
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickData)}", MarketDataDbCql.GetFuturesTickData)
        .SetParameters(new GetFuturesTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
        .ExecuteSingleAsync(MapToFuturesTickData!);
    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTickData)}", MarketDataDbCql.GetLastFuturesTickData)
        .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
        .ExecuteSingleAsync(MapToFuturesTickData!);
    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "tickDate"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetFuturesTickAtOrBeforeAsync(string contractId, DateOnly valueDate, TimeOnly tickTime, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var stamp = await this.GetProjectionScopeReadStampAsync(FuturesTickByTimeProjection, new[] { MarketDataDbContextExtensions.GetFuturesTickScopeKey(contractId, valueDate) }).WaitAsync(token);
        // Partial/stale time indexes cannot establish a contiguous historical seed.
        if (stamp is null)
            return null;
        var result = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickAtOrBefore)}", MarketDataDbCql.GetFuturesTickAtOrBefore)
            .SetParameters(new GetLastFuturesTickDataByTickTime(contractId, valueDate, tickTime))
            .ExecuteSingleAsync(MapToFuturesTickData!).WaitAsync(token);
        return await this.IsProjectionScopeReadStampValidAsync(stamp.Value).WaitAsync(token) ? result : null;
    }

    public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate)
    {
        var db = _dbFactory.MarketDataDb;
        var valueDate = DateOnly.FromDateTime(tickDate);
        var tickTime = TimeOnly.FromDateTime(tickDate);
        var stamp = await this.GetProjectionScopeReadStampAsync(FuturesTickByTimeProjection, new[] { MarketDataDbContextExtensions.GetFuturesTickScopeKey(contractId, valueDate) });
        if (stamp is not null)
        {
            var projected = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTickDataByTickTime)}", MarketDataDbCql.GetLastFuturesTickDataByTickTime)
                .SetParameters(new GetLastFuturesTickDataByTickTime(contractId, valueDate, tickTime))
                .ExecuteSingleAsync(MapToFuturesTickData!);
            if (await this.IsProjectionScopeReadStampValidAsync(stamp.Value))
                return projected;
        }

        return (await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickDataByDate)}", MarketDataDbCql.GetFuturesTickDataByDate)
            .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
            .ExecuteQueryAsync(MapToFuturesTickData!)).Where(e => e.TickTime == tickTime).OrderByDescending(e => e.TickId).FirstOrDefault();
    }

    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesOptionTickData)}", MarketDataDbCql.GetLastFuturesOptionTickData)
        .SetParameters(new GetLastFuturesOptionTickData(contractId, valueDate))
        .ExecuteSingleAsync(MapToFuturesOptionTickData!);
    /// <summary>
    /// Asynchronously retrieves the most recent tick price data for a specified futures option contract on a given
    /// date.
    /// </summary>
    /// <remarks>This method queries the market data database for the latest available tick price information
    /// for the given contract and date. Ensure that the contract identifier and date are valid to avoid
    /// exceptions.</remarks>
    /// <param name = "contractId">The unique identifier of the futures option contract for which to retrieve tick price data. Cannot be null or
    /// empty.</param>
    /// <param name = "valueDate">The date for which to retrieve the tick price data. Must be a valid date.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the last tick price data for the
    /// specified contract and date, or null if no data is found.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesOptionTickPriceData)}", MarketDataDbCql.GetLastFuturesOptionTickPriceData)
        .SetParameters(new GetLastFuturesOptionTickPriceData(contractId, valueDate))
        .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!);
    /// <summary>
    /// get futures tick data id    
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTickData)}", MarketDataDbCql.GetLastFuturesTickData)
        .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
        .ExecuteSingleAsync(MapToFuturesTickDataId);
    /// <summary>
    /// Gets the futures bar data for a given contractId, symbol, valueDate, startDate, and endDate.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "symbol">The symbol.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "startDate">The start date.</param>
    /// <param name = "endDate">The end date.</param>
    /// <returns>A collection of <see cref = "FuturesBarDataReadModel"/>.</returns>
    public async Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        var futuresBarData = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesBarData)}", MarketDataDbCql.GetFuturesBarData)
            .SetParameters(new GetFuturesBarData(contractId, symbol, valueDate, startDate, endDate))
            .ExecuteQueryAsync(MapToFuturesBarData!);
        return [.. futuresBarData.OrderBy(e => e.BarDate)];
    }

    /// <summary>
    /// gets all futures bar data.
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync()
    {
        var futuresBarData = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesBarDataAll)}", MarketDataDbCql.GetFuturesBarDataAll)
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
        var lastFuturesBarData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesBarData)}", MarketDataDbCql.GetLastFuturesBarData)
            .SetParameters(new GetLastFuturesBarData(contractId, symbol, valueDate))
            .ExecuteSingleAsync(MapToFuturesBarData!);
        return lastFuturesBarData!;
    }

    /// <summary>
    /// Gets the count of futures bar data for a given FuturesBarDataId.
    /// </summary>
    /// <param name = "e">The futures bar data identifier.</param>
    /// <returns>The count of futures bar data.</returns>
    public async Task<int> GetFuturesBarDataCountAsync(FuturesBarDataId e) => Convert.ToInt32(await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesBarDataCount)}", MarketDataDbCql.GetFuturesBarDataCount)
        .SetParameters(new GetFuturesBarDataCount(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate))
        .ExecuteScalarAsync(MapToFuturesBarDataCount!));
    /// <summary>
    /// Gets a collection of futures ITI signals for a given entity ID.
    /// </summary>
    /// <param name = "e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(FuturesItiSignalEntityId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignals)}", MarketDataDbCql.GetFuturesItiSignals)
        .SetParameters(new GetFuturesItiSignals(contractId: e.ContractId, valueDate: e.ValueDate, timePeriod: e.TimePeriod.ToStringFast()))
        .ExecuteQueryAsync(MapToFuturesItiSignal!);
    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name = "symbol">The symbol.</param>
    /// <param name = "startDate">The start date.</param>
    /// <param name = "endDate">The end date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            return [];
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(static contract => contract.ContractId).ToHashSet(StringComparer.Ordinal);
        var db = _dbFactory.MarketDataDb;
        var valueDates = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1).Select(startDate.AddDays);
        foreach (var batch in valueDates.Chunk(ProjectionReadConcurrency))
        {
            var reads = batch.Select(valueDate => db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalContractIdsByDate)}", MarketDataDbCql.GetFuturesItiSignalContractIdsByDate)
                .SetParameters(new GetFuturesItiSignalContractIdsByDate(valueDate))
                .ExecuteQueryAsync(static row => row.GetString(0)));
            foreach (var indexedContractIds in await Task.WhenAll(reads))
                contractIds.UnionWith(indexedContractIds.Where(contractId => MarketDataDbContextExtensions.IsFuturesContractForSymbol(contractId, symbol)));
        }

        return await this.ReadFuturesItiSignalsByDateRangeAsync(contractIds, startDate, endDate);
    }

    /// <summary>
    /// Gets futures ITI signals for one concrete futures contract and date range.
    /// This avoids the securities-symbol lookup used by the cross-contract query.
    /// </summary>
    public Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsForContractAsync(string contractId, DateOnly startDate, DateOnly endDate) => this.ReadFuturesItiSignalsByDateRangeAsync([contractId], startDate, endDate);
    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name = "symbol"></param>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(static contract => contract.ContractId).Distinct(StringComparer.Ordinal).ToArray();
        var modes = this.GetIntrinsicTimeModes().ToHashSet(StringComparer.Ordinal);
        var futuresItiSignals = await this.ReadFuturesItiSignalsByDateRangeAsync(contractIds, startDate, endDate);
        return [.. futuresItiSignals.Where(signal => modes.Contains(signal.IntrinsicTimeMode.ToStringFast())).OrderBy(static signal => signal.ValueDate).ThenBy(static signal => signal.SequenceId)];
    }

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name = "symbol"></param>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(static contract => contract.ContractId).Distinct(StringComparer.Ordinal).ToArray();
        var modes = this.GetIntrinsicTimeModes().ToHashSet(StringComparer.Ordinal);
        var futuresItiSignals = await this.ReadFuturesItiSignalsByDateRangeAsync(contractIds, startDate, endDate);
        return [.. futuresItiSignals.Where(signal => modes.Contains(signal.IntrinsicTimeMode.ToStringFast())).OrderBy(static signal => signal.ValueDate).ThenBy(static signal => signal.SequenceId)];
    }

    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name = "e">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(FuturesBarDataReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBarData)}", MarketDataDbCql.InsertFuturesBarData)
        .SetParameters(new InsertFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate, barDate: e.BarDate, barRateType: e.BarRateType.ToStringFast(), barValue: e.BarValue, upTrendTrigger: e.UpTrendTrigger, downTrendTrigger: e.DownTrendTrigger))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name = "futuresBarData">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(ICollection<FuturesBarDataReadModel> futuresBarData) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBarData)}", MarketDataDbCql.InsertFuturesBarData)
        .SetParameters(futuresBarData.Select(e => new InsertFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate, barDate: e.BarDate, barRateType: e.BarRateType.ToStringFast(), barValue: e.BarValue, upTrendTrigger: e.UpTrendTrigger, downTrendTrigger: e.DownTrendTrigger)))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a collection of futures bar data into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided futures bar data and inserts it into the database using a
    /// batch operation.  The <paramref name = "futuresBarData"/> collection is enumerated to count the rows and prepare
    /// the data for insertion.</remarks>
    /// <param name = "futuresBarData">A collection of <see cref = "FuturesBarDataReadModel"/> objects representing the futures bar data to be inserted.
    /// Each object must contain valid values for contract ID, symbol, value date, bar date, bar rate type, bar value, 
    /// up trend trigger, and down trend trigger.</param>
    /// <returns>A <see cref = "Task{TResult}"/> representing the asynchronous operation. The result contains the total number  of
    /// rows processed during the insertion.</returns>
    public async Task<long> InsertFuturesBarDataAsync(IEnumerable<FuturesBarDataReadModel> futuresBarData)
    {
        long rowCount = 0;
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBarData)}", MarketDataDbCql.InsertFuturesBarData)
            .SetParameters(GetFuturesBarData().Select(e => new InsertFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate, barDate: e.BarDate, barRateType: e.BarRateType.ToStringFast(), barValue: e.BarValue, upTrendTrigger: e.UpTrendTrigger, downTrendTrigger: e.DownTrendTrigger)))
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
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesClosingPrice)}", MarketDataDbCql.InsertFuturesClosingPrice)
        .SetParameters(new InsertFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate, closingPrice: e.ClosingPrice, createdOn: e.CreatedOn, createdBy: e.CreatedBy))
        .ExecuteCommandAsync();
    /// <summary>
    /// 
    /// </summary>
    /// <param name = "tickData"></param>
    /// <returns></returns>
    public async Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel e)
    {
        var tickId = e.TickId > 0 ? e.TickId : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTickData_TickId);
        await this.ExecuteAtomicTickWriteAsync(MarketDataDbContextExtensions.GetFuturesTickScopeKey(e.ContractId, e.ValueDate), new[] { new InsertFuturesTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId, tickTime: e.TickTime, price: e.Price, size: e.Size) }, new[] { new InsertFuturesTickDataByTime(contractId: e.ContractId, valueDate: e.ValueDate, tickTime: e.TickTime, tickId, price: e.Price, size: e.Size) });
    }

    /// <summary>
    /// insert futures tick data collection
    /// </summary>
    /// <param name = "tickData"></param>
    /// <returns></returns>
    public async Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> tickData)
    {
        if (tickData.Count == 0)
            return;
        MarketDataDbContextExtensions.EnsureDistinctFuturesTickWrites(tickData);
        var batchesByGuard = tickData.GroupBy(static e => (e.ContractId, e.ValueDate)).SelectMany(group => group.Chunk(TickAtomicBatchRowCount).Select(chunk => (group.Key.ContractId, group.Key.ValueDate, Rows: chunk))).GroupBy(batch => MarketDataDbContextExtensions.GetProjectionGuardScopeKey(MarketDataDbContextExtensions.GetFuturesTickScopeKey(batch.ContractId, batch.ValueDate)));
        foreach (var guardGroupBatch in batchesByGuard.Chunk(ProjectionReadConcurrency))
        {
            await Task.WhenAll(guardGroupBatch.Select(async guardBatches =>
            {
                foreach (var batch in guardBatches)
                {
                    await this.ExecuteAtomicTickWriteAsync(MarketDataDbContextExtensions.GetFuturesTickScopeKey(batch.ContractId, batch.ValueDate), batch.Rows.Select(e => new InsertFuturesTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId, tickTime: e.TickTime, price: e.Price, size: e.Size)).ToArray(), batch.Rows.Select(e => new InsertFuturesTickDataByTime(contractId: e.ContractId, valueDate: e.ValueDate, tickTime: e.TickTime, tickId: e.TickId, price: e.Price, size: e.Size)).ToArray());
                }
            }));
        }
    }

    /// <summary>
    /// Inserts a new futures ITI signal record into the database.
    /// </summary>
    /// <param name = "e">The futures ITI signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesItiSignalAsync(FuturesItiSignalV2ReadModel e)
    {
        var trend = e.IntrinsicTimeTrend.ToStringFast();
        var mode = e.IntrinsicTimeMode.ToStringFast();
        await this.ExecuteMaintainedProjectionMutationAsync(FuturesItiSignalQueryProjection, MarketDataDbContextExtensions.GetFuturesItiProjectionScopeKeys(e.ContractId, e.ValueDate, trend, mode), async () =>
        {
            var sequenceId = e.SequenceId > 0 ? e.SequenceId : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesItiSignal_SequenceId);
            var db = _dbFactory.MarketDataDb;
            var canonicalParameters = MarketDataDbContextExtensions.CreateFuturesItiSignalParameters(e, sequenceId);
            var monthParameters = MarketDataDbContextExtensions.CreateFuturesItiSignalMonthParameters(e, sequenceId);
            List<object> commands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalIndex)}", MarketDataDbCql.InsertFuturesItiSignalIndex)
                .SetParameters(new InsertFuturesItiSignalIndex(e.ValueDate, e.ContractId))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignal)}", MarketDataDbCql.InsertFuturesItiSignal)
                .SetParameters(canonicalParameters)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractDay)}", MarketDataDbCql.InsertFuturesItiSignalByContractDay)
                .SetParameters(canonicalParameters)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractMonth)}", MarketDataDbCql.InsertFuturesItiSignalByContractMonth)
                .SetParameters(monthParameters)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)
                .SetParameters(monthParameters)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpsertFuturesItiTimeFrameState)}", MarketDataDbCql.UpsertFuturesItiTimeFrameState)
                .SetParameters(MarketDataDbContextExtensions.CreateFuturesItiTimeFrameStateParameters(e, sequenceId))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(new InsertMarketDataProjectionMonth(FuturesItiSignalQueryProjection, MarketDataDbContextExtensions.ToYearMonth(e.ValueDate)))
                .QueueCommand()];
            await db.ExecuteQueuedCommandsAsync(commands);
        });
    }

    public async Task<FuturesItiSignalV2ReadModel?> GetFuturesItiTimeFrameStateAsync(string contractId, TimeFrameType timePeriod, DateOnly calendarBucketStart, CancellationToken cancellationToken = default) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTimeFrameState)}", MarketDataDbCql.GetFuturesItiTimeFrameState)
        .SetParameters(new GetFuturesItiTimeFrameState(contractId, timePeriod.ToStringFast(), calendarBucketStart))
        .ExecuteSingleAsync(MapToFuturesItiTimeFrameState!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Inserts a Futures RSI Signal asynchronously into the database.
    /// </summary>
    /// <param name = "futuresRsiSignal">The Futures RSI Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel e)
    {
        var parameters = new InsertFuturesRsiSignal(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), e.PeriodLength, e.Timestamp, e.Price, e.PriceChange, e.PriceGain, e.PriceLoss, e.AveragePriceGain, e.AveragePriceLoss, e.RS, e.RSI, e.RSIAverage, e.RSISlope, e.SourceSequence, e.SourceEventTimestamp, e.Metadata?.CalculationConfigurationId, e.Metadata?.ObservationId.Value, e.Metadata?.MarketDataAsOfUtc.UtcDateTime, e.Metadata?.CalculationVersion, e.Metadata?.CalculationMethod.ToString(), e.Metadata is { } rsiMetadata ? rsiMetadata.SchemaVersion : null, e.Metadata?.IsValid, e.PreviousRsi, e.RegimeSlope, e.IsWarm);
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesRsiSignal)}", MarketDataDbCql.InsertFuturesRsiSignal)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a single yield curve rate record into the database.
    /// </summary>
    /// <param name = "e">The YieldCurveRateReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e) => InsertYieldCurveRatesAsync([e]);
    /// <summary>
    /// Inserts a collection of yield curve rate records into the database.
    /// </summary>
    /// <param name = "e">The collection of YieldCurveRateReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertYieldCurveRatesAsync(YieldCurveRateReadModel[] e) => await InsertYieldCurveRatesAsync(e, ImportDuplicatePolicy.Overwrite, Guid.Empty);
    public async Task InsertYieldCurveRatesAsync(YieldCurveRateReadModel[] e, ImportDuplicatePolicy duplicatePolicy, Guid commandId)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Length == 0)
            return;
        MarketDataDbContextExtensions.ValidateImportPolicy(duplicatePolicy, commandId);
        var db = _dbFactory.MarketDataDb;
        var commands = new List<object>(e.Length * 3);
        var years = new HashSet<int>();
        foreach (var row in e)
        {
            if (duplicatePolicy == ImportDuplicatePolicy.Reject)
            {
                await this.EnsureImportOwnershipAsync("treasury-curve", row.ValueDate.ToString("yyyy-MM-dd"), commandId, await GetYieldCurveRateAsync(row.ValueDate)
                    .ConfigureAwait(false) is not null)
                    .ConfigureAwait(false);
            }

            var parameters = new InsertYieldCurveRate(id: YieldCurveLookupId, valueDate: row.ValueDate, oneMonth: row.OneMonth, twoMonth: row.TwoMonth, threeMonth: row.ThreeMonth, sixMonth: row.SixMonth, oneYear: row.OneYear, twoYear: row.TwoYear, threeYear: row.ThreeYear, fiveYear: row.FiveYear, sevenYear: row.SevenYear, tenYear: row.TenYear, twentyYear: row.TwentyYear, thirtyYear: row.ThirtyYear);
            commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRate)}", MarketDataDbCql.InsertYieldCurveRate)
                .SetParameters(parameters)
                .QueueCommand());
            commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRateByDate)}", MarketDataDbCql.InsertYieldCurveRateByDate)
                .SetParameters(parameters)
                .QueueCommand());
            years.Add(row.ValueDate.Year);
        }

        commands.AddRange(years.Select(rateYear => db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRateYear)}", MarketDataDbCql.InsertYieldCurveRateYear)
            .SetParameters(new InsertYieldCurveRateYear(YieldCurveLookupId, rateYear))
            .QueueCommand()));
        await db.ExecuteQueuedCommandsAsync(commands);
    }

    /// <summary>
    /// Gets the last FuturesOptionTickDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>The last <see cref = "FuturesOptionTickDataId"/>.</returns>
    public async Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesOptionTickDataId)}", MarketDataDbCql.GetLastFuturesOptionTickDataId)
        .SetParameters(new GetLastFuturesOptionTickDataId(contractId, valueDate))
        .ExecuteSingleAsync(MapToFuturesOptionTickDataId!);
    /// <summary>
    /// Gets the FuturesOptionTickDataV2ReadModel for a given FuturesOptionTickDataId.
    /// </summary>
    /// <param name = "e">The futures option tick data identifier.</param>
    /// <returns>The <see cref = "FuturesOptionTickDataV2ReadModel"/>.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesOptionTickData)}", MarketDataDbCql.GetFuturesOptionTickData)
        .SetParameters(new GetFuturesOptionTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
        .ExecuteSingleAsync(MapToFuturesOptionTickData!);
    /// <summary>
    /// Asynchronously retrieves the tick price data for a specified futures option.
    /// </summary>
    /// <remarks>This method queries the market data database for the latest tick price information associated
    /// with the provided identifier. Ensure that the identifier is valid to avoid unexpected results.</remarks>
    /// <param name = "e">An identifier that specifies the futures option tick data to retrieve. This includes the contract ID, value
    /// date, and tick ID. Must represent a valid futures option tick.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the tick price data for the
    /// specified futures option, or null if no matching data is found.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickPriceDataAsync(FuturesOptionTickDataId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesOptionTickPriceData)}", MarketDataDbCql.GetFuturesOptionTickPriceData)
        .SetParameters(new GetFuturesOptionTickPriceData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
        .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!);
    /// <summary>
    /// Inserts a single FuturesOptionTickData into the database.
    /// </summary>
    /// <param name = "e">The futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel e)
    {
        var tickId = e.TickId > 0 ? e.TickId : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesOptionTickData_TickId);
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesOptionTickData)}", MarketDataDbCql.InsertFuturesOptionTickData)
            .SetParameters(new InsertFuturesOptionTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId, tickTime: e.TickTime, optionPrice: e.OptionPrice, bidPrice: e.BidPrice, askPrice: e.AskPrice, bidSize: e.BidSize, askSize: e.AskSize, impliedVolatility: e.ImpliedVolatility, underlyingPrice: e.UnderlyingPrice, delta: e.Delta, gamma: e.Gamma, vega: e.Vega, theta: e.Theta, rho: e.Rho))
            .ExecuteCommandAsync();
    }

    public async Task InsertFuturesOptionTickPriceDataAsync(FuturesOptionTickDataV2ReadModel e)
    {
        var tickId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesOptionTickPriceData_TickId);
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesOptionTickPriceData)}", MarketDataDbCql.InsertFuturesOptionTickPriceData)
            .SetParameters(new InsertFuturesOptionTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId, tickTime: e.TickTime, optionPrice: e.OptionPrice, bidPrice: e.BidPrice, askPrice: e.AskPrice, bidSize: e.BidSize, askSize: e.AskSize, impliedVolatility: e.ImpliedVolatility, underlyingPrice: e.UnderlyingPrice, delta: e.Delta, gamma: e.Gamma, vega: e.Vega, theta: e.Theta, rho: e.Rho))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a collection of FuturesOptionTickDataV2ReadModel into the database.
    /// </summary>
    /// <param name = "tickData">The collection of futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> tickData) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesOptionTickData)}", MarketDataDbCql.InsertFuturesOptionTickData)
        .SetParameters(tickData.Select(e => new InsertFuturesOptionTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId, tickTime: e.TickTime, optionPrice: e.OptionPrice, bidPrice: e.BidPrice, askPrice: e.AskPrice, bidSize: e.BidSize, askSize: e.AskSize, impliedVolatility: e.ImpliedVolatility, underlyingPrice: e.UnderlyingPrice, delta: e.Delta, gamma: e.Gamma, vega: e.Vega, theta: e.Theta, rho: e.Rho)))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a trade live feed record into the database.
    /// </summary>
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertTradeLiveFeed)}", MarketDataDbCql.InsertTradeLiveFeed)
        .SetParameters(new InsertTradeLiveFeed(orderId: e.OrderId, tradeId: e.TradeId, tradeLiveFeedState: e.TradeLiveFeedState.ToStringFast()))
        .ExecuteCommandAsync();
    /// <summary>
    /// Gets the FuturesDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesDataId.</returns>
    public async Task<FuturesDataId?> GetFuturesDataId(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesDataId)}", MarketDataDbCql.GetFuturesDataId)
        .SetParameters(new GetFuturesDataId(contractId, valueDate))
        .ExecuteSingleAsync(MapToFuturesDataId); // Map the result to FuturesDataId
    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given FuturesDataId.
    /// </summary>
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickHLVData)}", MarketDataDbCql.GetFuturesTickHLVData)
        .SetParameters(new GetFuturesTickHLVData(contractId: e.ContractId, valueDate: e.ValueDate))
        .ExecuteSingleAsync(MapToFuturesTickHLVData!);
    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task<FuturesTickHLVDataReadModel?> GetVixFuturesTickHLVDataAsync(VixFuturesEodDataEntityId e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickHLVData)}", MarketDataDbCql.GetFuturesTickHLVData)
        .SetParameters(new GetFuturesTickHLVData(contractId: e.ContractId, valueDate: e.ValueDate))
        .ExecuteSingleAsync(MapToFuturesTickHLVData!);
    /// <summary>
    /// Gets the FuturesEodDataV2ReadModel for a given contractId and valueDate.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var futuresEodData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodData)}", MarketDataDbCql.GetFuturesEodData)
            .SetParameters(new GetFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesEodData!);
        futuresEodData ??= await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYesterdaysFuturesEodData)}", MarketDataDbCql.GetYesterdaysFuturesEodData)
            .SetParameters(new GetYesterdaysFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesEodData!);
        return futuresEodData;
    }

    /// <summary>
    /// Asynchronously retrieves intra-day market data for a specified futures contract on a given date.
    /// </summary>
    /// <remarks>This method performs an asynchronous database query to fetch the requested data. Ensure that
    /// the provided contract identifier and date are valid to avoid exceptions.</remarks>
    /// <param name = "contractId">The unique identifier of the futures contract for which to retrieve intra-day data. This parameter cannot be
    /// null or empty.</param>
    /// <param name = "valueDate">The date for which the intra-day market data is requested.</param>
    /// <returns>A collection of <see cref = "FuturesIntraDayDataReadModel"/> objects representing the intra-day market data for
    /// the specified contract and date.</returns>
    public async Task<ICollection<FuturesIntraDayDataReadModel>> GetFuturesIntraDayDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesIntraDayData)}", MarketDataDbCql.GetFuturesIntraDayData)
        .SetParameters(new GetFuturesIntraDayData(contractId, valueDate))
        .ExecuteQueryAsync(MapToFuturesIntraDayData!);
    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain the latest available futures data at
    /// the end of the trading day.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see
    ///cref = "FuturesEodDataV2ReadModel"/> representing the latest end-of-day futures data, or <see langword="null"/> if
    /// no data is available.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesEodData)}", MarketDataDbCql.GetLastFuturesEodData)
        .SetParameters(new GetLastFuturesEodData(contractId, valueDate))
        .ExecuteSingleAsync(MapToFuturesEodData!);
    /// <summary>
    /// Asynchronously retrieves a collection of end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain all available end-of-day futures
    /// data.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a collection of  <see
    ///cref = "FuturesEodDataV2ReadModel"/> objects representing the end-of-day futures data.</returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync() => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
        .ExecuteQueryAsync(MapToFuturesEodData!);
    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given contractId and date range.
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateOnly startDate, DateOnly endDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataByDateRange)}", MarketDataDbCql.GetFuturesEodDataByDateRange)
        .SetParameters(new GetFuturesEodDataByDateRange(contractId, startDate, endDate))
        .ExecuteQueryAsync(MapToFuturesEodData!);
    /// <summary>
    /// Gets the current FuturesEodDataV2ReadModel for a given FuturesDataId.
    /// </summary>
    /// <param name = "e">The FuturesDataId containing the contractId and valueDate.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetCurrentFuturesEodDataAsync(DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var targetYearMonth = MarketDataDbContextExtensions.ToYearMonth(valueDate);
        var projectionMonths = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMonths)}", MarketDataDbCql.GetMarketDataProjectionMonths)
            .SetParameters(new GetMarketDataProjectionMonths(FuturesEodProjection, targetYearMonth))
            .ExecuteQueryAsync(MapToYearMonth);
        var orderedProjectionMonths = projectionMonths.ToArray();
        var stamp = await this.GetProjectionScopeReadStampAsync(FuturesEodProjection, orderedProjectionMonths.Select(MarketDataDbContextExtensions.GetFuturesEodScopeKey).Concat(this.GetProjectionGuardScopeKeys()));
        if (stamp is null)
            return await this.ReadLegacyCurrentFuturesEodDataAsync(valueDate);
        FuturesEodDataV2ReadModel? result = null;
        foreach (var yearMonth in orderedProjectionMonths)
        {
            var monthCutoff = yearMonth == targetYearMonth ? valueDate : MarketDataDbContextExtensions.GetMonthEnd(yearMonth);
            result = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetCurrentFuturesEodDataByMonth)}", MarketDataDbCql.GetCurrentFuturesEodDataByMonth)
                .SetParameters(new GetCurrentFuturesEodDataByMonth(yearMonth, monthCutoff))
                .ExecuteSingleAsync(MapToFuturesEodData!);
            if (result is not null)
                break;
        }

        var validatedProjectionMonths = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMonths)}", MarketDataDbCql.GetMarketDataProjectionMonths)
            .SetParameters(new GetMarketDataProjectionMonths(FuturesEodProjection, targetYearMonth))
            .ExecuteQueryAsync(MapToYearMonth);
        if (orderedProjectionMonths.SequenceEqual(validatedProjectionMonths) && await this.IsProjectionScopeReadStampValidAsync(stamp.Value))
        {
            return result;
        }

        return await this.ReadLegacyCurrentFuturesEodDataAsync(valueDate);
    }

    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given date range.
    /// </summary>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetCurrentFuturesEodDataByDateRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var yearMonths = MarketDataDbContextExtensions.GetYearMonths(startDate, endDate).ToHashSet();
        var stamp = await this.GetProjectionScopeReadStampAsync(FuturesEodProjection, yearMonths.Select(MarketDataDbContextExtensions.GetFuturesEodScopeKey));
        if (stamp is null)
            return await this.ReadLegacyFuturesEodDataByMonthsAsync(startDate, endDate, yearMonths);
        List<FuturesEodDataV2ReadModel> results = [];
        foreach (var yearMonth in yearMonths)
        {
            var monthStart = MarketDataDbContextExtensions.GetMonthStart(yearMonth);
            var monthEnd = MarketDataDbContextExtensions.GetMonthEnd(yearMonth);
            var rangeStart = startDate > monthStart ? startDate : monthStart;
            var rangeEnd = endDate < monthEnd ? endDate : monthEnd;
            var monthValues = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetCurrentFuturesEodDataByDateRange)}", MarketDataDbCql.GetCurrentFuturesEodDataByDateRange)
                .SetParameters(new GetCurrentFuturesEodDataByDateRange(yearMonth, rangeStart, rangeEnd))
                .ExecuteQueryAsync(MapToFuturesEodData!);
            results.AddRange(monthValues);
        }

        if (!await this.IsProjectionScopeReadStampValidAsync(stamp.Value))
            return await this.ReadLegacyFuturesEodDataByMonthsAsync(startDate, endDate, yearMonths);
        return [.. results.OrderByDescending(e => e.ValueDate).ThenBy(e => e.ContractId)];
    }

    /// <summary>
    /// Gets the FuturesEodMovingAverageReadModel for a given symbol and date range.
    /// </summary>
    /// <param name = "symbol">The symbol.</param>
    /// <param name = "startDate">The start date.</param>
    /// <param name = "endDate">The end date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodMovingAverageReadModel.</returns>
    public async Task<FuturesEodMovingAverageReadModel?> GetFuturesEodMovingAverageAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        var values = (await GetCurrentFuturesEodDataByDateRangeAsync(DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate))).Where(e => string.Equals(e.Symbol, symbol, StringComparison.Ordinal)).Select(e => e.ClosePrice).ToArray();
        return values.Length == 0 ? null : new FuturesEodMovingAverageReadModel(symbol, (double)values.Average());
    }

    /// <summary>
    /// Gets a collection of FuturesEodClosingPriceReadModel for a given symbol and date range, limited by maxDays.
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "symbol">The symbol.</param>
    /// <param name = "startDate">The start date.</param>
    /// <param name = "endDate">The end date.</param>
    /// <param name = "maxDays">The maximum number of days to retrieve.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of FuturesEodClosingPriceReadModel.</returns>
    public async Task<ICollection<FuturesEodClosingPriceReadModel>> GetFuturesEodClosingPricesAsync(string contractId, string symbol, DateOnly startDate, DateOnly endDate, int maxDays)
    {
        if (maxDays <= 0)
            return [];
        var closingPrices = new List<FuturesEodClosingPriceReadModel>(Math.Min(maxDays, 256));
        var rows = _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodClosingPrices)}", MarketDataDbCql.GetFuturesEodClosingPrices)
            .SetParameters(new GetFuturesEodClosingPrices(contractId, startDate, endDate))
            .ExecuteStreamAsync(MapToFuturesEodClosingPrice);
        await foreach (var row in rows.ConfigureAwait(false))
        {
            if (!string.Equals(row.Symbol, symbol, StringComparison.Ordinal))
                continue;
            closingPrices.Add(row);
            if (closingPrices.Count == maxDays)
                break;
        }

        return closingPrices;
    }

    /// <summary>
    /// return futures iti trend delta data by date range
    /// </summary>
    /// <param name = "symbol"></param>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    public async Task<ICollection<FuturesItiTrendDeltaDataReadModel>> GetFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendDeltaData)}", MarketDataDbCql.GetFuturesItiTrendDeltaData)
        .SetParameters(new GetFuturesItiTrendDeltaData(symbol, startDate, endDate))
        .ExecuteQueryAsync(MapToFuturesItiTrendDeltaData);
    /// <summary>
    /// return futures iti trend class data by date range
    /// </summary>
    /// <param name = "symbol"></param>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiTrendClassDataReadModel>> GetFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendClassData)}", MarketDataDbCql.GetFuturesItiTrendClassData)
        .SetParameters(new GetFuturesItiTrendClassData(symbol, startDate, endDate))
        .ExecuteQueryAsync(MapToFuturesItiTrendClassData);
    /// <summary>
    /// return futures iti trend delta model
    /// </summary>
    /// <param name = "symbol"></param>
    /// <param name = "valueDate"></param>
    public async Task<FuturesItiTrendDeltaModelReadModel> GetFuturesItiTrendDeltaModelAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendDeltaModelMaxValueDate)}", MarketDataDbCql.GetFuturesItiTrendDeltaModelMaxValueDate)
            .SetParameters(new GetFuturesItiTrendDeltaModelMaxValueDate(symbol, valueDate))
            .ExecuteScalarAsync(MapToMaxValueDate);
        return await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendDeltaModel)}", MarketDataDbCql.GetFuturesItiTrendDeltaModel)
            .SetParameters(new GetFuturesItiTrendDeltaModel(symbol, valueDate: maxValueDate))
            .ExecuteSingleAsync(MapToFuturesItiTrendDeltaModel!);
    }

    /// <summary>
    /// return futures iti trend class model
    /// </summary>
    /// <param name = "symbol"></param>
    /// <param name = "valueDate"></param>
    public async Task<FuturesItiTrendClassModelReadModel> GetFuturesItiTrendClassModelAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendClassModelMaxValueDate)}", MarketDataDbCql.GetFuturesItiTrendClassModelMaxValueDate)
            .SetParameters(new GetFuturesItiTrendClassModelMaxValueDate(symbol, valueDate))
            .ExecuteScalarAsync(MapToMaxValueDate!);
        return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendClassModel)}", MarketDataDbCql.GetFuturesItiTrendClassModel)
            .SetParameters(new GetFuturesItiTrendClassModel(symbol, valueDate: maxValueDate))
            .ExecuteSingleAsync(MapToFuturesItiTrendClassModel!);
    }

    /// <summary>
    /// Inserts a new record into the futures_eod_data_index table if it does not already exist.
    /// </summary>
    /// <param name = "e">The FuturesEodDataIndexReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesEodDataIndexAsync(FuturesEodDataIndexReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataIndex)}", MarketDataDbCql.InsertFuturesEodDataIndex)
        .SetParameters(new InsertFuturesEodDataIndex(valueDate: e.ValueDate, contractId: e.ContractId))
        .ExecuteCommandAsync();
    /// <summary>
    /// insert futures iti trend delta model
    /// </summary>
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendDeltaModel)}", MarketDataDbCql.InsertFuturesItiTrendDeltaModel)
        .SetParameters(new InsertFuturesItiTrendDeltaModel(symbol: e.Symbol, valueDate: e.ValueDate, startDate: e.StartDate, endDate: e.EndDate, count: e.Count, maximum: e.Maximum, mean: e.Mean, median: e.Median, minimum: e.Minimum, skewness: e.Skewness, stdDev: e.StdDev, variance: e.Variance, meanAbsoluteError: e.MeanAbsoluteError, meanSquaredError: e.MeanSquaredError, rootMeanSquaredError: e.RootMeanSquaredError, lossFunction: e.LossFunction, rSquared: e.RSquared, modelData: e.ModelData))
        .ExecuteCommandAsync();
    /// <summary>
    /// insert funtures iti trend class model
    /// </summary>
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendClassModelAsync(FuturesItiTrendClassModelReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendClassModel)}", MarketDataDbCql.InsertFuturesItiTrendClassModel)
        .SetParameters(new InsertFuturesItiTrendClassModel(symbol: e.Symbol, valueDate: e.ValueDate, startDate: e.StartDate, endDate: e.EndDate, count: e.Count, maximum: e.Maximum, mean: e.Mean, median: e.Median, minimum: e.Minimum, skewness: e.Skewness, stdDev: e.StdDev, variance: e.Variance, accuracy: e.Accuracy, areaUnderPrecisionRecallCurve: e.AreaUnderPrecisionRecallCurve, areaUnderRocCurve: e.AreaUnderRocCurve, entropy: e.Entropy, f1Score: e.F1Score, modelData: e.ModelData))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a Futures TDI Signal asynchronously into the database.
    /// </summary>
    /// <param name = "futuresTdiSignal">The Futures TDI Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel futuresTdiSignal) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTdiSignal)}", MarketDataDbCql.InsertFuturesTdiSignal)
        .SetParameters(new InsertFuturesTdiSignal(contractId: futuresTdiSignal.ContractId, timePeriod: futuresTdiSignal.TimePeriod.ToStringFast(), configurationId: futuresTdiSignal.ConfigurationId, valueDate: futuresTdiSignal.ValueDate, timestamp: futuresTdiSignal.Timestamp, schemaVersion: futuresTdiSignal.SchemaVersion, rsiPeriod: futuresTdiSignal.RsiPeriod, priceLinePeriod: futuresTdiSignal.PriceLinePeriod, signalLinePeriod: futuresTdiSignal.SignalLinePeriod, marketBasePeriod: futuresTdiSignal.MarketBasePeriod, volatilityBandPeriod: futuresTdiSignal.VolatilityBandPeriod, volatilityBandDeviation: futuresTdiSignal.VolatilityBandDeviation, price: futuresTdiSignal.Price, rsi: futuresTdiSignal.Rsi, priceLine: futuresTdiSignal.PriceLine, signalLine: futuresTdiSignal.SignalLine, marketBaseLine: futuresTdiSignal.MarketBaseLine, upperVolatilityBand: futuresTdiSignal.UpperVolatilityBand, lowerVolatilityBand: futuresTdiSignal.LowerVolatilityBand, bandWidth: futuresTdiSignal.BandWidth, priceSignalDivergence: futuresTdiSignal.PriceSignalDivergence, crossType: futuresTdiSignal.Cross.ToString(), marketState: futuresTdiSignal.MarketState.ToString(), trendDirection: futuresTdiSignal.TDI.ToStringFast(), trendStrength: futuresTdiSignal.TDIStrength.ToStringFast(), sourceSequence: futuresTdiSignal.SourceSequence, sourceEventTimestamp: futuresTdiSignal.SourceEventTimestamp))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a Futures MACD Signal asynchronously into the database.
    /// </summary>
    /// <param name = "futuresMacdSignal">The Futures MACD Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesMacdSignalAsync(FuturesMacdSignalReadModel futuresMacdSignal) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesMacdSignal)}", MarketDataDbCql.InsertFuturesMacdSignal)
        .SetParameters(new InsertFuturesMacdSignal(contractId: futuresMacdSignal.ContractId, valueDate: futuresMacdSignal.ValueDate, timePeriod: futuresMacdSignal.TimePeriod.ToStringFast(), signalEmaPeriod: futuresMacdSignal.SignalEmaPeriod, fastEmaPeriod: futuresMacdSignal.FastEmaPeriod, slowEmaPeriod: futuresMacdSignal.SlowEmaPeriod, timestamp: futuresMacdSignal.Timestamp, futuresPrice: futuresMacdSignal.FuturesPrice, fastEma: futuresMacdSignal.FastEma, slowEma: futuresMacdSignal.SlowEma, macdLine: futuresMacdSignal.MacdLine, signalLine: futuresMacdSignal.SignalLine, histogram: futuresMacdSignal.Histogram, macd: futuresMacdSignal.MACD.ToStringFast(), macdStrength: futuresMacdSignal.MACDStrength.ToStringFast(), configurationId: futuresMacdSignal.Metadata?.CalculationConfigurationId, observationId: futuresMacdSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresMacdSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresMacdSignal.Metadata?.SourceSequence, calculationVersion: futuresMacdSignal.Metadata?.CalculationVersion, calculationMethod: futuresMacdSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresMacdSignal.Metadata is { } macdMetadata ? macdMetadata.SchemaVersion : null, isValid: futuresMacdSignal.Metadata?.IsValid, isWarm: futuresMacdSignal.IsWarm, observationCount: futuresMacdSignal.ObservationCount))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a Futures ATR Signal asynchronously into the database.
    /// </summary>
    /// <param name = "futuresAtrSignal">The Futures ATR Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesAtrSignalAsync(FuturesAtrSignalReadModel futuresAtrSignal) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesAtrSignal)}", MarketDataDbCql.InsertFuturesAtrSignal)
        .SetParameters(new InsertFuturesAtrSignal(contractId: futuresAtrSignal.ContractId, valueDate: futuresAtrSignal.ValueDate, timePeriod: futuresAtrSignal.TimePeriod.ToStringFast(), periodLength: futuresAtrSignal.PeriodLength, timestamp: futuresAtrSignal.Timestamp, futuresPrice: futuresAtrSignal.FuturesPrice, atrValue: futuresAtrSignal.AtrValue, trueRange: futuresAtrSignal.TrueRange, atr: futuresAtrSignal.ATR.ToStringFast(), atrStrength: futuresAtrSignal.ATRStrength.ToStringFast(), configurationId: futuresAtrSignal.Metadata?.CalculationConfigurationId, observationId: futuresAtrSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAtrSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAtrSignal.Metadata?.SourceSequence, calculationVersion: futuresAtrSignal.Metadata?.CalculationVersion, calculationMethod: futuresAtrSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAtrSignal.Metadata is { } atrMetadata ? atrMetadata.SchemaVersion : null, isValid: futuresAtrSignal.Metadata?.IsValid, previousAtrValue: futuresAtrSignal.PreviousAtrValue, atrBaseline: futuresAtrSignal.AtrBaseline, atrRatio: futuresAtrSignal.AtrRatio, isWarm: futuresAtrSignal.IsWarm))
        .ExecuteCommandAsync();
    /// <summary>
    /// Deletes futures ATR signal data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "timePeriod">The signal time frame.</param>
    /// <param name = "periodLength">The indicator period length.</param>
    public async Task DeleteFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesAtrSignal)}", MarketDataDbCql.DeleteFuturesAtrSignal)
        .SetParameters(new DeleteFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a Futures ADX Signal
    /// </summary>
    /// <param name = "futuresAdxSignal">The Futures ADX Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesAdxSignalAsync(FuturesAdxSignalReadModel futuresAdxSignal) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesAdxSignal)}", MarketDataDbCql.InsertFuturesAdxSignal)
        .SetParameters(new InsertFuturesAdxSignal(contractId: futuresAdxSignal.ContractId, valueDate: futuresAdxSignal.ValueDate, timePeriod: futuresAdxSignal.TimePeriod.ToStringFast(), periodLength: futuresAdxSignal.PeriodLength, timestamp: futuresAdxSignal.Timestamp, futuresPrice: futuresAdxSignal.FuturesPrice, plusDI: futuresAdxSignal.PlusDI, minusDI: futuresAdxSignal.MinusDI, adxValue: futuresAdxSignal.AdxValue, adx: futuresAdxSignal.ADX.ToStringFast(), adxStrength: futuresAdxSignal.ADXStrength.ToStringFast(), configurationId: futuresAdxSignal.Metadata?.CalculationConfigurationId, observationId: futuresAdxSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAdxSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAdxSignal.Metadata?.SourceSequence, calculationVersion: futuresAdxSignal.Metadata?.CalculationVersion, calculationMethod: futuresAdxSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAdxSignal.Metadata is { } metadata ? metadata.SchemaVersion : null, isValid: futuresAdxSignal.Metadata?.IsValid))
        .ExecuteCommandAsync();
    /// <summary>
    /// Deletes futures ADX signal data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "timePeriod">The signal time frame.</param>
    /// <param name = "periodLength">The indicator period length.</param>
    public async Task DeleteFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesAdxSignal)}", MarketDataDbCql.DeleteFuturesAdxSignal)
        .SetParameters(new DeleteFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a futures trade signal
    /// </summary>
    /// <param name = "FuturesTradeSignalV2ReadModel">The futures trade signal view model to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel FuturesTradeSignalV2ReadModel)
    {
        var sequenceId = FuturesTradeSignalV2ReadModel.SequenceId > 0 ? FuturesTradeSignalV2ReadModel.SequenceId : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId);
        var timePeriod = FuturesTradeSignalV2ReadModel.TimePeriod.ToStringFast();
        var db = _dbFactory.MarketDataDb;
        var insertSignal = db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignal)}", MarketDataDbCql.InsertFuturesTradeSignal)
            .SetParameters(new InsertFuturesTradeSignal(contractId: FuturesTradeSignalV2ReadModel.ContractId, valueDate: FuturesTradeSignalV2ReadModel.ValueDate, timePeriod, sequenceId, timestamp: FuturesTradeSignalV2ReadModel.Timestamp, mean: FuturesTradeSignalV2ReadModel.Mean, stdDev: FuturesTradeSignalV2ReadModel.StdDev, futuresPrice: FuturesTradeSignalV2ReadModel.FuturesPrice, priceChangePercent: FuturesTradeSignalV2ReadModel.PriceChangePercent, fundRiskPercent: FuturesTradeSignalV2ReadModel.FundRiskPercent, rsi: FuturesTradeSignalV2ReadModel.RSI, rsiSlope: FuturesTradeSignalV2ReadModel.RSISlope, trendType: FuturesTradeSignalV2ReadModel.TrendType.ToStringFast(), trendStrength: FuturesTradeSignalV2ReadModel.TrendStrength.ToStringFast(), tradeSignal: FuturesTradeSignalV2ReadModel.TradeSignal.ToStringFast(), tdi: FuturesTradeSignalV2ReadModel.TDI.ToStringFast(), tdiStrength: FuturesTradeSignalV2ReadModel.TDIStrength.ToStringFast(), mdi: FuturesTradeSignalV2ReadModel.MDI, mdiTrend: FuturesTradeSignalV2ReadModel.MDITrend.ToStringFast(), mdiUpTrendLimit: FuturesTradeSignalV2ReadModel.MDIUpTrendLimit, mdiDownTrendLimit: FuturesTradeSignalV2ReadModel.MDIDownTrendLimit, upTrendingTrigger: FuturesTradeSignalV2ReadModel.UpTrendingTrigger, downTrendingTrigger: FuturesTradeSignalV2ReadModel.DownTrendingTrigger, entryTrigger: FuturesTradeSignalV2ReadModel.EntryTrigger, exitTrigger: FuturesTradeSignalV2ReadModel.ExitTrigger, trendDelta: FuturesTradeSignalV2ReadModel.TrendDelta, trendExtreme: FuturesTradeSignalV2ReadModel.TrendExtreme, trendReversal: FuturesTradeSignalV2ReadModel.TrendReversal, fiftyDma: FuturesTradeSignalV2ReadModel.FiftyDMA, twoHundredDma: FuturesTradeSignalV2ReadModel.TwoHundredDMA, tradeExecuteState: FuturesTradeSignalV2ReadModel.TradeExecuteState.ToStringFast()))
            .QueueCommand();
        var insertLatestIndex = db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(new InsertFuturesTradeSignalIndex($"latest:{timePeriod}", "latest", sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod))
            .QueueCommand();
        var insertDateIndex = db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(new InsertFuturesTradeSignalIndex($"date:{timePeriod}:{FuturesTradeSignalV2ReadModel.ValueDate.DayNumber}", FuturesTradeSignalV2ReadModel.ContractId, sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod))
            .QueueCommand();
        await db.ExecuteQueuedCommandsAsync([insertSignal, insertLatestIndex, insertDateIndex]);
    }

    /// <summary>
    /// Inserts a collection of futures trade signals into the database asynchronously.
    /// </summary>
    /// <param name = "futuresTradeSignals"></param>
    /// <returns></returns>
    public async Task InsertFuturesTradeSignalsAsync(ICollection<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
    {
        var ftsQuery = new FuturesTradeSignalV2ReadModel[futuresTradeSignals.Count];
        var signalIndex = 0;
        foreach (var signal in futuresTradeSignals)
        {
            var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId)
                .ConfigureAwait(false);
            ftsQuery[signalIndex++] = signal with
            {
                SequenceId = sequenceId
            };
        }

        var db = _dbFactory.MarketDataDb;
        var insertSignals = db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignal)}", MarketDataDbCql.InsertFuturesTradeSignal)
            .SetParameters(ftsQuery.Select(e => new InsertFuturesTradeSignal(contractId: e.ContractId, valueDate: e.ValueDate, timePeriod: e.TimePeriod.ToStringFast(), sequenceId: e.SequenceId, timestamp: e.Timestamp, mean: e.Mean, stdDev: e.StdDev, futuresPrice: e.FuturesPrice, priceChangePercent: e.PriceChangePercent, fundRiskPercent: e.FundRiskPercent, rsi: e.RSI, rsiSlope: e.RSISlope, trendType: e.TrendType.ToStringFast(), trendStrength: e.TrendStrength.ToStringFast(), tradeSignal: e.TradeSignal.ToStringFast(), tdi: e.TDI.ToStringFast(), tdiStrength: e.TDIStrength.ToStringFast(), mdi: e.MDI, mdiTrend: e.MDITrend.ToStringFast(), mdiUpTrendLimit: e.MDIUpTrendLimit, mdiDownTrendLimit: e.MDIDownTrendLimit, upTrendingTrigger: e.UpTrendingTrigger, downTrendingTrigger: e.DownTrendingTrigger, entryTrigger: e.EntryTrigger, exitTrigger: e.ExitTrigger, trendDelta: e.TrendDelta, trendExtreme: e.TrendExtreme, trendReversal: e.TrendReversal, fiftyDma: e.FiftyDMA, twoHundredDma: e.TwoHundredDMA, tradeExecuteState: e.TradeExecuteState.ToStringFast())))
            .QueueCommand();
        var indexParameters = ftsQuery.SelectMany(e =>
        {
            var timePeriod = e.TimePeriod.ToStringFast();
            return new InsertFuturesTradeSignalIndex[]
            {
                new($"latest:{timePeriod}", "latest", e.SequenceId, e.ContractId, e.ValueDate, timePeriod),
                new($"date:{timePeriod}:{e.ValueDate.DayNumber}", e.ContractId, e.SequenceId, e.ContractId, e.ValueDate, timePeriod)
            };
        });
        var insertIndex = db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(indexParameters)
            .QueueCommand();
        await db.ExecuteQueuedCommandsAsync([insertSignals, insertIndex])
            .ConfigureAwait(false);
    }

    public async Task<long> InsertFuturesTradeSignalsAsync(IEnumerable<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
    {
        var signals = futuresTradeSignals as IReadOnlyCollection<FuturesTradeSignalV2ReadModel> ?? futuresTradeSignals.ToArray();
        var rowCount = signals.Count;
        var ftsQuery = new FuturesTradeSignalV2ReadModel[rowCount];
        var signalIndex = 0;
        foreach (var signal in signals)
        {
            var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId)
                .ConfigureAwait(false);
            ftsQuery[signalIndex++] = signal with
            {
                SequenceId = sequenceId
            };
        }

        var db = _dbFactory.MarketDataDb;
        var insertSignals = db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignal)}", MarketDataDbCql.InsertFuturesTradeSignal)
            .SetParameters(ftsQuery.Select(e => new InsertFuturesTradeSignal(contractId: e.ContractId, valueDate: e.ValueDate, timePeriod: e.TimePeriod.ToStringFast(), sequenceId: e.SequenceId, timestamp: e.Timestamp, mean: e.Mean, stdDev: e.StdDev, futuresPrice: e.FuturesPrice, priceChangePercent: e.PriceChangePercent, fundRiskPercent: e.FundRiskPercent, rsi: e.RSI, rsiSlope: e.RSISlope, trendType: e.TrendType.ToStringFast(), trendStrength: e.TrendStrength.ToStringFast(), tradeSignal: e.TradeSignal.ToStringFast(), tdi: e.TDI.ToStringFast(), tdiStrength: e.TDIStrength.ToStringFast(), mdi: e.MDI, mdiTrend: e.MDITrend.ToStringFast(), mdiUpTrendLimit: e.MDIUpTrendLimit, mdiDownTrendLimit: e.MDIDownTrendLimit, upTrendingTrigger: e.UpTrendingTrigger, downTrendingTrigger: e.DownTrendingTrigger, entryTrigger: e.EntryTrigger, exitTrigger: e.ExitTrigger, trendDelta: e.TrendDelta, trendExtreme: e.TrendExtreme, trendReversal: e.TrendReversal, fiftyDma: e.FiftyDMA, twoHundredDma: e.TwoHundredDMA, tradeExecuteState: e.TradeExecuteState.ToStringFast())))
            .QueueCommand();
        var indexParameters = ftsQuery.SelectMany(e =>
        {
            var timePeriod = e.TimePeriod.ToStringFast();
            return new InsertFuturesTradeSignalIndex[]
            {
                new($"latest:{timePeriod}", "latest", e.SequenceId, e.ContractId, e.ValueDate, timePeriod),
                new($"date:{timePeriod}:{e.ValueDate.DayNumber}", e.ContractId, e.SequenceId, e.ContractId, e.ValueDate, timePeriod)
            };
        });
        var insertIndex = db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(indexParameters)
            .QueueCommand();
        await db.ExecuteQueuedCommandsAsync([insertSignals, insertIndex])
            .ConfigureAwait(false);
        return rowCount;
    }

    /// <summary>
    /// Inserts a rate of return record into the database asynchronously.
    /// </summary>
    /// <param name = "e">The rate of return data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertRateOfReturnAsync(RateOfReturnReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertRateOfReturn)}", MarketDataDbCql.InsertRateOfReturn)
        .SetParameters(new InsertRateOfReturn(symbol: e.Symbol, valueDate: e.ValueDate, rateOfReturn: e.RateOfReturn))
        .ExecuteCommandAsync();
    /// <summary>
    /// Inserts a market holiday record into the database asynchronously.
    /// </summary>
    /// <param name = "e">The MarketHolidayReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertMarketHolidayAsync(MarketHolidayReadModel e) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketHoliday)}", MarketDataDbCql.InsertMarketHoliday)
        .SetParameters(new InsertMarketHoliday(currencyType: e.CurrencyType.ToStringFast(), holidayDate: e.HolidayDate, description: e.Description))
        .ExecuteCommandAsync();
    /// <summary>
    /// load futures iti trend class  data by date range into
    /// </summary>
    /// <param name = "e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbReader = db as IMarketDataDbReadContext;
        var futuresItiSignals = await dbReader!.GetFuturesItiSignalTrendClassDataAsync(symbol, startDate, endDate);
        var sourceSignals = futuresItiSignals.GroupBy(e => (e.ContractId, e.ValueDate, e.IntrinsicTimeGroupId)).Select(e => e.OrderByDescending(signal => signal.SequenceId).First()).ToArray();
        if (sourceSignals.Length == 0)
            return FuturesItiTrendModelDataStatistics.Empty;
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiTrendClassData)}", MarketDataDbCql.DeleteFuturesItiTrendClassData)
            .SetParameters(new DeleteFuturesItiTrendClassData(symbol, startDate, endDate))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendClassData)}", MarketDataDbCql.InsertFuturesItiTrendClassData)
            .SetParameters(sourceSignals.Select(e => new InsertFuturesItiTrendClassData(symbol, e.ValueDate, e.IntrinsicTime, e.SequenceId, (float)e.IntrinsicTimeGroupId, (float)e.IntrinsicTimeTrend, (float)e.IntrinsicTimeMode, (float)e.TrendDelta, 0f)))
            .ExecuteCommandAsync();
        return MarketDataDbContextExtensions.CalculateStatistics(sourceSignals.Select(e => (double)e.IntrinsicTimeGroupId));
    }

    /// <summary>
    /// load futures iti trend delta data by date range into
    /// </summary>
    /// <param name = "e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbReader = db as IMarketDataDbReadContext;
        var futuresItiSignals = await dbReader!.GetFuturesItiSignalTrendDeltaDataAsync(symbol, startDate, endDate);
        var sourceSignals = futuresItiSignals.GroupBy(e => (e.ContractId, e.ValueDate, e.IntrinsicTimeGroupId)).Select(e => e.OrderByDescending(signal => signal.SequenceId).First()).ToArray();
        if (sourceSignals.Length == 0)
            return FuturesItiTrendModelDataStatistics.Empty;
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiTrendDeltaData)}", MarketDataDbCql.DeleteFuturesItiTrendDeltaData)
            .SetParameters(new DeleteFuturesItiTrendDeltaData(symbol, startDate, endDate))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendDeltaData)}", MarketDataDbCql.InsertFuturesItiTrendDeltaData)
            .SetParameters(sourceSignals.Select(e => new InsertFuturesItiTrendDeltaData(symbol, e.ValueDate, e.IntrinsicTime, e.SequenceId, (float)e.TrendDelta, (float)e.IntrinsicTimeTrend, (float)e.IntrinsicTimeMode, (float)e.IntrinsicPrice, (float)e.TrendExtreme, 0f)))
            .ExecuteCommandAsync();
        return MarketDataDbContextExtensions.CalculateStatistics(sourceSignals.Select(e => e.TrendDelta));
    }

    /// <summary>
    /// Upserts a FuturesEodDataV2ReadModel into the database.
    /// </summary>
    /// <param name = "e">The futures EOD data to upsert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesEodDataAsync(FuturesEodDataV2ReadModel e)
    {
        // check if the data already exists...
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesDataId)}", MarketDataDbCql.GetFuturesDataId)
            .SetParameters(new GetFuturesDataId(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesDataId!);
        if (existingData is null)
        {
            // insert new data if it doesn't exist...
            var openPrice = e.OpenPrice;
            await this.ExecuteMaintainedProjectionMutationAsync(FuturesEodProjection, new[] { MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.ValueDate) }, async () =>
            {
                await InsertFuturesEodDataIndexAsync(new FuturesEodDataIndexReadModel(e.ValueDate, e.ContractId));
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodData)}", MarketDataDbCql.InsertFuturesEodData)
                    .SetParameters(new InsertFuturesEodData(contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA))
                    .ExecuteCommandAsync();
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesIntraDayData)}", MarketDataDbCql.InsertFuturesIntraDayData)
                    .SetParameters(new InsertFuturesIntraDayData(contractId: e.ContractId, valueDate: e.ValueDate, sequenceId: await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId), symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize))
                    .ExecuteCommandAsync();
                await this.UpsertFuturesEodProjectionAsync(e, openPrice);
            });
        }
        else
        {
            // Update existing data if it exists
            var openPrice = e.OpenPrice;
            await this.ExecuteMaintainedProjectionMutationAsync(FuturesEodProjection, new[] { MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.ValueDate) }, async () =>
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpdateFuturesEodData)}", MarketDataDbCql.UpdateFuturesEodData)
                    .SetParameters(new UpdateFuturesEodData(contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA))
                    .ExecuteCommandAsync();
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesIntraDayData)}", MarketDataDbCql.InsertFuturesIntraDayData)
                    .SetParameters(new InsertFuturesIntraDayData(contractId: e.ContractId, valueDate: e.ValueDate, sequenceId: await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId), symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize))
                    .ExecuteCommandAsync();
                await this.UpsertFuturesEodProjectionAsync(e, openPrice);
            });
        }
    }

    /// <summary>
    /// Updates provider-supplied session prices and the two metrics that depend on
    /// the session open. The immutable intraday observation table is intentionally
    /// not appended for a statistics-only correction.
    /// </summary>
    public async Task UpdateFuturesEodSessionStatisticsAsync(FuturesEodDataV2ReadModel e)
    {
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesDataId)}", MarketDataDbCql.GetFuturesDataId)
            .SetParameters(new GetFuturesDataId(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesDataId!);
        if (existingData is null)
            throw new InvalidOperationException($"Futures EOD row '{e.ContractId}:{e.ValueDate:yyyy-MM-dd}' does not exist.");
        await this.ExecuteMaintainedProjectionMutationAsync(FuturesEodProjection, new[] { MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.ValueDate) }, async () =>
        {
            List<object> commands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpdateFuturesEodSessionStatistics)}", MarketDataDbCql.UpdateFuturesEodSessionStatistics)
                .SetParameters(new UpdateFuturesEodSessionStatistics(contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice: e.OpenPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, priceDirection: e.PriceDirection.ToStringFast()))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                .SetParameters(MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(e, e.OpenPrice))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(new InsertMarketDataProjectionMonth(FuturesEodProjection, MarketDataDbContextExtensions.ToYearMonth(e.ValueDate)))
                .QueueCommand()];
            await db.ExecuteQueuedCommandsAsync(commands);
        });
    }

    public async Task InsertFuturesEodDataAsync(ICollection<FuturesEodDataV2ReadModel> futuresEodData) => await this.InsertFuturesEodBatchAsync(futuresEodData);
    /// <summary>
    /// Inserts a collection of futures end-of-day (EOD) data records into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided futures EOD data and inserts it into the database. The
    /// method ensures that all records in the collection are processed sequentially, and the total count of processed
    /// records is returned.</remarks>
    /// <param name = "futuresEodData">A collection of <see cref = "FuturesEodDataV2ReadModel"/> objects representing the futures EOD data to be
    /// inserted. Each object must contain valid data for all required fields.</param>
    /// <returns>A <see cref = "Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// records processed.</returns>
    public async Task<long> InsertFuturesEodDataAsync(IEnumerable<FuturesEodDataV2ReadModel> futuresEodData)
    {
        var rowCount = 0l;
        List<FuturesEodDataV2ReadModel> batch = new(ProjectionWriteBatchSize);
        foreach (var e in futuresEodData)
        {
            batch.Add(e);
            rowCount++;
            if (batch.Count == ProjectionWriteBatchSize)
            {
                await this.InsertFuturesEodBatchAsync(batch);
                batch.Clear();
            }
        }

        await this.InsertFuturesEodBatchAsync(batch);
        return rowCount;
    }

    /// <summary>
    /// Upserts a VixFuturesEodDataReadModel into the database.
    /// </summary>
    /// <param name = "e"></param>
    /// <returns></returns>
    public async Task InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel e, FuturesSessionStatisticsSnapshot? sessionStatistics = null)
    {
        // check if the data already exists...
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodData)}", MarketDataDbCql.GetVixFuturesEodData)
            .SetParameters(new GetVixFuturesEodData(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToVixFuturesEodData!);
        if (existingData == null)
        {
            var hasPrices = sessionStatistics is { HasPriceStatistics: true };
            var openPrice = hasPrices ? sessionStatistics.Value.OpenPrice : e.Price;
            var highPrice = hasPrices ? sessionStatistics.Value.HighPrice : e.Price;
            var lowPrice = hasPrices ? sessionStatistics.Value.LowPrice : e.Price;
            var volume = sessionStatistics is { HasVolume: true } ? sessionStatistics.Value.Volume : e.Size;
            await this.ExecuteMaintainedProjectionMutationAsync(VixFuturesContractIndexProjection, new[] { MarketDataDbContextExtensions.GetVixContractIndexScopeKey(e.ContractId) }, async () =>
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertVixFuturesEodData)}", MarketDataDbCql.InsertVixFuturesEodData)
                    .SetParameters(new InsertVixFuturesEodData(contractId: e.ContractId, valueDate: e.ValueDate, openPrice, highPrice, lowPrice, closePrice: e.Price, volume))
                    .ExecuteCommandAsync();
                await this.UpsertVixFuturesContractIndexAsync(e.ContractId);
            });
        }
        else
        {
            // The contract index is unchanged for an update to an existing canonical
            // partition, so avoid an index and projection-state write on every VX observation.
            // Derive the rolling row only from its current stored state and the incoming
            // trade-or-quote observation; tick storage is an independent realtime projection.
            var hasPrices = sessionStatistics is { HasPriceStatistics: true };
            var openPrice = hasPrices ? sessionStatistics.Value.OpenPrice : existingData.OpenPrice;
            var highPrice = hasPrices ? sessionStatistics.Value.HighPrice : Math.Max(existingData.HighPrice, e.Price);
            var lowPrice = hasPrices ? sessionStatistics.Value.LowPrice : Math.Min(existingData.LowPrice, e.Price);
            var volume = sessionStatistics is { HasVolume: true } ? sessionStatistics.Value.Volume : checked(existingData.Volume + e.Size);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpdateVixFuturesEodData)}", MarketDataDbCql.UpdateVixFuturesEodData)
                .SetParameters(new UpdateVixFuturesEodData(contractId: e.ContractId, valueDate: e.ValueDate, openPrice, highPrice, lowPrice, closePrice: e.Price, volume))
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI for a given entity ID.
    /// </summary>
    /// <param name = "e">The entity ID containing the contract ID and value date.</param>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalMDIViewModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
    {
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, IntrinsicTimeTrendType.UpTrend, mode)));
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode)));
        return [.. rows.SelectMany(static values => values).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI by trend for a given entity ID, intrinsic time trend, and intrinsic time group ID.
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate"> </param>
    /// <param name = "intrinsicTimeTrend">The intrinsic time trend.</param>
    /// <param name = "intrinsicTimeGroupId">The intrinsic time group ID.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalMDIViewModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId)
    {
        _ = intrinsicTimeGroupId; // The legacy query never applied this argument.
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, intrinsicTimeTrend, mode)));
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode)));
        return [.. rows.SelectMany(static values => values).Where(row => row.IntrinsicTimeTrend == intrinsicTimeTrend).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <param name = "e"></param>
    /// <param name = "timestamp"></param>
    /// <param name = "lookbackInterval"></param>
    /// <param name = "startTime"></param>
    /// <param name = "endTime"></param>
    /// <returns></returns>
    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime)
    {
        var db = _dbFactory.MarketDataDb;
        var rsiValues = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesRsiSignalsForTrend)}", MarketDataDbCql.GetFuturesRsiSignalsForTrend)
            .SetParameters(new GetFuturesRsiSignalsForTrend(contractId, timePeriod.ToStringFast(), periodLength, valueDate, TimeOnly.FromDateTime(startTime), TimeOnly.FromDateTime(endTime)))
            .ExecuteQueryAsync(MapToRsi!);
        var upTrendCount = rsiValues.Count(static rsi => rsi >= 50);
        var downTrendCount = rsiValues.Count(static rsi => rsi < 50);
        var trendDirection = default(FuturesTrendType) switch
        {
            _ when upTrendCount > downTrendCount => FuturesTrendType.UpTrending,
            _ when upTrendCount < downTrendCount => FuturesTrendType.DownTrending,
            _ when upTrendCount == downTrendCount => FuturesTrendType.RangeBound,
            _ => FuturesTrendType.RangeBound
        };
        return new FuturesTrendDirectionReadModel(ContractId: contractId, ValueDate: valueDate, Timestamp: TimeOnly.FromDateTime(DateTime.Now), LookbackInterval: lookbackInterval, UpTrendCount: upTrendCount, DownTrendCount: downTrendCount, TrendDirection: trendDirection);
    }

    /// <summary>
    /// return last futures intrinsic time indicator signal
    /// </summary>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate) => (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged)).FirstOrDefault();
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged, cancellationToken: cancellationToken)
        .ConfigureAwait(false)).FirstOrDefault();
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)}", MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)
        .SetParameters(new GetLastFuturesItiSignalByTimePeriod(contractId, valueDate, timePeriod.ToString()))
        .ExecuteSingleAsync(MapToFuturesItiSignal!);
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)}", MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)
        .SetParameters(new GetLastFuturesItiSignalByTimePeriod(contractId, valueDate, timePeriod.ToString()))
        .ExecuteSingleAsync(MapToFuturesItiSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// return last futures intrinsic time indicator signal from trend direction change
    /// </summary>\
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate) => (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged)).FirstOrDefault();
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
        .ConfigureAwait(false)).FirstOrDefault();
    /// <summary>
    /// return last futures intrinsic time indicator signal from trend extreme change
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate)
    {
        var direction = (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged)).FirstOrDefault();
        return (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendExtremeChanged, direction?.SequenceId ?? 0)).FirstOrDefault();
    }

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var direction = (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();
        return (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendExtremeChanged, direction?.SequenceId ?? 0, cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();
    }

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend reversal change
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate)
    {
        var direction = (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged)).FirstOrDefault();
        return (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged, direction?.SequenceId ?? 0)).FirstOrDefault();
    }

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var direction = (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();
        return (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged, direction?.SequenceId ?? 0, cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();
    }

    /// <summary>
    /// Gets the last Futures RSI signal by value date.
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "timePeriod">The time period.</param>
    /// <param name = "periodLength">The period length.</param>
    /// <returns></returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiSignal)}", MarketDataDbCql.GetLastFuturesRsiSignal)
        .SetParameters(new GetLastFuturesRsiSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteSingleAsync(MapToFuturesRsiSignal);
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiSignal)}", MarketDataDbCql.GetLastFuturesRsiSignal)
        .SetParameters(new GetLastFuturesRsiSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteSingleAsync(MapToFuturesRsiSignal, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures RSI signal by time period and period length.
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "timePeriod">The time period.</param>
    /// <param name = "periodLength">The period length.</param>
    /// <returns></returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiDailySignal)}", MarketDataDbCql.GetLastFuturesRsiDailySignal)
        .SetParameters(new GetLastFuturesRsiDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
        .ExecuteSingleAsync(MapToFuturesRsiSignal);
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiDailySignal)}", MarketDataDbCql.GetLastFuturesRsiDailySignal)
        .SetParameters(new GetLastFuturesRsiDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
        .ExecuteSingleAsync(MapToFuturesRsiSignal, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures TDI signal for a given entity ID.
    /// </summary>
    /// <param name = "e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesTdiSignalReadModel"/>.</returns>
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate) => await GetLastFuturesTdiSignalAsync(contractId, valueDate, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId);
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, string configurationId) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTdiSignal)}", MarketDataDbCql.GetLastFuturesTdiSignal)
        .SetParameters(new GetLastFuturesTdiSignal(contractId, timePeriod.ToStringFast(), configurationId, valueDate))
        .ExecuteSingleAsync(MapToFuturesTdiSignal!);
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => await GetLastFuturesTdiSignalAsync(contractId, valueDate, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId, cancellationToken);
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, string configurationId, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTdiSignal)}", MarketDataDbCql.GetLastFuturesTdiSignal)
        .SetParameters(new GetLastFuturesTdiSignal(contractId, timePeriod.ToStringFast(), configurationId, valueDate))
        .ExecuteSingleAsync(MapToFuturesTdiSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures MACD signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "timePeriod"></param>
    /// <param name = "periodLength"></param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesMacdSignalReadModel"/>.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await GetLastFuturesMacdSignalAsync(contractId, valueDate, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod);
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdSignal)}", MarketDataDbCql.GetLastFuturesMacdSignal)
        .SetParameters(new GetLastFuturesMacdSignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate))
        .ExecuteSingleAsync(MapToFuturesMacdSignal!);
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await GetLastFuturesMacdSignalAsync(contractId, valueDate, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod, cancellationToken);
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdSignal)}", MarketDataDbCql.GetLastFuturesMacdSignal)
        .SetParameters(new GetLastFuturesMacdSignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate))
        .ExecuteSingleAsync(MapToFuturesMacdSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// 
    /// </summary>
    /// <param name = "contractId"></param>
    /// <param name = "timePeriod"></param>
    /// <param name = "periodLength"></param>
    /// <returns></returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await GetLastFuturesMacdDailySignalAsync(contractId, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod);
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdDailySignal)}", MarketDataDbCql.GetLastFuturesMacdDailySignal)
        .SetParameters(new GetLastFuturesMacdDailySignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod))
        .ExecuteSingleAsync(MapToFuturesMacdSignal!);
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await GetLastFuturesMacdDailySignalAsync(contractId, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod, cancellationToken);
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdDailySignal)}", MarketDataDbCql.GetLastFuturesMacdDailySignal)
        .SetParameters(new GetLastFuturesMacdDailySignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod))
        .ExecuteSingleAsync(MapToFuturesMacdSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures ATR signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesAtrSignalReadModel"/>.</returns>
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAtrSignal)}", MarketDataDbCql.GetLastFuturesAtrSignal)
        .SetParameters(new GetLastFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteSingleAsync(MapToFuturesAtrSignal!);
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAtrSignal)}", MarketDataDbCql.GetLastFuturesAtrSignal)
        .SetParameters(new GetLastFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteSingleAsync(MapToFuturesAtrSignal!, cancellationToken)
        .ConfigureAwait(false);
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesDailyAtrSignal)}", MarketDataDbCql.GetLastFuturesDailyAtrSignal)
        .SetParameters(new GetLastFuturesAtrDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
        .ExecuteSingleAsync(MapToFuturesAtrSignal!);
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesDailyAtrSignal)}", MarketDataDbCql.GetLastFuturesDailyAtrSignal)
        .SetParameters(new GetLastFuturesAtrDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
        .ExecuteSingleAsync(MapToFuturesAtrSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures ADX signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesAdxSignalReadModel"/>.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxSignal)}", MarketDataDbCql.GetLastFuturesAdxSignal)
        .SetParameters(new GetLastFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteSingleAsync(MapToFuturesAdxSignal!);
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxSignal)}", MarketDataDbCql.GetLastFuturesAdxSignal)
        .SetParameters(new GetLastFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
        .ExecuteSingleAsync(MapToFuturesAdxSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures ADX daily signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "timePeriod">The value date.</param>
    /// <param name = "periodLength"></param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesAdxSignalReadModel"/>.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxDailySignal)}", MarketDataDbCql.GetLastFuturesAdxDailySignal)
        .SetParameters(new GetLastFuturesAdxDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
        .ExecuteSingleAsync(MapToFuturesAdxSignal!);
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxDailySignal)}", MarketDataDbCql.GetLastFuturesAdxDailySignal)
        .SetParameters(new GetLastFuturesAdxDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
        .ExecuteSingleAsync(MapToFuturesAdxSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last futures trade signal
    /// </summary>
    /// <param name = "contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name = "valueDate"> The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesTradeSignalV2ReadModel"/>.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalById)}", MarketDataDbCql.GetLastFuturesTradeSignalById)
        .SetParameters(new GetLastFuturesTradeSignalById(contractId, valueDate, TimeFrameType.FifteenSeconds.ToStringFast()))
        .ExecuteSingleAsync(MapToFuturesTradeSignal!);
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalById)}", MarketDataDbCql.GetLastFuturesTradeSignalById)
        .SetParameters(new GetLastFuturesTradeSignalById(contractId, valueDate, TimeFrameType.FifteenSeconds.ToStringFast()))
        .ExecuteSingleAsync(MapToFuturesTradeSignal!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// gets the last futures trade signal asynchronously.
    /// </summary>
    /// <returns></returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync()
    {
        var id = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignal)}", MarketDataDbCql.GetLastFuturesTradeSignal)
            .SetParameters(new GetLastFuturesTradeSignal($"latest:{TimeFrameType.FifteenSeconds.ToStringFast()}"))
            .ExecuteSingleAsync(MapToFuturesTradeSignalId);
        return id is null ? null : await GetLastFuturesTradeSignalAsync(id.ContractId, id.ValueDate);
    }

    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(CancellationToken cancellationToken)
    {
        var id = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignal)}", MarketDataDbCql.GetLastFuturesTradeSignal)
            .SetParameters(new GetLastFuturesTradeSignal($"latest:{TimeFrameType.FifteenSeconds.ToStringFast()}"))
            .ExecuteSingleAsync(MapToFuturesTradeSignalId, cancellationToken)
            .ConfigureAwait(false);
        return id is null ? null : await GetLastFuturesTradeSignalAsync(id.ContractId, id.ValueDate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// gets all futures trade signals asynchronously.
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalsAsync()
    {
        ICollection<FuturesTradeSignalV2ReadModel> resultSet = [];
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalAll)}", MarketDataDbCql.GetFuturesTradeSignalAll)
            .ExecuteMapReduceAsync(MapToFuturesTradeSignal, reducer => resultSet = [.. reducer]);
        return resultSet;
    }

    /// <summary>
    /// Gets the last futures trade signal for a given symbol and value date asynchronously.    
    /// </summary>
    /// <param name = "symbol"></param>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        List<string> contractIds = [.. (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(e => e.ContractId)];
        return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)}", MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate))
            .ExecuteSingleAsync(MapToFuturesTradeSignal);
    }

    /// <summary>
    /// Gets the last rate of return for a given symbol asynchronously.
    /// </summary>
    /// <param name = "symbol">The symbol to get the rate of return for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "RateOfReturnReadModel"/>.</returns>
    public async Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastRateOfReturn)}", MarketDataDbCql.GetLastRateOfReturn)
        .SetParameters(new GetLastRateOfReturn(symbol))
        .ExecuteSingleAsync(MapToRateOfReturn);
    public async Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastRateOfReturn)}", MarketDataDbCql.GetLastRateOfReturn)
        .SetParameters(new GetLastRateOfReturn(symbol))
        .ExecuteSingleAsync(MapToRateOfReturn, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the last VIX futures EOD data for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name = "e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "VixFuturesEodDataReadModel"/>.</returns>
    public async Task<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastVixFuturesEodData)}", MarketDataDbCql.GetLastVixFuturesEodData)
        .SetParameters(new GetLastVixFuturesEodData(contractId, valueDate))
        .ExecuteSingleAsync(MapToVixFuturesEodData);
    /// <summary>
    /// Gets the VIX futures EOD data for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name = "contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name = "valueDate"></param>
    public async Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodData)}", MarketDataDbCql.GetVixFuturesEodData)
        .SetParameters(new GetVixFuturesEodData(contractId, valueDate))
        .ExecuteSingleAsync(MapToVixFuturesEodData);
    /// <summary>
    /// Gets the VIX futures EOD data for a given by value date asynchronously.
    /// </summary>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task<ICollection<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
    {
        var stamp = await this.GetProjectionScopeReadStampAsync(VixFuturesContractIndexProjection, Enumerable.Range(0, VixContractBucketCount).Select(MarketDataDbContextExtensions.GetVixContractIndexScopeKey));
        if (stamp is null)
            return await this.ReadLegacyVixFuturesEodDataByValueDateAsync(valueDate);
        var results = await this.ReadIndexedVixFuturesEodDataByValueDateAsync(valueDate);
        if (await this.IsProjectionScopeReadStampValidAsync(stamp.Value))
            return results;
        return await this.ReadLegacyVixFuturesEodDataByValueDateAsync(valueDate);
    }

    /// <summary>
    /// Gets the last updated yield curve rate from the database.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "YieldCurveRateReadModel"/>.</returns>
    public async Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync() => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastYieldCurveRate)}", MarketDataDbCql.GetLastYieldCurveRate)
        .ExecuteSingleAsync(MapToYieldCurveRate!);
    public async Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync(CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastYieldCurveRate)}", MarketDataDbCql.GetLastYieldCurveRate)
        .ExecuteSingleAsync(MapToYieldCurveRate!, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets the yield curve rate for a given value date.
    /// </summary>
    /// <param name = "valueDate">The value date to retrieve the yield curve rate for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "YieldCurveRateReadModel"/> if found; otherwise, null.</returns>
    public async Task<YieldCurveRateReadModel?> GetYieldCurveRateAsync(DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRate)}", MarketDataDbCql.GetYieldCurveRate)
        .SetParameters(new GetYieldCurveRate(valueDate))
        .ExecuteSingleAsync(MapToYieldCurveRate);
    /// <summary>
    /// Gets the collection of YieldCurveRateReadModel for a given start date and end date.
    /// </summary>
    /// <param name = "startDate">The start value date.</param>
    /// <param name = "endDate">The end value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the collection of YieldCurveRateReadModel.</returns>
    public async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate) => await this.GetYieldCurveRatesCoreAsync(startDate, endDate, CancellationToken.None);
    public async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken) => await this.GetYieldCurveRatesCoreAsync(startDate, endDate, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Gets a collection of integer values representing the years for yield curve rates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the collection of integer years.</returns>
    public async Task<ICollection<int>> GetYieldCurveRateYearsAsync() => await GetYieldCurveRateYearsAsync(CancellationToken.None);
    public async Task<ICollection<int>> GetYieldCurveRateYearsAsync(CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRateYears)}", MarketDataDbCql.GetYieldCurveRateYears)
        .SetParameters(new GetYieldCurveRateYears(YieldCurveLookupId))
        .ExecuteQueryAsync(MapToYearMonth, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Retrieves market holidays for a given currency type.
    /// </summary>
    /// <param name = "currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketHolidays)}", MarketDataDbCql.GetMarketHolidays)
        .SetParameters(new GetMarketHolidays(currencyType: currencyType.ToStringFast()))
        .ExecuteQueryAsync(MapToMarketHoliday);
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketHolidays)}", MarketDataDbCql.GetMarketHolidays)
        .SetParameters(new GetMarketHolidays(currencyType: currencyType.ToStringFast()))
        .ExecuteQueryAsync(MapToMarketHoliday, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Asynchronously retrieves the live feed data for a specific trade identified by the provided order and trade
    /// identifiers.
    /// </summary>
    /// <remarks>This method queries the market data database for the specified trade. Ensure that both
    /// orderId and tradeId are valid to avoid unexpected results.</remarks>
    /// <param name = "orderId">The unique identifier of the order associated with the trade. Must be a positive integer.</param>
    /// <param name = "tradeId">The unique identifier of the trade for which to retrieve live feed data. Must be a positive integer.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a TradeLiveFeedReadModel object with
    /// the live feed data if found; otherwise, null.</returns>
    public async Task<TradeLiveFeedReadModel?> GetTradeLiveFeedAsync(int orderId, int tradeId) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetTradeLiveFeed)}", MarketDataDbCql.GetTradeLiveFeed)
        .SetParameters(new GetTradeLiveFeed(orderId, tradeId))
        .ExecuteSingleAsync(MapToTradeLiveFeed!);
    /// <summary>
    /// Retrieves market holidays for a given currency type within a specified date range.
    /// </summary>
    /// <param name = "currencyType">The currency type.</param>
    /// <param name = "startDate">The start date of the range.</param>
    /// <param name = "endDate">The end date of the range.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysByDateRangeAsync(CurrencyType currencyType, DateOnly startDate, DateOnly endDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketHolidaysByDateRange)}", MarketDataDbCql.GetMarketHolidaysByDateRange)
        .SetParameters(new GetMarketHolidaysByDateRange(currencyType: currencyType.ToStringFast(), startDate, endDate))
        .ExecuteQueryAsync(MapToMarketHoliday);
    /// <summary>
    /// return number of trading days...
    /// </summary>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    /// <param name = "marketType"></param>
    /// <param name = "currencyType"></param>
    /// <returns></returns>
    public async Task<int> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD)
    {
        var key = new TradingDaysKey(StartDate: startDate, EndDate: endDate, MarketType: marketType, CurrencyType: currencyType);
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
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday || tradeDate.DayOfWeek == DayOfWeek.Sunday || holidayMap.ContainsKey(tradeDate))
                continue;
            tradingDays++;
        }

        _tradingDaysMap.Add(key, tradingDays);
        return tradingDays;
    }

    /// <summary>
    /// return all normal curve data
    /// </summary>
    public async Task<ICollection<NormalCurveDataReadModel>> GetNormalCurveDataAsync() => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetNormalCurveData)}", MarketDataDbCql.GetNormalCurveData)
        .ExecuteQueryAsync(MapToNormalCurveData!);
    /// <summary>
    /// return futures trade signal id by value date
    /// </summary>
    /// <param name = "valueDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)}", MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)
        .SetParameters(new GetFuturesTradeSignalIdByValueDate($"date:{TimeFrameType.FifteenSeconds.ToStringFast()}:{valueDate.DayNumber}"))
        .ExecuteQueryAsync(MapToFuturesTradeSignalId);
    public async Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)}", MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)
        .SetParameters(new GetFuturesTradeSignalIdByValueDate($"date:{TimeFrameType.FifteenSeconds.ToStringFast()}:{valueDate.DayNumber}"))
        .ExecuteQueryAsync(MapToFuturesTradeSignalId, cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// return normal curve table
    /// </summary>
    /// <returns></returns>
    public async Task<NormalCurveTableReadModel> GetNormalCurveTableAsync()
    {
        _normalCurveTable ??= new NormalCurveTableReadModel([.. await GetNormalCurveDataAsync()]);
        return _normalCurveTable!;
    }

    /// <summary>
    /// return trading dates...
    /// </summary>
    /// <param name = "startDate"></param>
    /// <param name = "endDate"></param>
    /// <param name = "marketType"></param>
    /// <param name = "currencyType"></param>
    /// <returns></returns>
    public async Task<DateOnly[]> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD)
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
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday || tradeDate.DayOfWeek == DayOfWeek.Sunday || holidayMap.ContainsKey(tradeDate))
                continue;
            tradingDates.Add(tradeDate);
        }

        return [.. tradingDates];
    }

    public async Task<DateOnly[]> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken)
    {
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType, cancellationToken)
            .ConfigureAwait(false);
        var holidayDates = marketHolidays.Select(static holiday => holiday.HolidayDate).ToHashSet();
        var tradingDates = new List<DateOnly>();
        for (var tradeDate = startDate; tradeDate <= endDate; tradeDate = tradeDate.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tradeDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidayDates.Contains(tradeDate))
                continue;
            tradingDates.Add(tradeDate);
        }

        return [.. tradingDates];
    }

    public async Task<int> GetTradingDayCountAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD)
    {
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType)!;
        var holidayDates = marketHolidays.Select(static holiday => holiday.HolidayDate).ToHashSet();
        var tradingDayCount = 0;
        for (var tradeDate = startDate; tradeDate <= endDate; tradeDate = tradeDate.AddDays(1))
        {
            if (tradeDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidayDates.Contains(tradeDate))
                continue;
            tradingDayCount++;
        }

        return tradingDayCount;
    }

    public async Task<int> GetTradingDayCountAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken)
    {
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType, cancellationToken)
            .ConfigureAwait(false);
        var holidayDates = marketHolidays.Select(static holiday => holiday.HolidayDate).ToHashSet();
        var tradingDayCount = 0;
        for (var tradeDate = startDate; tradeDate <= endDate; tradeDate = tradeDate.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tradeDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidayDates.Contains(tradeDate))
                continue;
            tradingDayCount++;
        }

        return tradingDayCount;
    }

    /// <summary>
    /// Checks if yield curve rate data exists for a given value date.
    /// </summary>
    /// <param name = "valueDate">The value date to check.</param>
    /// <returns>A task representing the asynchronous operation, containing a boolean indicating whether the data exists.</returns>
    public async Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate) => (await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRate)}", MarketDataDbCql.GetYieldCurveRate)
        .SetParameters(new GetYieldCurveRate(valueDate))
        .ExecuteSingleAsync(MapToYieldCurveRate!)) is not null;
    public async Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate, CancellationToken cancellationToken) => (await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRate)}", MarketDataDbCql.GetYieldCurveRate)
        .SetParameters(new GetYieldCurveRate(valueDate))
        .ExecuteSingleAsync(MapToYieldCurveRate!, cancellationToken)
        .ConfigureAwait(false)) is not null;
    /// <summary>
    /// return stream request id
    /// </summary>
    /// <param name = "streamId"></param>
    /// <returns></returns>
    public async Task<int> GetStreamingRequestIdAsync() => Convert.ToInt32(await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId));
    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <param name = "e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate) => await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged);
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
        .ConfigureAwait(false);
    /// <summary>
    /// Idempotently rebuilds the non-signal MarketData V2 query projections from canonical tables.
    /// </summary>
    public async Task<MarketDataProjectionBackfillReadModel> BackfillQueryProjectionsV2Async(int batchSize = ProjectionWriteBatchSize, CancellationToken cancellationToken = default, DateTime? staleOperationCutoffUtc = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (staleOperationCutoffUtc is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException("The stale operation cutoff must use DateTimeKind.Utc.", nameof(staleOperationCutoffUtc));
        }

        if (staleOperationCutoffUtc > DateTime.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(staleOperationCutoffUtc), staleOperationCutoffUtc, "The stale operation cutoff cannot be in the future.");
        }

        var db = _dbFactory.MarketDataDb;
        string[] projectionNames = [FuturesTickByTimeProjection, FuturesEodProjection, VixFuturesContractIndexProjection, FuturesItiSignalQueryProjection];
        var failedMutationIds = new Dictionary<string, Guid[]>(StringComparer.Ordinal);
        var backfillMutationIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var backfillScopes = projectionNames.ToDictionary(static projectionName => projectionName, static _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var backfillStartedScopes = projectionNames.ToDictionary(static projectionName => projectionName, static _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var backfillAcknowledgedScopes = projectionNames.ToDictionary(static projectionName => projectionName, static _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var backfillGlobalOperationsAcknowledged = new HashSet<string>(StringComparer.Ordinal);
        var targetMutationSubmissionStarted = false;
        try
        {
            // Failed operations are safe to reclaim automatically because their writer
            // reached a terminal catch path. Other old operations require an explicit
            // operator cutoff after every writer has been drained.
            var scopedMutations = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)}", MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(MapToProjectionScopeMutation);
            var recoverableScopedMutations = scopedMutations.Where(mutation => projectionNames.Contains(mutation.ProjectionName, StringComparer.Ordinal)).Where(mutation => mutation.IsFailed || staleOperationCutoffUtc.HasValue && mutation.StartedOn <= staleOperationCutoffUtc.Value).ToArray();
            if (recoverableScopedMutations.Length > 0)
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)
                    .SetParameters(recoverableScopedMutations.Select(mutation => new RemoveMarketDataProjectionScopeOperationV3(mutation.ProjectionName, mutation.ScopeKey, mutation.MutationId)))
                    .ExecuteCommandAsync(cancellationToken);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                    .SetParameters(recoverableScopedMutations.Select(mutation => new DeleteMarketDataProjectionScopeMutationV3(mutation.ProjectionName, mutation.ScopeKey, mutation.MutationId)))
                    .ExecuteCommandAsync(cancellationToken);
            }

            // Supplying a cutoff is an explicit operator assertion that all older writers
            // have been drained or terminated. Time alone is not treated as a lease.
            if (staleOperationCutoffUtc.HasValue)
            {
                foreach (var projectionName in projectionNames)
                {
                    var mutations = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMutations)}", MarketDataDbCql.GetMarketDataProjectionMutations)
                        .SetParameters(new GetMarketDataProjectionMutation(projectionName))
                        .ExecuteQueryAsync(MapToProjectionMutation);
                    var staleMutationIds = mutations.Where(mutation => mutation.StartedOn <= staleOperationCutoffUtc.Value).Select(mutation => mutation.MutationId).ToHashSet();
                    if (staleMutationIds.Count == 0)
                        continue;
                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionOperations)}", MarketDataDbCql.RemoveMarketDataProjectionOperations)
                        .SetParameters(new RemoveMarketDataProjectionOperations(projectionName, staleMutationIds))
                        .ExecuteCommandAsync(cancellationToken);
                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionMutation)}", MarketDataDbCql.DeleteMarketDataProjectionMutation)
                        .SetParameters(staleMutationIds.Select(mutationId => new DeleteMarketDataProjectionMutation(projectionName, mutationId)))
                        .ExecuteCommandAsync(cancellationToken);
                }
            }

            // Publish the repair markers before touching any projection. Readers keep using
            // canonical tables until every rebuilt projection has reconciled successfully.
            foreach (var projectionName in projectionNames)
            {
                var mutationId = Guid.NewGuid();
                backfillMutationIds.Add(projectionName, mutationId);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMutation)}", MarketDataDbCql.InsertMarketDataProjectionMutation)
                    .SetParameters(new InsertMarketDataProjectionMutation(projectionName, mutationId, DateTime.UtcNow))
                    .ExecuteCommandAsync(cancellationToken);
                async Task ActivateGlobalProjectionAsync() => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.BeginMarketDataProjectionOperation)}", MarketDataDbCql.BeginMarketDataProjectionOperation)
                    .SetParameters(new BeginMarketDataProjectionOperation(projectionName, mutationId, new HashSet<Guid> { mutationId }))
                    .ExecuteCommandAsync(cancellationToken);
                if (ProjectionBackfillGlobalActivationForTestingAsync is { } globalActivation)
                    await globalActivation(ActivateGlobalProjectionAsync);
                else
                    await ActivateGlobalProjectionAsync();
                backfillGlobalOperationsAcknowledged.Add(projectionName);
                var existingMutations = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMutations)}", MarketDataDbCql.GetMarketDataProjectionMutations)
                    .SetParameters(new GetMarketDataProjectionMutation(projectionName))
                    .ExecuteQueryAsync(MapToProjectionMutation);
                failedMutationIds.Add(projectionName, [.. existingMutations.Where(existingMutation => existingMutation.MutationId != mutationId && existingMutation.IsFailed).Select(existingMutation => existingMutation.MutationId)]);
            }

            // Claim every guard before discovering data scopes. Ordinary writers touch a
            // deterministic guard as well as their data scope; a post-discovery write then
            // prevents the guard's conditional release without creating a global hot row.
            foreach (var projectionName in projectionNames)
            {
                var guards = this.GetProjectionGuardScopeKeys();
                backfillScopes[projectionName].UnionWith(guards);
                await BeginBackfillScopesAsync(projectionName, guards);
            }

            // Discover the union of canonical, target, and prior state scopes while the
            // projection-wide gate is closed. Existing targets/states are included so a
            // replay also clears deleted or previously mis-bucketed partitions.
            await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickProjectionScopesSource)}", MarketDataDbCql.GetFuturesTickProjectionScopesSource)
                .ExecuteStreamAsync(MapToFuturesTickProjectionScope, cancellationToken))
            {
                backfillScopes[FuturesTickByTimeProjection].Add(scope);
            }

            await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickProjectionScopesTarget)}", MarketDataDbCql.GetFuturesTickProjectionScopesTarget)
                .ExecuteStreamAsync(MapToFuturesTickProjectionScope, cancellationToken))
            {
                backfillScopes[FuturesTickByTimeProjection].Add(scope);
            }

            await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodProjectionScopesSource)}", MarketDataDbCql.GetFuturesEodProjectionScopesSource)
                .ExecuteStreamAsync(MapToFuturesEodProjectionSourceScope, cancellationToken))
            {
                backfillScopes[FuturesEodProjection].Add(scope);
            }

            await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodProjectionScopesTarget)}", MarketDataDbCql.GetFuturesEodProjectionScopesTarget)
                .ExecuteStreamAsync(MapToFuturesEodProjectionTargetScope, cancellationToken))
            {
                backfillScopes[FuturesEodProjection].Add(scope);
            }

            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalProjectionScopesSource)}", MarketDataDbCql.GetFuturesItiSignalProjectionScopesSource)
                .ExecuteStreamAsync(MapToFuturesItiProjectionScope, cancellationToken))
            {
                AddFuturesItiScopes(row);
            }

            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalProjectionScopesDayTarget)}", MarketDataDbCql.GetFuturesItiSignalProjectionScopesDayTarget)
                .ExecuteStreamAsync(MapToFuturesItiProjectionScope, cancellationToken))
            {
                AddFuturesItiScopes(row);
            }

            await foreach (var state in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3All)}", MarketDataDbCql.GetMarketDataProjectionScopeStatesV3All)
                .ExecuteStreamAsync(MapToProjectionScopeState, cancellationToken))
            {
                if (backfillScopes.TryGetValue(state.ProjectionName, out var scopes))
                    scopes.Add(state.ScopeKey);
            }

            foreach (var bucket in Enumerable.Range(0, VixContractBucketCount))
                backfillScopes[VixFuturesContractIndexProjection].Add(MarketDataDbContextExtensions.GetVixContractIndexScopeKey(bucket));
            // Journal and claim every discovered scope with the projection backfill's ID.
            // A concurrent ordinary writer adds a second ID, causing scoped completion to
            // fail without affecting unrelated partitions.
            foreach (var projectionName in projectionNames)
            {
                var scopesToStart = backfillScopes[projectionName].Except(backfillStartedScopes[projectionName], StringComparer.Ordinal).ToArray();
                await BeginBackfillScopesAsync(projectionName, scopesToStart);
            }

            // A clean rebuild is what makes reconciliation detect deleted and stale rows,
            // rather than merely proving that the source and projection have equal counts.
            targetMutationSubmissionStarted = true;
            if (ProjectionBackfillTargetMutationSubmittingForTestingAsync is { } targetMutationSubmitting)
                await targetMutationSubmitting();
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesTickDataByTime)}", MarketDataDbCql.TruncateFuturesTickDataByTime)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesEodDataByMonth)}", MarketDataDbCql.TruncateFuturesEodDataByMonth)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateVixFuturesContractIndex)}", MarketDataDbCql.TruncateVixFuturesContractIndex)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesItiSignalByContractDay)}", MarketDataDbCql.TruncateFuturesItiSignalByContractDay)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesItiSignalByContractMonth)}", MarketDataDbCql.TruncateFuturesItiSignalByContractMonth)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.TruncateFuturesItiSignalByTrendModeMonth)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateMarketDataProjectionMonth)}", MarketDataDbCql.TruncateMarketDataProjectionMonth)
                .ExecuteCommandAsync(cancellationToken);
            var futuresTickSourceIdentityBuilder = new ProjectionIdentityBuilder();
            var tickBatch = new List<InsertFuturesTickDataByTime>(batchSize);
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickDataAll)}", MarketDataDbCql.GetFuturesTickDataAll)
                .ExecuteStreamAsync(MapToFuturesTickData!, cancellationToken))
            {
                futuresTickSourceIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesTickIdentity(row));
                tickBatch.Add(new InsertFuturesTickDataByTime(row.ContractId, row.ValueDate, row.TickTime, row.TickId, row.Price, row.Size));
                if (tickBatch.Count == batchSize)
                    await FlushTicksAsync();
            }

            await FlushTicksAsync();
            var futuresEodSourceIdentityBuilder = new ProjectionIdentityBuilder();
            var eodBatch = new List<InsertFuturesEodDataByMonth>(batchSize);
            var eodMonths = new HashSet<int>();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
                .ExecuteStreamAsync(MapToFuturesEodData!, cancellationToken))
            {
                futuresEodSourceIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesEodIdentity(row));
                eodBatch.Add(MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(row, row.OpenPrice));
                eodMonths.Add(MarketDataDbContextExtensions.ToYearMonth(row.ValueDate));
                if (eodBatch.Count == batchSize)
                    await FlushFuturesEodAsync();
            }

            await FlushFuturesEodAsync();
            var futuresItiSourceIdentityBuilder = new ProjectionIdentityBuilder();
            var itiDayBatch = new List<InsertFuturesItiSignal>(batchSize);
            var itiMonthBatch = new List<InsertFuturesItiSignalByContractMonth>(batchSize);
            var itiMonths = new HashSet<int>();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsAll)}", MarketDataDbCql.GetFuturesItiSignalsAll)
                .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
            {
                futuresItiSourceIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesItiSignalIdentity(row));
                itiDayBatch.Add(MarketDataDbContextExtensions.CreateFuturesItiSignalParameters(row, row.SequenceId));
                itiMonthBatch.Add(MarketDataDbContextExtensions.CreateFuturesItiSignalMonthParameters(row, row.SequenceId));
                itiMonths.Add(MarketDataDbContextExtensions.ToYearMonth(row.ValueDate));
                if (itiDayBatch.Count == batchSize)
                    await FlushFuturesItiAsync();
            }

            await FlushFuturesItiAsync();
            long vixFuturesEodRowsSource = 0;
            var vixContracts = new HashSet<string>(StringComparer.Ordinal);
            var vixContractsSourceIdentityBuilder = new ProjectionIdentityBuilder();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataAll)}", MarketDataDbCql.GetVixFuturesEodDataAll)
                .ExecuteStreamAsync(MapToVixFuturesEodData, cancellationToken))
            {
                vixFuturesEodRowsSource++;
                if (vixContracts.Add(row.ContractId))
                    vixContractsSourceIdentityBuilder.Add(MarketDataDbContextExtensions.GetVixContractIdentity(row.ContractId));
            }

            var vixContractBatch = new List<InsertVixFuturesContractIndex>(batchSize);
            foreach (var contractId in vixContracts.OrderBy(static contractId => contractId, StringComparer.Ordinal))
            {
                vixContractBatch.Add(new InsertVixFuturesContractIndex(MarketDataDbContextExtensions.GetVixContractBucket(contractId), contractId));
                if (vixContractBatch.Count == batchSize)
                    await FlushVixContractsAsync();
            }

            await FlushVixContractsAsync();
            var futuresTickProjectedIdentityBuilder = new ProjectionIdentityBuilder();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickDataByTimeAll)}", MarketDataDbCql.GetFuturesTickDataByTimeAll)
                .ExecuteStreamAsync(MapToFuturesTickData!, cancellationToken))
            {
                futuresTickProjectedIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesTickIdentity(row));
            }

            var futuresEodProjectedIdentityBuilder = new ProjectionIdentityBuilder();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataByMonthAll)}", MarketDataDbCql.GetFuturesEodDataByMonthAll)
                .ExecuteStreamAsync(MapToFuturesEodData!, cancellationToken))
            {
                futuresEodProjectedIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesEodIdentity(row));
            }

            var futuresItiDayIdentityBuilder = new ProjectionIdentityBuilder();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalByContractDayAll)}", MarketDataDbCql.GetFuturesItiSignalByContractDayAll)
                .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
            {
                futuresItiDayIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesItiSignalIdentity(row));
            }

            var futuresItiMonthIdentityBuilder = new ProjectionIdentityBuilder();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalByContractMonthAll)}", MarketDataDbCql.GetFuturesItiSignalByContractMonthAll)
                .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
            {
                futuresItiMonthIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesItiSignalIdentity(row));
            }

            var futuresItiTrendModeIdentityBuilder = new ProjectionIdentityBuilder();
            await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalByTrendModeMonthAll)}", MarketDataDbCql.GetFuturesItiSignalByTrendModeMonthAll)
                .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
            {
                futuresItiTrendModeIdentityBuilder.Add(MarketDataDbContextExtensions.GetFuturesItiSignalIdentity(row));
            }

            var vixContractsIndexedIdentityBuilder = new ProjectionIdentityBuilder();
            await foreach (var indexRow in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesContractIndexAll)}", MarketDataDbCql.GetVixFuturesContractIndexAll)
                .ExecuteStreamAsync(MapToVixFuturesContractIndex, cancellationToken))
            {
                vixContractsIndexedIdentityBuilder.Add(MarketDataDbContextExtensions.GetVixContractIdentity(indexRow.Bucket, indexRow.ContractId));
            }

            var futuresTickSourceIdentity = futuresTickSourceIdentityBuilder.Build();
            var futuresTickProjectedIdentity = futuresTickProjectedIdentityBuilder.Build();
            var futuresEodSourceIdentity = futuresEodSourceIdentityBuilder.Build();
            var futuresEodProjectedIdentity = futuresEodProjectedIdentityBuilder.Build();
            var vixContractsSourceIdentity = vixContractsSourceIdentityBuilder.Build();
            var vixContractsIndexedIdentity = vixContractsIndexedIdentityBuilder.Build();
            var futuresItiSourceIdentity = futuresItiSourceIdentityBuilder.Build();
            var futuresItiDayIdentity = futuresItiDayIdentityBuilder.Build();
            var futuresItiMonthIdentity = futuresItiMonthIdentityBuilder.Build();
            var futuresItiTrendModeIdentity = futuresItiTrendModeIdentityBuilder.Build();
            var reconciled = futuresTickSourceIdentity == futuresTickProjectedIdentity && futuresEodSourceIdentity == futuresEodProjectedIdentity && vixContractsSourceIdentity == vixContractsIndexedIdentity && futuresItiSourceIdentity == futuresItiDayIdentity && futuresItiSourceIdentity == futuresItiMonthIdentity && futuresItiSourceIdentity == futuresItiTrendModeIdentity;
            if (ProjectionBackfillReconciledForTestingAsync is { } backfillReconciled)
                await backfillReconciled();
            var cutoverPublished = false;
            if (reconciled)
            {
                foreach (var projectionName in projectionNames)
                {
                    var failedOperations = failedMutationIds[projectionName];
                    if (failedOperations.Length == 0)
                        continue;
                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionOperations)}", MarketDataDbCql.RemoveMarketDataProjectionOperations)
                        .SetParameters(new RemoveMarketDataProjectionOperations(projectionName, failedOperations.ToHashSet()))
                        .ExecuteCommandAsync(cancellationToken);
                }

                var futuresTickScopesCompleted = await CompleteProjectionScopesAsync(FuturesTickByTimeProjection, guardScopes: false);
                var futuresEodScopesCompleted = await CompleteProjectionScopesAsync(FuturesEodProjection, guardScopes: false);
                var vixContractIndexScopesCompleted = await CompleteProjectionScopesAsync(VixFuturesContractIndexProjection, guardScopes: false);
                var futuresItiScopesCompleted = await CompleteProjectionScopesAsync(FuturesItiSignalQueryProjection, guardScopes: false);
                var allDataScopesCompleted = futuresTickScopesCompleted && futuresEodScopesCompleted && vixContractIndexScopesCompleted && futuresItiScopesCompleted;
                var futuresTickCompleted = false;
                var futuresEodCompleted = false;
                var vixContractIndexCompleted = false;
                var futuresItiCompleted = false;
                if (allDataScopesCompleted)
                {
                    futuresTickCompleted = await CompleteProjectionAsync(FuturesTickByTimeProjection, futuresTickSourceIdentity, futuresTickProjectedIdentity);
                    futuresEodCompleted = await CompleteProjectionAsync(FuturesEodProjection, futuresEodSourceIdentity, futuresEodProjectedIdentity);
                    vixContractIndexCompleted = await CompleteProjectionAsync(VixFuturesContractIndexProjection, vixContractsSourceIdentity, vixContractsIndexedIdentity);
                    futuresItiCompleted = await CompleteProjectionAsync(FuturesItiSignalQueryProjection, futuresItiSourceIdentity, futuresItiDayIdentity);
                }

                var allGlobalStatesCompleted = allDataScopesCompleted && futuresTickCompleted && futuresEodCompleted && vixContractIndexCompleted && futuresItiCompleted;
                var allGuardScopesCompleted = false;
                if (allGlobalStatesCompleted)
                {
                    // Global mutation markers remain present while guards are released, so
                    // readers cannot observe global readiness between these phases.
                    var futuresTickGuardsCompleted = await CompleteProjectionScopesAsync(FuturesTickByTimeProjection, guardScopes: true);
                    var futuresEodGuardsCompleted = await CompleteProjectionScopesAsync(FuturesEodProjection, guardScopes: true);
                    var vixContractIndexGuardsCompleted = await CompleteProjectionScopesAsync(VixFuturesContractIndexProjection, guardScopes: true);
                    var futuresItiGuardsCompleted = await CompleteProjectionScopesAsync(FuturesItiSignalQueryProjection, guardScopes: true);
                    allGuardScopesCompleted = futuresTickGuardsCompleted && futuresEodGuardsCompleted && vixContractIndexGuardsCompleted && futuresItiGuardsCompleted;
                }

                cutoverPublished = allGlobalStatesCompleted && allGuardScopesCompleted;
                if (cutoverPublished)
                {
                    // Only explicitly failed operations plus this repair's own marker are
                    // removed. Any live/unclassified operation keeps reads on canonical data.
                    foreach (var projectionName in projectionNames)
                    {
                        var backfillMutationId = backfillMutationIds[projectionName];
                        var scopes = backfillScopes[projectionName];
                        if (scopes.Count > 0)
                        {
                            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                                .SetParameters(scopes.Select(scope => new DeleteMarketDataProjectionScopeMutationV3(projectionName, scope, backfillMutationId)))
                                .ExecuteCommandAsync(cancellationToken);
                        }

                        foreach (var failedMutationId in failedMutationIds[projectionName])
                            await DeleteProjectionMutationAsync(projectionName, failedMutationId);
                        await DeleteProjectionMutationAsync(projectionName, backfillMutationId);
                    }
                }
                else
                {
                    if (futuresTickCompleted)
                        await CloseCompletedProjectionAsync(FuturesTickByTimeProjection);
                    if (futuresEodCompleted)
                        await CloseCompletedProjectionAsync(FuturesEodProjection);
                    if (vixContractIndexCompleted)
                        await CloseCompletedProjectionAsync(VixFuturesContractIndexProjection);
                    if (futuresItiCompleted)
                        await CloseCompletedProjectionAsync(FuturesItiSignalQueryProjection);
                    await FailBackfillMutationsAsync();
                }
            }
            else
                await FailBackfillMutationsAsync();
            var cutoverCompleted = cutoverPublished && (await GetQueryProjectionReadinessAsync(cancellationToken)).IsReady;
            return new MarketDataProjectionBackfillReadModel(futuresTickSourceIdentity.Count, futuresTickProjectedIdentity.Count, futuresTickSourceIdentity.Fingerprint, futuresTickProjectedIdentity.Fingerprint, futuresEodSourceIdentity.Count, futuresEodProjectedIdentity.Count, futuresEodSourceIdentity.Fingerprint, futuresEodProjectedIdentity.Fingerprint, vixFuturesEodRowsSource, vixContractsSourceIdentity.Count, vixContractsIndexedIdentity.Count, vixContractsSourceIdentity.Fingerprint, vixContractsIndexedIdentity.Fingerprint, futuresItiSourceIdentity.Count, futuresItiDayIdentity.Count, futuresItiMonthIdentity.Count, futuresItiTrendModeIdentity.Count, futuresItiSourceIdentity.Fingerprint, futuresItiDayIdentity.Fingerprint, futuresItiMonthIdentity.Fingerprint, futuresItiTrendModeIdentity.Fingerprint, cutoverCompleted);
            async Task FlushTicksAsync()
            {
                if (tickBatch.Count == 0)
                    return;
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickDataByTime)}", MarketDataDbCql.InsertFuturesTickDataByTime)
                    .SetParameters(tickBatch)
                    .ExecuteCommandAsync(cancellationToken);
                tickBatch.Clear();
            }

            async Task FlushFuturesEodAsync()
            {
                if (eodBatch.Count == 0)
                    return;
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                    .SetParameters(eodBatch)
                    .ExecuteCommandAsync(cancellationToken);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                    .SetParameters(eodMonths.Select(yearMonth => new InsertMarketDataProjectionMonth(FuturesEodProjection, yearMonth)))
                    .ExecuteCommandAsync(cancellationToken);
                eodBatch.Clear();
                eodMonths.Clear();
            }

            async Task FlushFuturesItiAsync()
            {
                if (itiDayBatch.Count == 0)
                    return;
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractDay)}", MarketDataDbCql.InsertFuturesItiSignalByContractDay)
                    .SetParameters(itiDayBatch)
                    .ExecuteCommandAsync(cancellationToken);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractMonth)}", MarketDataDbCql.InsertFuturesItiSignalByContractMonth)
                    .SetParameters(itiMonthBatch)
                    .ExecuteCommandAsync(cancellationToken);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)
                    .SetParameters(itiMonthBatch)
                    .ExecuteCommandAsync(cancellationToken);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                    .SetParameters(itiMonths.Select(yearMonth => new InsertMarketDataProjectionMonth(FuturesItiSignalQueryProjection, yearMonth)))
                    .ExecuteCommandAsync(cancellationToken);
                itiDayBatch.Clear();
                itiMonthBatch.Clear();
                itiMonths.Clear();
            }

            async Task FlushVixContractsAsync()
            {
                if (vixContractBatch.Count == 0)
                    return;
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertVixFuturesContractIndex)}", MarketDataDbCql.InsertVixFuturesContractIndex)
                    .SetParameters(vixContractBatch)
                    .ExecuteCommandAsync(cancellationToken);
                vixContractBatch.Clear();
            }

            void AddFuturesItiScopes(FuturesItiProjectionScopeData row)
            {
                var yearMonth = MarketDataDbContextExtensions.ToYearMonth(row.ValueDate);
                var scopes = backfillScopes[FuturesItiSignalQueryProjection];
                scopes.Add(MarketDataDbContextExtensions.GetFuturesItiDayScopeKey(row.ContractId, row.ValueDate));
                scopes.Add(MarketDataDbContextExtensions.GetFuturesItiMonthScopeKey(row.ContractId, yearMonth));
                scopes.Add(MarketDataDbContextExtensions.GetFuturesItiTimelineScopeKey(row.ContractId, row.IntrinsicTimeTrend, row.IntrinsicTimeMode, yearMonth));
            }

            async Task BeginBackfillScopesAsync(string projectionName, IEnumerable<string> scopeKeys)
            {
                var scopes = scopeKeys.Distinct(StringComparer.Ordinal).Except(backfillStartedScopes[projectionName], StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (scopes.Length == 0)
                    return;
                // Record these before issuing commands: either command can fail after a
                // partial server-side apply, and the catch path must conservatively end and
                // classify every possibly started scope.
                backfillStartedScopes[projectionName].UnionWith(scopes);
                var mutationId = backfillMutationIds[projectionName];
                var startedOn = DateTime.UtcNow;
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                    .SetParameters(scopes.Select(scope => new InsertMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId, startedOn)))
                    .ExecuteCommandAsync(cancellationToken);
                var activeOperations = new HashSet<Guid>
                {
                    mutationId
                };
                async Task ActivateBackfillScopesAsync() => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)
                    .SetParameters(scopes.Select(scope => new BeginMarketDataProjectionScopeOperationV3(projectionName, scope, mutationId, activeOperations)))
                    .ExecuteCommandAsync(cancellationToken);
                if (ProjectionBackfillScopeActivationForTestingAsync is { } scopeActivation)
                    await scopeActivation(ActivateBackfillScopesAsync);
                else
                    await ActivateBackfillScopesAsync();
                backfillAcknowledgedScopes[projectionName].UnionWith(scopes);
            }

            async Task<bool> CompleteProjectionScopesAsync(string projectionName, bool guardScopes)
            {
                var mutationId = backfillMutationIds[projectionName];
                var activeOperations = new HashSet<Guid>
                {
                    mutationId
                };
                var allCompleted = true;
                var scopes = backfillScopes[projectionName].Where(scope => MarketDataDbContextExtensions.IsProjectionGuardScopeKey(scope) == guardScopes);
                foreach (var scopeBatch in scopes.Chunk(ProjectionReadConcurrency))
                {
                    var completions = scopeBatch.Select(async scope => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)
                        .SetParameters(new CompleteMarketDataProjectionScopeOperationV3(projectionName, scope, mutationId, activeOperations, DateTime.UtcNow, activeOperations))
                        .ExecuteSingleAsync(MapToBoolean) == true).ToArray();
                    if ((await Task.WhenAll(completions)).Any(static completed => !completed))
                        allCompleted = false;
                }

                return allCompleted;
            }

            async Task<bool> CompleteProjectionAsync(string projectionName, ProjectionIdentity sourceIdentity, ProjectionIdentity projectedIdentity)
            {
                var activeOperations = new HashSet<Guid>
                {
                    backfillMutationIds[projectionName]
                };
                return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionState)}", MarketDataDbCql.CompleteMarketDataProjectionState)
                    .SetParameters(new CompleteMarketDataProjectionState(projectionName, backfillMutationIds[projectionName], activeOperations, sourceIdentity.Count, projectedIdentity.Count, sourceIdentity.Fingerprint, projectedIdentity.Fingerprint, DateTime.UtcNow, activeOperations))
                    .ExecuteSingleAsync(MapToBoolean) == true;
            }

            async Task CloseCompletedProjectionAsync(string projectionName)
            {
                var activeOperations = new HashSet<Guid>
                {
                    backfillMutationIds[projectionName]
                };
                await MarketDataDbContextExtensions.EndProjectionOperationAsync(db, projectionName, activeOperations, cancellationToken);
            }

            async Task FailBackfillMutationsAsync()
            {
                foreach (var projectionName in projectionNames)
                {
                    var mutationId = backfillMutationIds[projectionName];
                    var scopes = backfillStartedScopes[projectionName];
                    if (scopes.Count > 0)
                    {
                        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)
                            .SetParameters(scopes.Select(scope => new FailMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId, DateTime.UnixEpoch)))
                            .ExecuteCommandAsync();
                    }

                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionMutation)}", MarketDataDbCql.FailMarketDataProjectionMutation)
                        .SetParameters(new FailMarketDataProjectionMutation(projectionName, mutationId, DateTime.UnixEpoch))
                        .ExecuteCommandAsync();
                }
            }

            async Task DeleteProjectionMutationAsync(string projectionName, Guid mutationId) => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionMutation)}", MarketDataDbCql.DeleteMarketDataProjectionMutation)
                .SetParameters(new DeleteMarketDataProjectionMutation(projectionName, mutationId))
                .ExecuteCommandAsync(cancellationToken);
        }
        catch
        {
            if (targetMutationSubmissionStarted)
            {
                // A TRUNCATE or projection upsert may still be applied after a timeout
                // or cancellation. Preserve every original nonfailed journal and active
                // guard so no later repair can cut over until an operator has drained
                // writers and supplied an explicit stale-operation cutoff.
                throw;
            }

            foreach (var (projectionName, mutationId) in backfillMutationIds)
            {
                var acknowledgedScopes = backfillAcknowledgedScopes[projectionName];
                try
                {
                    if (acknowledgedScopes.Count > 0)
                    {
                        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)
                            .SetParameters(acknowledgedScopes.Select(scope => new FailMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId, DateTime.UnixEpoch)))
                            .ExecuteCommandAsync();
                    }
                }
                catch
                {
                    // Leave original markers for operator-cutoff recovery.
                }

                if (backfillGlobalOperationsAcknowledged.Contains(projectionName))
                {
                    try
                    {
                        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionMutation)}", MarketDataDbCql.FailMarketDataProjectionMutation)
                            .SetParameters(new FailMarketDataProjectionMutation(projectionName, mutationId, DateTime.UnixEpoch))
                            .ExecuteCommandAsync();
                    }
                    catch
                    {
                        // Leave an unclassified marker in place; a repair must not clear it.
                    }
                }
                // An unacknowledged Begin may still be applied later. Its original
                // nonfailed journal and possible active ID are intentionally untouched;
                // only an explicit cutoff after writers are drained may reclaim them.
            }

            throw;
        }
    }

    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, IntrinsicTimeTrendType.UpTrend, mode, cancellationToken)));
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode, cancellationToken: cancellationToken)));
        return [.. rows.SelectMany(static values => values).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId, CancellationToken cancellationToken)
    {
        _ = intrinsicTimeGroupId; // The legacy query never applied this argument.
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, intrinsicTimeTrend, mode, cancellationToken)));
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode, cancellationToken: cancellationToken)));
        return [.. rows.SelectMany(static values => values).Where(row => row.IntrinsicTimeTrend == intrinsicTimeTrend).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime, CancellationToken cancellationToken)
    {
        var db = _dbFactory.MarketDataDb;
        var rsiValues = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesRsiSignalsForTrend)}", MarketDataDbCql.GetFuturesRsiSignalsForTrend)
            .SetParameters(new GetFuturesRsiSignalsForTrend(contractId, timePeriod.ToStringFast(), periodLength, valueDate, TimeOnly.FromDateTime(startTime), TimeOnly.FromDateTime(endTime)))
            .ExecuteQueryAsync(MapToRsi!, cancellationToken)
            .ConfigureAwait(false);
        var upTrendCount = rsiValues.Count(static rsi => rsi >= 50);
        var downTrendCount = rsiValues.Count(static rsi => rsi < 50);
        var trendDirection = upTrendCount.CompareTo(downTrendCount) switch
        {
            > 0 => FuturesTrendType.UpTrending,
            < 0 => FuturesTrendType.DownTrending,
            _ => FuturesTrendType.RangeBound
        };
        return new FuturesTrendDirectionReadModel(contractId, valueDate, TimeOnly.FromDateTime(DateTime.Now), lookbackInterval, upTrendCount, downTrendCount, trendDirection);
    }

    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var securitiesDb = (ISecuritiesDbReadContext)_dbFactory.SecuritiesDb;
        var contracts = await securitiesDb.GetFuturesContractsBySymbolAsync(symbol, cancellationToken)
            .ConfigureAwait(false);
        List<string> contractIds = [.. contracts.Select(static contract => contract.ContractId)];
        return await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)}", MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate))
            .ExecuteSingleAsync(MapToFuturesTradeSignal, cancellationToken)
            .ConfigureAwait(false);
    }

    internal const string DownloadLogSelect = "SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc FROM market_data_download_log WHERE dataset = :Dataset AND provider = :Provider AND scope = :Scope AND value_date = :ValueDate";
    internal const string DownloadLogInsert = "INSERT INTO market_data_download_log (dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc) VALUES (:Dataset, :Provider, :Scope, :ValueDate, :RequestedAtUtc, :ImportCommandId, :LogCommandId, :SourceTerminalEventId, :SchemaVersion, :Status, :StartedAtUtc, :FinishedAtUtc, :ElapsedMilliseconds, :DownloadedRecordCount, :PersistedRecordCount, :ErrorCode, :ErrorMessage, :PayloadSha256, :ProjectedAtUtc);";
    public async Task InsertMarketDataDownloadLogAsync(MarketDataDownloadOutcome outcome, Guid logCommandId, string payloadSha256, CancellationToken cancellationToken = default)
    {
        var command = new InsertMarketDataDownloadLogCommand(outcome);
        if (command.CommandId != logCommandId || command.PayloadSha256 != payloadSha256)
            throw new ArgumentException("DownloadLog projection identity/hash mismatch.");
        await _dbFactory.MarketDataDb.Use("DownloadLog.Insert", DownloadLogInsert)
            .SetParameters(new DownloadLogParameters(outcome.Dataset.ToString(), outcome.Provider, outcome.Scope, outcome.ValueDate, outcome.RequestedAtUtc, outcome.ImportCommandId, logCommandId, outcome.SourceTerminalEventId, outcome.SchemaVersion, outcome.Status.ToString(), outcome.StartedAtUtc, outcome.FinishedAtUtc, outcome.ElapsedMilliseconds, outcome.DownloadedRecordCount, outcome.PersistedRecordCount, outcome.ErrorCode, outcome.ErrorMessage, payloadSha256, DateTime.UtcNow))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MarketDataDownloadLogResult> GetMarketDataDownloadLogAsync(MarketDataDownloadPartition partition, MarketDataDownloadCursor attempt, CancellationToken cancellationToken = default)
    {
        partition.Validate();
        MarketDataDbContextExtensions.ValidateDownloadCursor(attempt);
        var rows = await _dbFactory.MarketDataDb.Use("DownloadLog.Exact", DownloadLogSelect + " AND requested_at_utc = :RequestedAtUtc AND import_command_id = :ImportCommandId LIMIT 1;")
            .SetParameters(new DownloadLogReadParameters(partition.Dataset.ToString(), partition.Provider, partition.Scope, partition.ValueDate, attempt.RequestedAtUtc, attempt.ImportCommandId, 1, true))
            .ExecuteQueryAsync(MapToDownloadLog, cancellationToken)
            .ConfigureAwait(false);
        return new(rows.FirstOrDefault());
    }

    public async Task<MarketDataDownloadHistoryResult> GetMarketDataDownloadHistoryAsync(MarketDataDownloadPartition partition, int pageSize = 100, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        partition.Validate();
        if (pageSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (cursor is not null)
            MarketDataDbContextExtensions.ValidateDownloadCursor(cursor);
        var cql = DownloadLogSelect + (cursor is null ? "" : " AND (requested_at_utc, import_command_id) < (:RequestedAtUtc, :ImportCommandId)") + " LIMIT :RowLimit;";
        var rows = await _dbFactory.MarketDataDb.Use(cursor is null ? "DownloadLog.History" : "DownloadLog.HistoryAfter", cql)
            .SetParameters(new DownloadLogReadParameters(partition.Dataset.ToString(), partition.Provider, partition.Scope, partition.ValueDate, cursor?.RequestedAtUtc ?? DateTime.UnixEpoch, cursor?.ImportCommandId ?? Guid.Empty, pageSize + 1))
            .ExecuteQueryAsync(MapToDownloadLog, cancellationToken)
            .ConfigureAwait(false);
        var items = rows.Take(pageSize).ToArray();
        var last = items.LastOrDefault()?.Outcome;
        return new(items, rows.Count > pageSize && last is not null ? new(last.RequestedAtUtc, last.ImportCommandId) : null);
    }

    public async Task<MarketDataDownloadStatusResult> GetMarketDataDownloadStatusAsync(MarketDataDownloadPartition partition, Guid? requiredImportCommandId = null, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        if (requiredImportCommandId == Guid.Empty)
            throw new ArgumentException("A required import ID cannot be empty.");
        // Each call is bounded; consumers may continue explicitly. Always keep the newest attempt separate.
        var first = await GetMarketDataDownloadHistoryAsync(partition, 100, null, cancellationToken)
            .ConfigureAwait(false);
        var page = cursor is null ? first : await GetMarketDataDownloadHistoryAsync(partition, 100, cursor, cancellationToken)
            .ConfigureAwait(false);
        var selected = page.Attempts.FirstOrDefault(r => requiredImportCommandId.HasValue ? r.Outcome.ImportCommandId == requiredImportCommandId : r.Outcome.Status == MarketDataDownloadStatus.Completed);
        var success = selected?.Outcome.Status == MarketDataDownloadStatus.Completed ? selected : null;
        return new(success is not null, first.Attempts.FirstOrDefault(), success, page.Continuation is null, selected is null ? page.Continuation : null, requiredImportCommandId.HasValue ? selected : null);
    }

    internal static MarketDataDownloadLogReadModel MapToDownloadLog(IObjectDataRecord row)
    {
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = Enum.Parse<MarketDataDownloadDataset>(row.GetString(0)),
            Provider = row.GetString(1),
            Scope = row.GetString(2),
            ValueDate = row.GetDateOnly(3),
            RequestedAtUtc = DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc),
            ImportCommandId = row.GetGuid(5),
            SourceTerminalEventId = row.GetGuid(7),
            SchemaVersion = row.GetShort(8),
            Status = Enum.Parse<MarketDataDownloadStatus>(row.GetString(9)),
            StartedAtUtc = DateTime.SpecifyKind(row.GetDateTime(10), DateTimeKind.Utc),
            FinishedAtUtc = DateTime.SpecifyKind(row.GetDateTime(11), DateTimeKind.Utc),
            ElapsedMilliseconds = row.GetLong(12),
            DownloadedRecordCount = row.IsNull(13) ? null : row.GetLong(13),
            PersistedRecordCount = row.IsNull(14) ? null : row.GetLong(14),
            ErrorCode = row.IsNull(15) ? null : row.GetString(15),
            ErrorMessage = row.IsNull(16) ? null : row.GetString(16),
        };
        outcome.Validate();
        var result = new MarketDataDownloadLogReadModel(outcome, row.GetGuid(6), row.GetString(17), DateTime.SpecifyKind(row.GetDateTime(18), DateTimeKind.Utc));
        if (result.LogCommandId != MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId) || result.PayloadSha256 != outcome.ComputeHash())
            throw new InvalidOperationException("DownloadLog read model failed integrity validation.");
        return result;
    }

    internal const int EconomicCalendarMaximumRangeMonths = EconomicCalendarQueryLimits.MaximumRangeMonths;
    internal const int EconomicCalendarMaximumRows = 10_000;
    internal const int EconomicCalendarMaximumRowsPerMonth = EconomicCalendarQueryLimits.MaximumRowsPerPartition;
    internal const int EconomicCalendarMaximumConcurrentQueries = 4;
    internal const int EconomicCalendarLookupId = 1;
    internal const int EconomicCalendarCutoverId = 1;
    internal static EconomicCalendarReadModel MapToEconomicCalendar(IObjectDataRecord row) => new()
    {
        EventDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.GetDateTime(0)),
        CountryCode = row.GetString(1),
        EventName = row.GetString(2),
        Actual = MarketDataDbContextExtensions.GetNullableString(row, 3),
        Forecast = MarketDataDbContextExtensions.GetNullableString(row, 4),
        Prior = MarketDataDbContextExtensions.GetNullableString(row, 5),
        Impact = MarketDataDbContextExtensions.GetNullableString(row, 6),
        Unit = MarketDataDbContextExtensions.GetNullableString(row, 7),
        Change = MarketDataDbContextExtensions.GetNullableString(row, 8),
        ChangePercentage = MarketDataDbContextExtensions.GetNullableString(row, 9),
        CreatedOn = row.GetDateTime(10),
        CreatedBy = row.GetString(11)
    };
    internal static EconomicCalendarCountryCodeReadModel MapToEconomicCalendarCountryCode(IObjectDataRecord row) => new(row.GetString(0));
    public Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id) => GetEconomicCalendarAsync(id, CancellationToken.None);
    public async Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id, CancellationToken cancellationToken)
    {
        var eventDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(id.EventDate);
        return await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetEconomicCalendarV2ById)}", MarketDataDbCql.GetEconomicCalendarV2ById)
            .SetParameters(new GetEconomicCalendarV2ById(id.CountryCode, MarketDataDbContextExtensions.EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName))
            .ExecuteSingleAsync(MapToEconomicCalendar!, cancellationToken);
    }

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode) => GetEconomicCalendarsAsync(eventDate, countryCode, CancellationToken.None);
    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode, CancellationToken cancellationToken)
    {
        var startDate = eventDate.Date;
        var endDate = startDate == DateTime.MaxValue.Date ? DateTime.MaxValue : startDate.AddDays(1).AddTicks(-1);
        return GetEconomicCalendarsAsync(startDate, endDate, countryCode, cancellationToken);
    }

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode) => GetEconomicCalendarsAsync(startDate, endDate, countryCode, CancellationToken.None);
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode, CancellationToken cancellationToken)
    {
        startDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(startDate);
        endDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(endDate);
        if (endDate < startDate)
            return [];
        var request = new EconomicCalendarPageRequest
        {
            StartDateUtc = startDate,
            EndDateUtc = endDate,
            CountryCodes = [countryCode],
            PageSize = EconomicCalendarQueryLimits.MaximumPageSize
        };
        var rows = new List<EconomicCalendarReadModel>();
        do
        {
            var page = await GetEconomicCalendarPageAsync(request, cancellationToken)
                .ConfigureAwait(false);
            rows.AddRange(page.Items);
            if (!page.HasMore || rows.Count >= EconomicCalendarMaximumRows)
                break;
            request = request with
            {
                ContinuationToken = page.ContinuationToken
            };
        }
        while (true);
        return [.. rows.Take(EconomicCalendarMaximumRows)];
    }

    public async Task<EconomicCalendarPageReadModel> GetEconomicCalendarPageAsync(EconomicCalendarPageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var countries = request.CountryCodes.Select(static code => code.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var partitions = MarketDataDbContextExtensions.EconomicCalendarMonthBucketsDescending(request.StartDateUtc, request.EndDateUtc).SelectMany(month => countries.Select(country => new CalendarPartition(country, month))).ToArray();
        var fingerprint = MarketDataDbContextExtensions.GetPageRequestFingerprint(request, countries);
        var cursor = MarketDataDbContextExtensions.DecodePageToken(request.ContinuationToken, fingerprint, partitions.Length);
        var rows = new List<EconomicCalendarReadModel>(request.PageSize);
        for (var partitionIndex = cursor.PartitionIndex; partitionIndex < partitions.Length; partitionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partition = partitions[partitionIndex];
            var partitionRows = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetEconomicCalendars)}", MarketDataDbCql.GetEconomicCalendars)
                .SetParameters(new GetEconomicCalendars(partition.CountryCode, partition.MonthBucket, request.StartDateUtc, request.EndDateUtc))
                .ExecuteQueryAsync(MapToEconomicCalendar!, cancellationToken)
                .ConfigureAwait(false);
            if (partitionRows.Count > EconomicCalendarMaximumRowsPerMonth)
                throw new InvalidOperationException($"Economic-calendar partition '{partition.CountryCode}/{partition.MonthBucket}' exceeds the configured row bound.");
            var available = partitionRows.OrderByDescending(static row => row.EventDate).ThenBy(static row => row.EventName, StringComparer.Ordinal).Where(row => partitionIndex != cursor.PartitionIndex || MarketDataDbContextExtensions.IsAfterCursor(row, cursor)).ToArray();
            var take = Math.Min(request.PageSize - rows.Count, available.Length);
            rows.AddRange(available.Take(take));
            if (rows.Count == request.PageSize)
            {
                var last = rows[^1];
                var hasMore = take < available.Length || partitionIndex + 1 < partitions.Length;
                return new EconomicCalendarPageReadModel
                {
                    Items = [.. rows],
                    ContinuationToken = hasMore ? MarketDataDbContextExtensions.EncodePageToken(new CalendarPageToken(fingerprint, partitionIndex, last.EventDate.Ticks, last.EventName)) : null
                };
            }

            cursor = new CalendarPageToken(fingerprint, partitionIndex + 1, null, null);
        }

        return new EconomicCalendarPageReadModel
        {
            Items = [.. rows]
        };
    }

    [Obsolete("Use GetEconomicCalendarPageAsync with explicit UTC bounds and country codes.")]
    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync() => GetEconomicCalendarAllAsync(CancellationToken.None);
    [Obsolete("Use GetEconomicCalendarPageAsync with explicit UTC bounds and country codes.")]
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync(CancellationToken cancellationToken)
    {
        var countries = await GetEconomicCalendarCountryCodesAsync(cancellationToken)
            .ConfigureAwait(false);
        if (countries.Count == 0)
            return [];
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-107);
        var end = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(13).AddTicks(-1);
        var rows = new List<EconomicCalendarReadModel>();
        var maximumCountriesPerBatch = Math.Max(1, EconomicCalendarQueryLimits.MaximumPartitions / EconomicCalendarQueryLimits.MaximumRangeMonths);
        var countryCodes = countries.Select(static row => row.CountryCode).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var countryBatch in countryCodes.Chunk(maximumCountriesPerBatch))
        {
            var request = new EconomicCalendarPageRequest
            {
                StartDateUtc = start,
                EndDateUtc = end,
                CountryCodes = countryBatch,
                PageSize = EconomicCalendarQueryLimits.MaximumPageSize
            };
            do
            {
                var page = await GetEconomicCalendarPageAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                rows.AddRange(page.Items);
                if (!page.HasMore || rows.Count >= EconomicCalendarMaximumRows)
                    break;
                request = request with
                {
                    ContinuationToken = page.ContinuationToken
                };
            }
            while (true);
            if (rows.Count >= EconomicCalendarMaximumRows)
                break;
        }

        return [.. rows.OrderByDescending(static row => row.EventDate).ThenBy(static row => row.CountryCode, StringComparer.Ordinal).ThenBy(static row => row.EventName, StringComparer.Ordinal).Take(EconomicCalendarMaximumRows)];
    }

    public Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync() => GetEconomicCalendarCountryCodesAsync(CancellationToken.None);
    public async Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetEconomicCalendarCountryCodes)}", MarketDataDbCql.GetEconomicCalendarCountryCodes)
        .SetParameters(new GetEconomicCalendarCountryCodes(EconomicCalendarLookupId))
        .ExecuteQueryAsync(MapToEconomicCalendarCountryCode, cancellationToken);
    public async Task DeleteEconomicCalendarAsync(EconomicCalendarId id)
    {
        var eventDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(id.EventDate);
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteEconomicCalendarV2)}", MarketDataDbCql.DeleteEconomicCalendarV2)
            .SetParameters(new DeleteEconomicCalendarV2(id.CountryCode, MarketDataDbContextExtensions.EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName))
            .ExecuteCommandAsync();
    }

    public Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar) => InsertEconomicCalendarsAsync([economicCalendar]);
    public Task InsertEconomicCalendarsAsync(EconomicCalendarReadModel[] economicCalendars) => InsertEconomicCalendarsAsync(economicCalendars, ImportDuplicatePolicy.Overwrite, Guid.Empty);
    public async Task InsertEconomicCalendarsAsync(EconomicCalendarReadModel[] economicCalendars, ImportDuplicatePolicy duplicatePolicy, Guid commandId)
    {
        ArgumentNullException.ThrowIfNull(economicCalendars);
        if (economicCalendars.Length == 0)
            return;
        MarketDataDbContextExtensions.ValidateImportPolicy(duplicatePolicy, commandId);
        var db = _dbFactory.MarketDataDb;
        var commands = new List<object>(economicCalendars.Length * 2);
        var countryCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in economicCalendars)
        {
            var eventDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.EventDate);
            var parameters = new InsertEconomicCalendarV2(row.CountryCode, MarketDataDbContextExtensions.EconomicCalendarMonthBucket(eventDate), eventDate, row.EventName, row.Actual, row.Forecast, row.Prior, row.Impact, row.Unit, row.Change, row.ChangePercentage, row.CreatedOn, row.CreatedBy, commandId);
            if (duplicatePolicy == ImportDuplicatePolicy.Reject)
            {
                var applied = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertEconomicCalendarV2IfNotExists)}", MarketDataDbCql.InsertEconomicCalendarV2IfNotExists)
                    .SetParameters(parameters)
                    .ExecuteScalarAsync(MapToBoolean!);
                if (!applied)
                {
                    var owner = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetEconomicCalendarV2CommandId)}", MarketDataDbCql.GetEconomicCalendarV2CommandId)
                        .SetParameters(new GetEconomicCalendarV2CommandId(row.CountryCode, MarketDataDbContextExtensions.EconomicCalendarMonthBucket(eventDate), eventDate, row.EventName))
                        .ExecuteSingleAsync(MapToGuid!);
                    if (owner != commandId)
                        throw new MarketDataImportDuplicateException($"An economic-calendar row with logical key '{eventDate:O}|{row.CountryCode}|{row.EventName}' already exists.");
                }
            }
            else
            {
                commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertEconomicCalendarV2)}", MarketDataDbCql.InsertEconomicCalendarV2)
                    .SetParameters(parameters)
                    .QueueCommand());
            }

            countryCodes.Add(row.CountryCode);
        }

        commands.AddRange(countryCodes.Select(countryCode => db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertEconomicCalendarCountryCode)}", MarketDataDbCql.InsertEconomicCalendarCountryCode)
            .SetParameters(new InsertEconomicCalendarCountryCode(EconomicCalendarLookupId, countryCode))
            .QueueCommand()));
        if (commands.Count > 0)
            await db.ExecuteQueuedCommandsAsync(commands);
    }

    public async Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar)
    {
        await DeleteEconomicCalendarAsync(id);
        await InsertEconomicCalendarAsync(economicCalendar);
    }

    public async Task<EconomicCalendarCutoverReadModel> BackfillEconomicCalendarV2Async(int batchSize = 256, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var db = _dbFactory.MarketDataDb;
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateEconomicCalendarV2)}", MarketDataDbCql.TruncateEconomicCalendarV2)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateEconomicCalendarCountryCode)}", MarketDataDbCql.TruncateEconomicCalendarCountryCode)
            .ExecuteCommandAsync(cancellationToken);
        long sourceRows = 0;
        var sourceIdentity = new ProjectionIdentityBuilder();
        var countries = new HashSet<string>(StringComparer.Ordinal);
        var batch = new List<InsertEconomicCalendarV2>(batchSize);
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetEconomicCalendarLegacySource)}", MarketDataDbCql.GetEconomicCalendarLegacySource)
            .ExecuteStreamAsync(MapToEconomicCalendar!, cancellationToken))
        {
            sourceRows++;
            sourceIdentity.Add(MarketDataDbContextExtensions.GetEconomicCalendarProjectionIdentity(row));
            countries.Add(row.CountryCode);
            var eventDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.EventDate);
            batch.Add(new InsertEconomicCalendarV2(row.CountryCode, MarketDataDbContextExtensions.EconomicCalendarMonthBucket(eventDate), eventDate, row.EventName, row.Actual, row.Forecast, row.Prior, row.Impact, row.Unit, row.Change, row.ChangePercentage, row.CreatedOn, row.CreatedBy, Guid.Empty));
            if (batch.Count == batchSize)
                await FlushCalendarBatchAsync();
        }

        await FlushCalendarBatchAsync();
        if (countries.Count > 0)
        {
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertEconomicCalendarCountryCode)}", MarketDataDbCql.InsertEconomicCalendarCountryCode)
                .SetParameters(countries.Select(country => new InsertEconomicCalendarCountryCode(EconomicCalendarLookupId, country)))
                .ExecuteCommandAsync(cancellationToken);
        }

        long targetRows = 0;
        var targetIdentity = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetEconomicCalendarV2All)}", MarketDataDbCql.GetEconomicCalendarV2All)
            .ExecuteStreamAsync(MapToEconomicCalendar!, cancellationToken))
        {
            targetRows++;
            targetIdentity.Add(MarketDataDbContextExtensions.GetEconomicCalendarProjectionIdentity(row));
        }

        var source = sourceIdentity.Build();
        var target = targetIdentity.Build();
        var verified = sourceRows == targetRows && source.Fingerprint == target.Fingerprint;
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpsertEconomicCalendarCutoverV2)}", MarketDataDbCql.UpsertEconomicCalendarCutoverV2)
            .SetParameters(new UpsertEconomicCalendarCutoverV2(EconomicCalendarCutoverId, sourceRows, targetRows, source.Fingerprint, target.Fingerprint, verified, DateTime.UtcNow))
            .ExecuteCommandAsync(cancellationToken);
        return new EconomicCalendarCutoverReadModel(sourceRows, targetRows, source.Fingerprint, target.Fingerprint, countries.Count, verified);
        async Task FlushCalendarBatchAsync()
        {
            if (batch.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertEconomicCalendarV2)}", MarketDataDbCql.InsertEconomicCalendarV2)
                .SetParameters(batch)
                .ExecuteCommandAsync(cancellationToken);
            batch.Clear();
        }
    }

    public async Task<FmpQueryProjectionBackfillReadModel> BackfillFmpQueryProjectionsAsync(int batchSize = 256, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var db = _dbFactory.MarketDataDb;
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateYieldCurveRateByDate)}", MarketDataDbCql.TruncateYieldCurveRateByDate)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateYieldCurveRateYear)}", MarketDataDbCql.TruncateYieldCurveRateYear)
            .ExecuteCommandAsync(cancellationToken);
        long sourceRows = 0;
        var sourceIdentity = new ProjectionIdentityBuilder();
        var years = new HashSet<int>();
        var batch = new List<InsertYieldCurveRate>(batchSize);
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRateProjectionSource)}", MarketDataDbCql.GetYieldCurveRateProjectionSource)
            .ExecuteStreamAsync(MapToYieldCurveRate!, cancellationToken))
        {
            sourceRows++;
            sourceIdentity.Add(MarketDataDbContextExtensions.GetYieldCurveProjectionIdentity(row));
            years.Add(row.ValueDate.Year);
            batch.Add(new InsertYieldCurveRate(YieldCurveLookupId, row.ValueDate, row.OneMonth, row.TwoMonth, row.ThreeMonth, row.SixMonth, row.OneYear, row.TwoYear, row.ThreeYear, row.FiveYear, row.SevenYear, row.TenYear, row.TwentyYear, row.ThirtyYear));
            if (batch.Count == batchSize)
                await FlushYieldBatchAsync();
        }

        await FlushYieldBatchAsync();
        if (years.Count > 0)
        {
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRateYear)}", MarketDataDbCql.InsertYieldCurveRateYear)
                .SetParameters(years.Select(year => new InsertYieldCurveRateYear(YieldCurveLookupId, year)))
                .ExecuteCommandAsync(cancellationToken);
        }

        long targetRows = 0;
        var targetIdentity = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRateByDateAll)}", MarketDataDbCql.GetYieldCurveRateByDateAll)
            .ExecuteStreamAsync(MapToYieldCurveRate!, cancellationToken))
        {
            targetRows++;
            targetIdentity.Add(MarketDataDbContextExtensions.GetYieldCurveProjectionIdentity(row));
        }

        var projectedYears = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRateYearAll)}", MarketDataDbCql.GetYieldCurveRateYearAll)
            .ExecuteQueryAsync(MapToYearMonth, cancellationToken);
        var source = sourceIdentity.Build();
        var target = targetIdentity.Build();
        var sourceYears = MarketDataDbContextExtensions.BuildIntegerSetIdentity(years);
        var targetYears = MarketDataDbContextExtensions.BuildIntegerSetIdentity(projectedYears);
        return new FmpQueryProjectionBackfillReadModel(sourceRows, targetRows, source.Fingerprint, target.Fingerprint, years.Count, projectedYears.Count, sourceYears.Fingerprint, targetYears.Fingerprint);
        async Task FlushYieldBatchAsync()
        {
            if (batch.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRateByDate)}", MarketDataDbCql.InsertYieldCurveRateByDate)
                .SetParameters(batch)
                .ExecuteCommandAsync(cancellationToken);
            batch.Clear();
        }
    }

    /// <summary>
    /// Copies malformed legacy rows to an idempotent quarantine table and rebuilds
    /// lookup entries from valid rows. Canonical source rows are never deleted.
    /// </summary>
    public async Task<FuturesTradeSignalRepairReadModel> RepairFuturesTradeSignalLookupAsync(int batchSize = 256, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var db = _dbFactory.MarketDataDb;
        var latestRows = new Dictionary<string, FuturesTradeSignalRepairRow>(StringComparer.Ordinal);
        var dateRows = new Dictionary<(string TimePeriod, DateOnly ValueDate, string ContractId), FuturesTradeSignalRepairRow>();
        List<InsertFuturesTradeSignalQuarantine> quarantinedRows = [];
        long rowsScanned = 0;
        long validRowCount = 0;
        long quarantinedRowCount = 0;
        await foreach (var payload in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalJsonAll)}", MarketDataDbCql.GetFuturesTradeSignalJsonAll)
            .ExecuteStreamAsync(MarketDataDbContextExtensions.MapJsonPayload, cancellationToken))
        {
            rowsScanned++;
            var parsed = MarketDataDbContextExtensions.ParseFuturesTradeSignalRepairRow(payload);
            if (parsed.Row is { } row)
            {
                validRowCount++;
                if (!latestRows.TryGetValue(row.TimePeriod, out var latest) || MarketDataDbContextExtensions.IsNewer(row, latest))
                    latestRows[row.TimePeriod] = row;
                var dateKey = (row.TimePeriod, row.ValueDate, row.ContractId);
                if (!dateRows.TryGetValue(dateKey, out var dateLatest) || MarketDataDbContextExtensions.IsNewer(row, dateLatest))
                    dateRows[dateKey] = row;
                continue;
            }

            quarantinedRowCount++;
            quarantinedRows.Add(new InsertFuturesTradeSignalQuarantine(MarketDataDbContextExtensions.Fingerprint(payload), payload, parsed.Error ?? "Malformed Futures Trade Signal row", DateTime.UtcNow));
            if (quarantinedRows.Count >= batchSize)
                await FlushQuarantineAsync()
                    .ConfigureAwait(false);
        }

        await FlushQuarantineAsync()
            .ConfigureAwait(false);
        var latestLookupRows = latestRows.Values.Select(static row => new InsertFuturesTradeSignalIndex($"latest:{row.TimePeriod}", "latest", row.SequenceId, row.ContractId, row.ValueDate, row.TimePeriod));
        var dateLookupRows = dateRows.Values.Select(static row => new InsertFuturesTradeSignalIndex($"date:{row.TimePeriod}:{row.ValueDate.DayNumber}", row.ContractId, row.SequenceId, row.ContractId, row.ValueDate, row.TimePeriod));
        var lookupRows = latestLookupRows.Concat(dateLookupRows).ToArray();
        for (var offset = 0; offset < lookupRows.Length; offset += batchSize)
        {
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
                .SetParameters(lookupRows.Skip(offset).Take(batchSize))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new FuturesTradeSignalRepairReadModel(rowsScanned, validRowCount, quarantinedRowCount, lookupRows.Length);
        async Task FlushQuarantineAsync()
        {
            if (quarantinedRows.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalQuarantine)}", MarketDataDbCql.InsertFuturesTradeSignalQuarantine)
                .SetParameters(quarantinedRows)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            quarantinedRows.Clear();
        }
    }

    internal const string FuturesItiSignalQueryProjection = "futures_iti_signal_queries";
    internal static FuturesItiProjectionScopeData MapToFuturesItiProjectionScope<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => new(row.GetString(0), row.GetDateOnly(1), row.GetString(2), row.GetString(3));
    internal static readonly MessagePackSerializerOptions MarketOutlookSerializerOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance).WithCompression(MessagePackCompression.Lz4BlockArray);
    public async Task UpsertMarketOutlookSnapshotAsync(MarketOutlookReadModel snapshot, long revision = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var payload = MessagePackSerializer.Serialize(snapshot, MarketOutlookSerializerOptions);
        var eod = MessagePackSerializer.Serialize(snapshot.FuturesEodData, MarketOutlookSerializerOptions);
        var tradeSignal = snapshot.FuturesTradeSignal is null ? null : MessagePackSerializer.Serialize(snapshot.FuturesTradeSignal, MarketOutlookSerializerOptions);
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpsertMarketOutlookSnapshot)}", MarketDataDbCql.UpsertMarketOutlookSnapshot)
            .SetParameters(new UpsertMarketOutlookSnapshot(snapshot.ContractId, snapshot.ValueDate, revision, snapshot.UpdatedAtUtc, eod, tradeSignal, snapshot.MissingInputs, payload))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MarketOutlookReadModel?> GetMarketOutlookSnapshotAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contractId) || valueDate == default)
            return null;
        return await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketOutlookSnapshot)}", MarketDataDbCql.GetMarketOutlookSnapshot)
            .SetParameters(new GetMarketOutlookSnapshot(contractId, valueDate))
            .ExecuteSingleAsync(MapToMarketOutlookSnapshot, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static MarketOutlookReadModel MapToMarketOutlookSnapshot(IObjectDataRecord row)
    {
        var contractId = row.GetString(1);
        var valueDate = row.GetDateOnly(2);
        var payload = row.GetBytes(0);
        var snapshot = payload.Length == 0 ? MapToLegacyMarketOutlookSnapshot(row, contractId, valueDate) : MessagePackSerializer.Deserialize<MarketOutlookReadModel>(payload, MarketOutlookSerializerOptions);
        if (!string.Equals(snapshot.ContractId, contractId, StringComparison.Ordinal) || snapshot.ValueDate != valueDate)
            throw new InvalidDataException($"Market Outlook snapshot payload identity '{snapshot.ContractId}.{snapshot.ValueDate:yyyyMMdd}' " + $"does not match row identity '{contractId}.{valueDate:yyyyMMdd}'.");
        return snapshot;
    }

    internal static MarketOutlookReadModel MapToLegacyMarketOutlookSnapshot(IObjectDataRecord row, string contractId, DateOnly valueDate)
    {
        var eodPayload = row.GetBytes(5);
        if (eodPayload.Length == 0)
            throw new InvalidDataException($"Market Outlook row '{contractId}.{valueDate:yyyyMMdd}' has neither a snapshot nor legacy EOD data.");
        var updatedAtUtc = row.GetDateTime(4);
        var tradeSignalPayload = row.GetBytes(6);
        var eod = MessagePackSerializer.Deserialize<FuturesEodDataV2ReadModel>(eodPayload, MarketOutlookSerializerOptions);
        var tradeSignal = tradeSignalPayload.Length == 0 ? null : MessagePackSerializer.Deserialize<FuturesTradeSignalV2ReadModel>(tradeSignalPayload, MarketOutlookSerializerOptions);
        return new MarketOutlookReadModel
        {
            ContractId = contractId,
            ValueDate = valueDate,
            UpdatedAtUtc = updatedAtUtc,
            MarketDataAsOfUtc = updatedAtUtc,
            RefreshTrigger = MarketOutlookRefreshTrigger.PersistedBaseline,
            FuturesEodData = eod,
            FuturesTradeSignal = tradeSignal,
            MissingInputs = row.IsNull(7) ? string.Empty : row.GetString(7),
            EsPriceAvailability = eod.IsValid ? MarketOutlookInputAvailability.Available : MarketOutlookInputAvailability.Unavailable
        };
    }

    internal const string EmaConfigurationId = "ema-10-20-50-200-v1";
    internal const string BollingerBandConfigurationId = "bb-10-20-ema-center-population-v1";
    /// <inheritdoc/>
    public Task InsertFuturesEmaSignalAsync(FuturesEmaSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEmaSignal)}", MarketDataDbCql.InsertFuturesEmaSignal)
            .SetParameters(new InsertFuturesEmaSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10, signal.PreviousEma10, signal.Ema10Slope, signal.Ema20, signal.PreviousEma20, signal.Ema20Slope, signal.Ema50, signal.PreviousEma50, signal.Ema50Slope, signal.Ema200, signal.PreviousEma200, signal.Ema200Slope, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task InsertFuturesBollingerBandSignalAsync(FuturesBbSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBollingerBandSignal)}", MarketDataDbCql.InsertFuturesBollingerBandSignal)
            .SetParameters(new InsertFuturesBollingerBandSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10Center, signal.StandardDeviation10, signal.Upper10, signal.Lower10, signal.Width10, signal.Position10, signal.Ema20Center, signal.StandardDeviation20, signal.Upper20, signal.Lower20, signal.Width20, signal.Position20, signal.Width20Baseline, signal.Width20Ratio, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<FuturesEmaSignalReadModel?> GetLatestFuturesEmaSignalAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, CancellationToken cancellationToken = default) => this.ReadLatestAsync(seriesIdentity, valueDate, EmaConfigurationId, MarketDataDbCql.GetLatestFuturesEmaSignal, nameof(MarketDataDbCql.GetLatestFuturesEmaSignal), MapToFuturesEmaSignal, cancellationToken);
    /// <inheritdoc/>
    public Task<FuturesBbSignalReadModel?> GetLatestFuturesBollingerBandSignalAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, CancellationToken cancellationToken = default) => this.ReadLatestAsync(seriesIdentity, valueDate, BollingerBandConfigurationId, MarketDataDbCql.GetLatestFuturesBollingerBandSignal, nameof(MarketDataDbCql.GetLatestFuturesBollingerBandSignal), MapToFuturesBollingerBandSignal, cancellationToken);
    public Task<FuturesBbSignalReadModel?> GetLatestFuturesBollingerBandSignalForTimeFrameAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, TimeFrameType timeFrame, CancellationToken cancellationToken = default)
    {
        if (timeFrame is not TimeFrameType.FiveMinutes)
            throw new ArgumentOutOfRangeException(nameof(timeFrame));
        return this.ReadMonthAsync(seriesIdentity, valueDate, valueDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), BollingerBandConfigurationId, MarketDataDbCql.GetLatestFuturesBollingerBandSignal, nameof(MarketDataDbCql.GetLatestFuturesBollingerBandSignal), MapToFuturesBollingerBandSignal, cancellationToken, timeFrame);
    }

    internal static FuturesEmaSignalReadModel MapToFuturesEmaSignal(IObjectDataRecord value)
    {
        var metadata = MapToMetadata(value, MarketAnalyticsSignalKind.Ema);
        return new()
        {
            Metadata = metadata,
            Price = value.GetDecimal(7),
            Ema10 = MarketDataDbContextExtensions.Decimal(value, 8),
            PreviousEma10 = MarketDataDbContextExtensions.Decimal(value, 9),
            Ema10Slope = MarketDataDbContextExtensions.Decimal(value, 10),
            Ema20 = MarketDataDbContextExtensions.Decimal(value, 11),
            PreviousEma20 = MarketDataDbContextExtensions.Decimal(value, 12),
            Ema20Slope = MarketDataDbContextExtensions.Decimal(value, 13),
            Ema50 = MarketDataDbContextExtensions.Decimal(value, 14),
            PreviousEma50 = MarketDataDbContextExtensions.Decimal(value, 15),
            Ema50Slope = MarketDataDbContextExtensions.Decimal(value, 16),
            Ema200 = MarketDataDbContextExtensions.Decimal(value, 17),
            PreviousEma200 = MarketDataDbContextExtensions.Decimal(value, 18),
            Ema200Slope = MarketDataDbContextExtensions.Decimal(value, 19),
            IsWarm = value.GetBool(20),
            BaselineValueDate = metadata.ValueDate
        };
    }

    internal static FuturesBbSignalReadModel MapToFuturesBollingerBandSignal(IObjectDataRecord value)
    {
        var metadata = MapToMetadata(value, MarketAnalyticsSignalKind.BollingerBand, 23);
        return new()
        {
            Metadata = metadata,
            Price = value.GetDecimal(7),
            Ema10Center = MarketDataDbContextExtensions.Decimal(value, 8),
            StandardDeviation10 = MarketDataDbContextExtensions.Decimal(value, 9),
            Upper10 = MarketDataDbContextExtensions.Decimal(value, 10),
            Lower10 = MarketDataDbContextExtensions.Decimal(value, 11),
            Width10 = MarketDataDbContextExtensions.Decimal(value, 12),
            Position10 = MarketDataDbContextExtensions.Decimal(value, 13),
            Ema20Center = MarketDataDbContextExtensions.Decimal(value, 14),
            StandardDeviation20 = MarketDataDbContextExtensions.Decimal(value, 15),
            Upper20 = MarketDataDbContextExtensions.Decimal(value, 16),
            Lower20 = MarketDataDbContextExtensions.Decimal(value, 17),
            Width20 = MarketDataDbContextExtensions.Decimal(value, 18),
            Position20 = MarketDataDbContextExtensions.Decimal(value, 19),
            Width20Baseline = MarketDataDbContextExtensions.Decimal(value, 20),
            Width20Ratio = MarketDataDbContextExtensions.Decimal(value, 21),
            IsWarm = value.GetBool(22),
            BaselineValueDate = metadata.ValueDate
        };
    }

    internal static MarketAnalyticsSignalMetadata MapToMetadata(IObjectDataRecord value, MarketAnalyticsSignalKind kind, int sourceSequenceIndex = 21)
    {
        var series = MarketSeriesIdentity.Parse(value.GetString(0));
        var timeFrame = value.GetEnum<TimeFrameType>(1);
        var calculatedAtIndex = sourceSequenceIndex + 1;
        var schemaVersionIndex = sourceSequenceIndex + 2;
        var calculationVersionIndex = sourceSequenceIndex + 3;
        var calculationMethodIndex = sourceSequenceIndex + 4;
        var isValidIndex = sourceSequenceIndex + 5;
        return new()
        {
            SignalKey = new(series, kind, timeFrame, value.GetString(2)),
            ContractId = value.GetString(5),
            ValueDate = value.GetDateOnly(6),
            ObservationId = new FuturesTradeSessionBarId(value.GetGuid(4)),
            MarketDataAsOfUtc = new DateTimeOffset(DateTime.SpecifyKind(value.GetDateTime(3), DateTimeKind.Utc)),
            CalculatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(value.GetDateTime(calculatedAtIndex), DateTimeKind.Utc)),
            SourceSequence = value.GetLong(sourceSequenceIndex),
            SchemaVersion = checked((ushort)value.GetInt(schemaVersionIndex)),
            CalculationVersion = value.GetString(calculationVersionIndex),
            CalculationMethod = value.GetEnum<MarketSignalCalculationMethod>(calculationMethodIndex),
            IsValid = value.GetBool(isValidIndex)
        };
    }

    /// <inheritdoc/>
    public Task InsertFuturesVwapSignalAsync(FuturesVwapSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesVwapSignal)}", MarketDataDbCql.InsertFuturesVwapSignal)
            .SetParameters(new InsertFuturesVwapSignal(signal.ContractId, signal.ValueDate, signal.ConfigurationId, signal.AsOfUtc.UtcDateTime, signal.LastTradeOrdinal, signal.SessionStartUtc.UtcDateTime, signal.SessionEndUtc.UtcDateTime, signal.CumulativePriceVolume, signal.CumulativeVolume, signal.EligibleTradeCount, signal.RejectedTradeCount, signal.LastPrice, signal.Vwap, signal.PriceMinusVwap, signal.PriceToVwapPercent, signal.LastTradeSourceSequence, signal.StreamEpochId, signal.IsWarm, signal.IsValid, signal.InvalidReason.ToString(), signal.IsTickExact, signal.CalculationMethod.ToString(), signal.SchemaVersion, signal.CalculationVersion))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<FuturesVwapSignalReadModel?> GetLatestFuturesVwapSignalAsync(string contractId, DateOnly valueDate, string configurationId, CancellationToken cancellationToken = default) => _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLatestFuturesVwapSignal)}", MarketDataDbCql.GetLatestFuturesVwapSignal)
        .SetParameters(new GetLatestFuturesVwapSignal(contractId, valueDate, configurationId))
        .ExecuteSingleAsync(MapToFuturesVwapSignal!, cancellationToken);
    /// <inheritdoc/>
    public async Task<ICollection<FuturesVwapSignalReadModel>> GetFuturesVwapSignalHistoryAsync(string contractId, DateOnly valueDate, string configurationId, CancellationToken cancellationToken = default) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesVwapSignalHistory)}", MarketDataDbCql.GetFuturesVwapSignalHistory)
        .SetParameters(new GetFuturesVwapSignalHistory(contractId, valueDate, configurationId))
        .ExecuteQueryAsync(MapToFuturesVwapSignal!, cancellationToken);
    internal static FuturesVwapSignalReadModel MapToFuturesVwapSignal<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => new()
        {
            ContractId = row.GetString(0),
            ValueDate = row.GetDateOnly(1),
            ConfigurationId = row.GetString(2),
            SessionStartUtc = new(row.GetDateTime(3), TimeSpan.Zero),
            SessionEndUtc = new(row.GetDateTime(4), TimeSpan.Zero),
            AsOfUtc = new(row.GetDateTime(5), TimeSpan.Zero),
            CumulativePriceVolume = row.GetDecimal(6),
            CumulativeVolume = row.GetLong(7),
            EligibleTradeCount = row.GetLong(8),
            RejectedTradeCount = row.GetLong(9),
            LastPrice = row.GetDecimal(10),
            Vwap = row.IsNull(11) ? null : row.GetDecimal(11),
            PriceMinusVwap = row.IsNull(12) ? null : row.GetDecimal(12),
            PriceToVwapPercent = row.IsNull(13) ? null : row.GetDecimal(13),
            LastTradeSourceSequence = row.GetLong(14),
            StreamEpochId = row.GetGuid(15),
            LastTradeOrdinal = row.GetLong(16),
            IsWarm = row.GetBool(17),
            IsValid = row.GetBool(18),
            InvalidReason = Enum.Parse<FuturesVwapInvalidReason>(row.GetString(19)),
            IsTickExact = row.GetBool(20),
            CalculationMethod = Enum.Parse<FuturesVwapCalculationMethod>(row.GetString(21)),
            SchemaVersion = row.GetInt(22),
            CalculationVersion = row.GetString(23)
        };
    /// <inheritdoc/>
    public Task InsertFuturesVxTermStructureSignalAsync(FuturesVxTermStructureSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesVxTermStructureSignal)}", MarketDataDbCql.InsertFuturesVxTermStructureSignal)
            .SetParameters(new InsertFuturesVxTermStructureSignal(signal.ValueDate, signal.ConfigurationId, signal.CalculatedAtUtc.UtcDateTime, signal.FrontSourceSequence, signal.BackSourceSequence, signal.FrontVxContractId, signal.FrontExpiry, signal.FrontVxPrice, signal.BackVxContractId, signal.BackExpiry, signal.BackVxPrice, signal.FrontBackSpread, signal.FrontBackRatio, signal.TermStructurePercent, signal.TermStructureState.ToString(), signal.PriorFrontBackRatio, signal.PriorTermStructurePercent, signal.FrontSourceTimestampUtc.UtcDateTime, signal.BackSourceTimestampUtc.UtcDateTime, signal.IsWarm, signal.IsValid, signal.SchemaVersion, signal.CalculationVersion))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<FuturesVxTermStructureSignalReadModel?> GetLatestFuturesVxTermStructureSignalAsync(DateOnly valueDate, string configurationId, CancellationToken cancellationToken = default) => _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLatestFuturesVxTermStructureSignal)}", MarketDataDbCql.GetLatestFuturesVxTermStructureSignal)
        .SetParameters(new GetLatestFuturesVxTermStructureSignal(valueDate, configurationId))
        .ExecuteSingleAsync(MapToFuturesVxTermStructureSignal!, cancellationToken);
    internal static FuturesVxTermStructureSignalReadModel MapToFuturesVxTermStructureSignal<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => new()
        {
            ValueDate = row.GetDateOnly(0),
            ConfigurationId = row.GetString(1),
            FrontVxContractId = row.GetString(2),
            FrontExpiry = row.GetDateOnly(3),
            FrontVxPrice = row.GetDecimal(4),
            BackVxContractId = row.GetString(5),
            BackExpiry = row.GetDateOnly(6),
            BackVxPrice = row.GetDecimal(7),
            FrontBackSpread = row.GetDecimal(8),
            FrontBackRatio = row.GetDecimal(9),
            TermStructurePercent = row.GetDecimal(10),
            TermStructureState = Enum.Parse<FuturesVxTermStructureState>(row.GetString(11)),
            PriorFrontBackRatio = row.IsNull(12) ? null : row.GetDecimal(12),
            PriorTermStructurePercent = row.IsNull(13) ? null : row.GetDecimal(13),
            FrontSourceTimestampUtc = new(row.GetDateTime(14), TimeSpan.Zero),
            BackSourceTimestampUtc = new(row.GetDateTime(15), TimeSpan.Zero),
            FrontSourceSequence = row.GetLong(16),
            BackSourceSequence = row.GetLong(17),
            CalculatedAtUtc = new(row.GetDateTime(18), TimeSpan.Zero),
            IsWarm = row.GetBool(19),
            IsValid = row.GetBool(20),
            SchemaVersion = row.GetInt(21),
            CalculationVersion = row.GetString(22)
        };
    public Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent e)
    {
        var id = e.TickDataId;
        var data = e.TradeData;
        return this.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertTickTradeData)}", MarketDataDbCql.InsertTickTradeData)
            .SetParameters(new InsertTickTradeData([(sbyte)e.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn, (long)data.SourceSequence, data.EventTimestampNanoseconds, data.ReceiveTimestampNanoseconds, (short)data.HeaderFlags, data.PriceRaw, data.Price, (long)data.Size, (short)data.Action, (short)data.Side, (short)data.DbnFlags]))
            .ExecuteCommandAsync();
    }

    /// <summary>Writes a bounded quote segment as one native CQL nested-UDT-list value.</summary>
    public Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e)
    {
        var id = e.TickDataId;
        var encoded = new TickQuoteScyllaBindValue(e.QuoteData);
        object?[] values = [(sbyte)e.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn, (short)e.EmissionReason, (short)e.QuoteCount, encoded];
        return this.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertTickQuoteData)}", MarketDataDbCql.InsertTickQuoteData)
            .SetParameters(new InsertTickQuoteData(values, encoded))
            .ExecuteCommandAsync();
    }

    internal const string CompositionPreparationSelect = "SELECT payload FROM composition_preparation WHERE workflow_id=:workflow AND input_revision=:revision;";
    internal const string CompositionPreparationInsert = "INSERT INTO composition_preparation(workflow_id,input_revision,payload) VALUES(:workflow,:revision,:payload) IF NOT EXISTS;";
    public async Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key, CancellationToken cancellationToken)
    {
        CompositionPreparationService.ValidateKey(key);
        var rows = await this.Database.Use("CompositionPreparation.Read", CompositionPreparationSelect)
            .SetParameters(new CompositionPreparationParameters([key.WorkflowId, key.InputRevision]))
            .ExecuteQueryAsync(row => MessagePackBinarySerializer.Shared.Deserialize<CompositionPreparation>(row.GetBytes(0)), cancellationToken)
            .ConfigureAwait(false);
        var result = rows.SingleOrDefault();
        if (result is not null)
        {
            CompositionPreparationService.Validate(result);
            if (result.Key != key)
                throw new InvalidOperationException("Workflow preparation conflicts with the accepted input hash.");
        }

        return result;
    }

    public async Task<CompositionPreparation> CommitAsync(CompositionPreparation proposed, CancellationToken cancellationToken)
    {
        CompositionPreparationService.Validate(proposed);
        await this.Database.Use("CompositionPreparation.Commit", CompositionPreparationInsert)
            .SetParameters(new CompositionPreparationParameters([proposed.Key.WorkflowId, proposed.Key.InputRevision, MessagePackBinarySerializer.Shared.Serialize(proposed)!]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync(proposed.Key, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Committed composition preparation could not be read back.");
    }

    public async ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        evidence.Validate();
        if (MessagePackBinarySerializer.MeasureContent(evidence) > 131072)
            throw new ArgumentException("Trade evidence exceeds its bounded payload.");
        var source = evidence.Source;
        await this.Database.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertOptionTradeEvidence)}", MarketDataDbCql.InsertOptionTradeEvidence)
            .SetParameters(new OptionTradeEvidenceParameters([source.ContractId, source.ValueDate, source.Identity, source.SourceDigest, MessagePackBinarySerializer.Shared.Serialize(evidence)!]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        var stored = await ReadAsync(source.ContractId, source.ValueDate, source.Identity, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null || stored.Source.SourceDigest != source.SourceDigest)
            throw new InvalidOperationException("Trade source identity collision or durable write not confirmed.");
    }

    public async Task<OptionTradeEvidence?> ReadAsync(string contract, DateOnly date, string sourceId, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(contract) || contract.Length > 128 || date == default || sourceId.Length != 64)
            throw new ArgumentException("Exact trade source identity is required.");
        Func<IObjectDataRecord, OptionTradeEvidence> map = row =>
        {
            var payload = row.GetBytes(1);
            if (payload.Length is 0 or > 131072)
                throw new InvalidDataException("Invalid trade evidence payload size.");
            var value = MessagePackBinarySerializer.Shared.Deserialize<OptionTradeEvidence>(payload) ?? throw new InvalidDataException("Missing trade evidence.");
            value.Validate();
            if (value.Source.ContractId != contract || value.Source.ValueDate != date || value.Source.Identity != sourceId || value.Source.SourceDigest != row.GetString(0))
                throw new InvalidDataException("Trade evidence identity mismatch.");
            return value;
        };
        var rows = await this.Database.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetOptionTradeEvidence)}", MarketDataDbCql.GetOptionTradeEvidence)
            .SetParameters(new OptionTradeEvidenceParameters([contract, date, sourceId]))
            .ExecuteQueryAsync(map, token)
            .ConfigureAwait(false);
        return rows.SingleOrDefault();
    }

    internal const int MaximumPayloadBytes = 1_048_576;
    public async Task AppendObservationAsync(string environment, OptionIvObservation observation, CancellationToken cancellationToken = default)
    {
        MarketDataDbContextExtensions.ValidateEnvironment(environment);
        MarketDataDbContextExtensions.ValidateObservation(observation);
        var existing = await this.ReadObservationAsync(environment, observation.ObservationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null && !MarketDataDbContextExtensions.Serialize(existing).AsSpan().SequenceEqual(MarketDataDbContextExtensions.Serialize(observation)))
            throw new InvalidOperationException("Observation identity conflicts with different immutable evidence.");
        if (observation.Revision > 1)
        {
            var superseded = await this.ReadObservationAsync(environment, observation.SupersedesObservationId!, cancellationToken)
                .ConfigureAwait(false);
            if (superseded is null || superseded.Series != observation.Series || superseded.ExchangeValueDate != observation.ExchangeValueDate || superseded.SamplingSlot != observation.SamplingSlot || superseded.Revision >= observation.Revision)
                throw new InvalidOperationException("An observation revision must supersede existing immutable evidence.");
        }

        var payload = MarketDataDbContextExtensions.Serialize(observation);
        var bucket = VolatilityCalendarBucket.From(observation.ExchangeValueDate).Value;
        await this.Database.Use("OptionVolatility.Observation.Id.Insert", MarketDataDbCql.InsertObservationById)
            .SetParameters(new OptionVolatilityParameters([environment, observation.ObservationId, payload]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        var stored = await this.ReadObservationAsync(environment, observation.ObservationId, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null || !MarketDataDbContextExtensions.Serialize(stored).AsSpan().SequenceEqual(payload))
            throw new InvalidOperationException("Observation identity conflicts with different immutable evidence.");
        await this.Database.Use("OptionVolatility.Observation.History.Insert", MarketDataDbCql.InsertObservationHistory)
            .SetParameters(new OptionVolatilityParameters([environment, observation.Series.SeriesId, observation.Series.MethodologyVersion, bucket, observation.ExchangeValueDate, observation.SamplingSlot, observation.Revision, observation.ObservationId, observation.AvailableAtUtc.UtcDateTime, payload]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PublishAsync(OptionIvPublication publication, CancellationToken cancellationToken = default)
    {
        MarketDataDbContextExtensions.ValidatePublication(publication);
        foreach (var observation in publication.SourceObservations)
            await AppendObservationAsync(publication.Environment, observation, cancellationToken)
                .ConfigureAwait(false);
        var metric = publication.Metric;
        var snapshot = metric.Snapshot;
        if (metric.Revision > 1)
        {
            var superseded = await GetSnapshotAsync(publication.Environment, metric.SupersedesSnapshotId!, cancellationToken)
                .ConfigureAwait(false);
            if (superseded is null || superseded.Snapshot.Series != snapshot.Series || superseded.Snapshot.MetricPolicyVersion != snapshot.MetricPolicyVersion)
                throw new InvalidOperationException("A metric revision must supersede existing evidence in the same series policy.");
        }

        var metricPayload = MarketDataDbContextExtensions.Serialize(metric);
        await this.Database.Use("OptionVolatility.Snapshot.Insert", MarketDataDbCql.InsertSnapshot)
            .SetParameters(new OptionVolatilityParameters([publication.Environment, snapshot.SnapshotId, snapshot.SnapshotDigest, metric.PublicationSequence, metricPayload]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        var sealedMetric = await GetSnapshotAsync(publication.Environment, snapshot.SnapshotId, cancellationToken)
            .ConfigureAwait(false);
        if (sealedMetric is null || !MarketDataDbContextExtensions.Serialize(sealedMetric).AsSpan().SequenceEqual(metricPayload))
            throw new InvalidOperationException("Snapshot identity conflicts with different immutable evidence.");
        await this.Database.Use("OptionVolatility.Metric.History.Insert", MarketDataDbCql.InsertMetricHistory)
            .SetParameters(new OptionVolatilityParameters([publication.Environment, snapshot.Series.SeriesId, snapshot.Series.MethodologyVersion, snapshot.MetricPolicyVersion, VolatilityCalendarBucket.From(snapshot.ExchangeValueDate).Value, snapshot.ExchangeValueDate, snapshot.SamplingSlot, metric.Revision, snapshot.SnapshotId, snapshot.AvailableAtUtc.UtcDateTime, metric.PublicationSequence, metricPayload]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        var pointer = MarketDataDbContextExtensions.Pointer(publication);
        var parameters = MarketDataDbContextExtensions.LatestValues(pointer);
        await this.Database.Use("OptionVolatility.Latest.Insert", MarketDataDbCql.InsertLatest)
            .SetParameters(parameters)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        await this.Database.Use("OptionVolatility.Latest.Advance", MarketDataDbCql.AdvanceLatest)
            .SetParameters(MarketDataDbContextExtensions.AdvanceLatestValues(pointer))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        var current = await this.ReadLatestAsync(pointer.Scope, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Latest pointer was not durably advertised.");
        if (current.PublicationSequence == pointer.PublicationSequence && current != pointer)
            throw new InvalidOperationException("Publication sequence conflicts with a different latest pointer.");
        if (current.PublicationSequence < pointer.PublicationSequence)
            throw new InvalidOperationException("Latest pointer monotonic advance failed.");
    }

    public async Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId, CancellationToken cancellationToken = default)
    {
        MarketDataDbContextExtensions.ValidateEnvironment(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        Func<IObjectDataRecord, OptionIvMetricRevision> map = row =>
        {
            var value = MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>(row.GetBytes(2));
            if (value.Snapshot.SnapshotId != snapshotId || value.Snapshot.SnapshotDigest != row.GetString(0) || value.PublicationSequence != row.GetLong(1))
                throw new InvalidDataException("Stored snapshot identity is invalid.");
            return value;
        };
        return await this.Database.Use("OptionVolatility.Snapshot.Read", MarketDataDbCql.SelectSnapshot)
            .SetParameters(new OptionVolatilityParameters([environment, snapshotId]))
            .ExecuteSingleAsync(map, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request, CancellationToken cancellationToken = default)
    {
        MarketDataDbContextExtensions.ValidateLatest(request);
        var pointer = await this.ReadLatestAsync(request.Scope, cancellationToken)
            .ConfigureAwait(false);
        if (pointer is null)
            return new(null, VolatilityFreshnessStatus.Unavailable);
        var metric = await GetSnapshotAsync(request.Scope.Environment, pointer.SnapshotId, cancellationToken)
            .ConfigureAwait(false);
        if (metric is null || metric.Snapshot.SnapshotDigest != pointer.SnapshotDigest)
            throw new InvalidDataException("Latest pointer references a missing or conflicting sealed snapshot.");
        return new(metric, request.RequestedAtUtc - pointer.AvailableAtUtc <= request.MaximumAge ? VolatilityFreshnessStatus.Accepted : VolatilityFreshnessStatus.Stale);
    }

    public async Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        MarketDataDbContextExtensions.ValidatePage(request);
        var page = await this.Database.Use("OptionVolatility.Observation.History", MarketDataDbCql.SelectObservationHistory)
            .SetParameters(MarketDataDbContextExtensions.HistoryValues(request, includePolicy: false))
            .ExecutePageAsync(row => MarketDataDbContextExtensions.Deserialize<OptionIvObservation>(row.GetBytes(0)), request.PageSize, request.PagingState, cancellationToken)
            .ConfigureAwait(false);
        var candidates = page.Items.AsEnumerable();
        if (request.SamplingSlot is not null)
            candidates = candidates.Where(x => x.SamplingSlot == request.SamplingSlot);
        if (request.Mode == VolatilityHistoricalMode.AsKnown)
            candidates = candidates.Where(x => x.AvailableAtUtc <= request.KnownAtUtc!.Value);
        var items = candidates.GroupBy(x => (x.ExchangeValueDate, x.SamplingSlot)).Select(x => x.OrderByDescending(y => y.Revision).First()).ToImmutableArray();
        return new(items, page.PagingState);
    }

    public async Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        MarketDataDbContextExtensions.ValidatePage(request);
        var page = await this.Database.Use("OptionVolatility.Metric.History", MarketDataDbCql.SelectMetricHistory)
            .SetParameters(MarketDataDbContextExtensions.HistoryValues(request, includePolicy: true))
            .ExecutePageAsync(row => MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>(row.GetBytes(0)), request.PageSize, request.PagingState, cancellationToken)
            .ConfigureAwait(false);
        var candidates = page.Items.AsEnumerable();
        if (request.SamplingSlot is not null)
            candidates = candidates.Where(x => x.Snapshot.SamplingSlot == request.SamplingSlot);
        if (request.Mode == VolatilityHistoricalMode.AsKnown)
            candidates = candidates.Where(x => x.Snapshot.AvailableAtUtc <= request.KnownAtUtc!.Value);
        var items = candidates.GroupBy(x => (x.Snapshot.ExchangeValueDate, x.Snapshot.SamplingSlot)).Select(x => x.OrderByDescending(y => y.Revision).ThenByDescending(y => y.PublicationSequence).First()).ToImmutableArray();
        return new(items, page.PagingState);
    }

    public async Task<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>> RebuildLatestCacheAsync(IEnumerable<VolatilityStorageScope> scopes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var result = ImmutableDictionary.CreateBuilder<VolatilityStorageScope, OptionIvLatestPointer>();
        foreach (var scope in scopes.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pointer = await this.ReadLatestAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            if (pointer is not null)
                result[scope] = pointer;
        }

        return result.ToImmutable();
    }

    public async ValueTask<bool> TryWriteObservationAsync(FuturesTradeSessionBarReadModel observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return await this.Database.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TryInsertObservation)}", MarketDataDbCql.TryInsertObservation)
            .SetParameters(new HistoricalObservationParameter(observation))
            .ExecuteSingleAsync(static row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false) == true;
    }

    public async ValueTask<bool> TryWriteRawEodAsync(FuturesEodObservationReadModel observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return await this.Database.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TryInsertRawEod)}", MarketDataDbCql.TryInsertRawEod)
            .SetParameters(new HistoricalRawEodParameter(observation))
            .ExecuteSingleAsync(static row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false) == true;
    }

    public ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, CancellationToken cancellationToken) => new(this.Database.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetRawEod)}", MarketDataDbCql.GetRawEod)
        .SetParameters(new HistoricalRawEodKey(seriesIdentity.Format(), MarketDataDbContextExtensions.YearMonth(valueDate), valueDate)).ExecuteSingleAsync<FuturesEodObservationReadModel?>(MapToRawEod, cancellationToken));
    public async ValueTask<IReadOnlyList<FuturesEodObservationReadModel>> GetRawEodRangeAsync(MarketSeriesIdentity seriesIdentity, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        if (startDate > endDate)
            throw new ArgumentOutOfRangeException(nameof(startDate));
        List<FuturesEodObservationReadModel> values = [];
        for (var month = new DateOnly(startDate.Year, startDate.Month, 1); month <= endDate; month = month.AddMonths(1))
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            var lower = startDate > month ? startDate : month;
            var upper = endDate < monthEnd ? endDate : monthEnd;
            var rows = await this.Database.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetRawEodRange)}", MarketDataDbCql.GetRawEodRange)
                .SetParameters(new HistoricalRawEodRangeKey(seriesIdentity.Format(), MarketDataDbContextExtensions.YearMonth(month), lower, upper))
                .ExecuteQueryAsync(MapToRawEod, cancellationToken)
                .ConfigureAwait(false);
            values.AddRange(rows);
        }

        return values.OrderBy(static value => value.ValueDate).ThenBy(static value => value.ContractId, StringComparer.Ordinal).ToArray();
    }

    internal static FuturesEodObservationReadModel MapToRawEod(IObjectDataRecord row) => new()
    {
        MarketSeriesIdentity = MarketSeriesIdentity.Parse(row.GetString(0)),
        ContractId = row.GetString(1),
        ValueDate = row.GetDateOnly(2),
        SessionStartUtc = MarketDataDbContextExtensions.Utc(row.GetDateTime(3)),
        SessionEndUtc = MarketDataDbContextExtensions.Utc(row.GetDateTime(4)),
        Open = row.GetDecimal(5),
        High = row.GetDecimal(6),
        Low = row.GetDecimal(7),
        Close = row.GetDecimal(8),
        Volume = row.GetDecimal(9),
        TradeCount = row.GetLong(10),
        PriceVolumeSum = row.GetDecimal(11),
        ObservationId = new(row.GetGuid(12)),
        FirstSourceSequence = row.GetLong(13),
        LastSourceSequence = row.GetLong(14),
        FirstMarketEventUtc = MarketDataDbContextExtensions.Utc(row.GetDateTime(15)),
        LastMarketEventUtc = MarketDataDbContextExtensions.Utc(row.GetDateTime(16)),
        SchemaVersion = checked((ushort)row.GetInt(17)),
        IsComplete = row.GetBool(18),
        IsValid = row.GetBool(19)
    };
}

/// <summary>
/// Represents a unique key for identifying a range of trading days within a specific market and currency context.
/// </summary>
/// <remarks>This record is used to encapsulate the start and end dates of a trading period, along with the
/// associated market type and currency type. It is primarily intended for scenarios where trading day ranges need to be
/// uniquely identified or compared.</remarks>
/// <param name = "StartDate"></param>
/// <param name = "EndDate"></param>
/// <param name = "MarketType"></param>
/// <param name = "CurrencyType"></param>
record TradingDaysKey(DateOnly StartDate, DateOnly EndDate, MarketType MarketType, CurrencyType CurrencyType)
{
    public override string ToString() => $"{StartDate:yyyy-MM-dd}|{EndDate:yyyy-MM-dd}|{MarketType}|{CurrencyType}";
}

internal sealed record FuturesTradeSignalRepairParseResult(FuturesTradeSignalRepairRow? Row, string? Error);
internal sealed record FuturesTradeSignalRepairRow(string ContractId, DateOnly ValueDate, string TimePeriod, TimeOnly Timestamp, long SequenceId);
readonly record struct FuturesItiProjectionScopeData(string ContractId, DateOnly ValueDate, string IntrinsicTimeTrend, string IntrinsicTimeMode);
