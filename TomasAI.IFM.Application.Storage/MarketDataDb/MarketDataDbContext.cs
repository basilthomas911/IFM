using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;


using Microsoft.Extensions.Logging;

using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;


using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;


using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
/// <summary>
/// Provides typed MarketDataDb queries and mutations over PostgreSQL.
/// </summary>
/// <param name="connectionSettings">The named PostgreSQL connection settings.</param>
/// <param name = "dbFactory">The factory supplying related database capabilities.</param>
/// <param name="sequenceIdGenerator">The database sequence allocator.</param>
/// <param name="logger">Database-provider diagnostics.</param>
/// <param name="timeProvider">The business clock used for caller-visible timestamps.</param>
/// <param name="sessionCalendar">The trading session calendar used to qualify persisted observations.</param>
public class MarketDataDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ISequenceIdGenerator sequenceIdGenerator, ILogger<DbProvider> logger, TimeProvider? timeProvider = null, IMarketSessionCalendar? sessionCalendar = null) : ObjectDataRepository<MarketDataDbContext>(connectionSettings[MarketDataDbConnection] is { ProviderName: "System.Data.Postgres" } postgresConnection
    ? postgresConnection : throw new NotSupportedException("Market Data projections require PostgreSQL; raw tick persistence is owned by TradeTickQuoteDb."), logger), IMarketDataDbContext
{
    /// <summary>Business clock for dated projections; transport and database limits remain physical.</summary>
    readonly IMarketSessionCalendar freshnessCalendar = sessionCalendar ?? new TomasAI.IFM.Application.MarketData.Databento.Historical.CmeFuturesMarketSessionCalendar();
    internal TimeProvider BusinessClock { get; } = timeProvider ?? TimeProvider.System;
    readonly ILogger _diagnosticLogger = logger;
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
    internal const int YieldCurveMaximumRows = 5_000;
    internal const int YieldCurveMaximumYears = 200;
    internal const int YieldCurveLookupId = 1;

    internal readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    internal readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);

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

    internal static EconomicCalendarReadModel MapToBaselineEconomicCalendar(System.Data.Common.DbDataReader reader)
        => MapToEconomicCalendar(new AdoNetDataRecord().SetReader(reader));

    internal static YieldCurveRateReadModel MapToBaselineYieldCurve(System.Data.Common.DbDataReader reader)
        => MapToYieldCurveRate(new AdoNetDataRecord().SetReader(reader));

    internal static TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamRevisionReadModel MapToBaselineStreamHead(System.Data.Common.DbDataReader reader)
        => new(reader.GetString(0), reader.GetInt64(1));

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static int MapToYearMonth<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetInt(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static string MapToString<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetString(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static Guid MapToGuid<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetGuid(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static bool MapToBoolean<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetBool(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static MarketDataImportOwnership MapToMarketDataImportOwnership<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => new(row.GetGuid(0), row.GetBool(1));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static MarketDataProjectionMutationReadModel MapToProjectionMutation<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetGuid(0), e.GetDateTime(1));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static MarketDataProjectionScopeMutationReadModel MapToProjectionScopeMutation<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetString(0), e.GetString(1), e.GetGuid(2), e.GetDateTime(3));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static VixFuturesContractIndexReadModel MapToVixFuturesContractIndex<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetInt(0), e.GetString(1));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static MarketDataProjectionStateReadModel MapToProjectionState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetString(0), e.GetGuid(1), e.GetBool(2));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static MarketDataProjectionScopeStateReadModel MapToProjectionScopeState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetString(0), e.GetString(1), e.GetGuid(2), e.GetBool(3), e.GetBool(4), e.IsCollectionEmpty(5));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static string MapToFuturesTickProjectionScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => MarketDataDbContextExtensions.GetFuturesTickScopeKey(e.GetString(0), e.GetDateOnly(1));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static string MapToFuturesEodProjectionSourceScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.GetDateOnly(0));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static string MapToFuturesEodProjectionTargetScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.GetInt(0));
    /// <summary>Checks usable PostgreSQL canonical query indexes; raw tick readiness belongs to TradeTickQuoteDb.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<MarketDataProjectionReadinessReadModel> GetQueryProjectionReadinessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await new PostgresEventTransaction(new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, ConnectionString, "System.Data.Postgres"))
            .ExecuteAsync((transaction, token) => transaction.ReadCanonicalQueryReadinessAsync(token), cancellationToken)
            .ConfigureAwait(false);
    }
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesDataId MapToFuturesDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesTickDataV2ReadModel MapToFuturesTickData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), price: e.GetDecimal(4), size: e.GetInt(5));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesTickDataId MapToFuturesTickDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), TickId: e.GetLong(2));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), optionPrice: e.GetDouble(4), bidPrice: e.GetDouble(5), askPrice: e.GetDouble(6), bidSize: e.GetInt(7), askSize: e.GetInt(8), impliedVolatility: e.GetDouble(9), underlyingPrice: e.GetDouble(10), delta: e.GetDouble(11), gamma: e.GetDouble(12), vega: e.GetDouble(13), theta: e.GetDouble(14), rho: e.GetDouble(15));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesOptionTickDataV2ReadModel MapToOptionChainQuote<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => MapToFuturesOptionTickData(e) with
        {
            Volume = e.IsNull(16) ? null : e.GetLong(16),
            OpenInterest = e.IsNull(17) ? null : e.GetLong(17),
            VolumeValueDate = e.IsNull(18) ? null : e.GetDateOnly(18),
            OpenInterestValueDate = e.IsNull(19) ? null : e.GetDateOnly(19)
        };
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickPriceData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), optionPrice: e.GetDouble(4), bidPrice: e.GetDouble(5), askPrice: e.GetDouble(6), bidSize: e.GetInt(7), askSize: e.GetInt(8), impliedVolatility: e.GetDouble(9), underlyingPrice: e.GetDouble(10), delta: e.GetDouble(11), gamma: e.GetDouble(12), vega: e.GetDouble(13), theta: e.GetDouble(14), rho: e.GetDouble(15));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesOptionTickDataId MapToFuturesOptionTickDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), TickId: e.GetLong(2));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesBarDataReadModel MapToFuturesBarData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), symbol: e.GetString(1), valueDate: e.GetDateOnly(2), barDate: e.GetDateTime(3), barRateType: e.GetEnum<BarRateType>(4), barValue: e.GetDecimal(5), upTrendTrigger: e.GetDouble(6), downTrendTrigger: e.GetDouble(7));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static long MapToFuturesBarDataCount<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetLong(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesClosingPriceReadModel MapToFuturesClosingPrice<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), closingPrice: e.GetDecimal(2), createdOn: e.GetDateTime(3), createdBy: e.GetString(4));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesEodDataV2ReadModel MapToFuturesEodData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), symbol: e.GetString(2), openPrice: e.GetDecimal(3), highPrice: e.GetDecimal(4), lowPrice: e.GetDecimal(5), closePrice: e.GetDecimal(6), volume: e.GetLong(7), dailyPercentChange: e.GetDouble(8), dailyStdDev: e.GetDouble(9), dailyStdDevAmount: e.GetDouble(10), upperBand: e.GetDouble(11), mean: e.GetDouble(12), lowerBand: e.GetDouble(13), marketDirection: e.GetEnum<MarketDirectionType>(14), marketVolatility: e.GetEnum<MarketVolatilityType>(15), priceDirection: e.GetEnum<PriceDirectionType>(16), priceVolatility: e.GetEnum<PriceVolatilityType>(17), marketDirectionIndicator: e.GetDouble(18), windowSize: e.GetInt(19), fiftyDMA: e.IsNull(20) ? 0m : e.GetDecimal(20), twoHundredDMA: e.IsNull(21) ? 0m : e.GetDecimal(21));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesIntraDayDataReadModel MapToFuturesIntraDayData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), sequenceId: e.GetLong(2), symbol: e.GetString(3), openPrice: e.GetDecimal(4), highPrice: e.GetDecimal(5), lowPrice: e.GetDecimal(6), closePrice: e.GetDecimal(7), volume: e.GetLong(8), dailyPercentChange: e.GetDouble(9), dailyStdDev: e.GetDouble(10), dailyStdDevAmount: e.GetDouble(11), upperBand: e.GetDouble(12), mean: e.GetDouble(13), lowerBand: e.GetDouble(14), marketDirection: e.GetEnum<MarketDirectionType>(15), marketVolatility: e.GetEnum<MarketVolatilityType>(16), priceDirection: e.GetEnum<PriceDirectionType>(17), priceVolatility: e.GetEnum<PriceVolatilityType>(18), marketDirectionIndicator: e.GetDouble(19), windowSize: e.GetInt(20));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesEodClosingPriceReadModel MapToFuturesEodClosingPrice<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), closingPrice: e.GetDecimal(2));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesTickHLVDataReadModel MapToFuturesTickHLVData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), HighPrice: e.GetDecimal(2), LowPrice: e.GetDecimal(3), Volume: e.GetLong(4));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesItiSignalV2ReadModel MapToFuturesItiSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3), intrinsicTime: e.GetDateTime(4), intrinsicTimeGroupId: e.GetInt(5), intrinsicTimeLength: e.GetDouble(6), intrinsicPrice: e.GetDouble(7), intrinsicTimeTrend: e.GetEnum<IntrinsicTimeTrendType>(8), intrinsicTimeMode: e.GetEnum<IntrinsicTimeModeType>(9), trendPrice: e.GetDouble(10), trendExtreme: e.GetDouble(11), trendReversal: e.GetDouble(12), trendDelta: e.GetDouble(13), targetDelta: e.GetDouble(14), lambda: e.GetDouble(15), tradingDays: e.GetInt(16), threshold: e.GetDouble(17), upTrendTrigger: e.GetDouble(18), downTrendTrigger: e.GetDouble(19), tradeState: e.GetEnum<IntrinsicTimeTradeState>(20), bandLevel: e.GetDouble(21), reversalLevel: e.GetDouble(22));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesItiSignalMDIV2ReadModel MapToFuturesItiSignalMDI<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), intrinsicTime: e.GetDateTime(2), trendType: e.GetEnum<IntrinsicTimeTrendType>(3), mdi: e.GetDouble(4));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static DateOnly MapToMaxValueDate<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDateOnly(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static int MapToMaxIntrinsicTimeGroupId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetInt(0);


    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesItiSignalV2ReadModel MapToFuturesItiTimeFrameState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3), intrinsicTime: e.GetDateTime(4), intrinsicTimeGroupId: e.GetInt(5), intrinsicTimeLength: e.GetDouble(6), intrinsicPrice: e.GetDouble(7), intrinsicTimeTrend: e.GetEnum<IntrinsicTimeTrendType>(8), intrinsicTimeMode: e.GetEnum<IntrinsicTimeModeType>(9), trendPrice: e.GetDouble(10), trendExtreme: e.GetDouble(11), trendReversal: e.GetDouble(12), trendDelta: e.GetDouble(13), targetDelta: e.GetDouble(14), lambda: e.GetDouble(15), tradingDays: e.GetInt(16), threshold: e.GetDouble(17), upTrendTrigger: e.GetDouble(18), downTrendTrigger: e.GetDouble(19), tradeState: e.GetEnum<IntrinsicTimeTradeState>(20), timeFrameStartValueDate: e.GetDateOnly(21), bandAnchorPrice: e.GetDouble(22), bandPercentage: e.GetDouble(23), bandSize: e.GetDouble(24), bandLevel: e.GetDouble(25), reversalLevel: e.GetDouble(26));


    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static double MapToRsi<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDouble(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static long MapToMaxSequenceId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetLong(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesMacdSignalReadModel MapToFuturesMacdSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
    {
        var isWarm = !e.IsNull(15) && e.GetBool(15);
        var signalEmaPeriod = e.GetInt(3);
        var slowEmaPeriod = e.GetInt(5);
        var signal = new FuturesMacdSignalReadModel(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), signalEmaPeriod: signalEmaPeriod, fastEmaPeriod: e.GetInt(4), slowEmaPeriod: slowEmaPeriod, timestamp: e.GetTimeOnly(6), futuresPrice: e.GetDecimal(7), fastEma: e.GetDouble(8), slowEma: e.GetDouble(9), macdLine: e.GetDouble(10), signalLine: e.GetDouble(11), histogram: e.GetDouble(12), macd: e.GetEnum<FuturesTrendDirectionType>(13), macdStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(14))
        {
            IsWarm = isWarm,
            ObservationCount = (e.IsNull(16), isWarm) switch { (false, _) => e.GetInt(16), (true, true) => slowEmaPeriod + signalEmaPeriod, _ => 0 }
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

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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

    /// <summary>Preserves the ADX warm-up flag in the evolved PostgreSQL observation schema.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesAdxSignalReadModel MapToPostgresFuturesAdxSignal<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => MapToFuturesAdxSignal(row) with { IsWarm = !row.IsNull(19) && row.GetBool(19) };

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesTradeSignalV2ReadModel MapToFuturesTradeSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3), timestamp: e.GetTimeOnly(4), mean: e.GetDouble(5), stdDev: e.GetDouble(6), futuresPrice: e.GetDouble(7), priceChangePercent: e.GetDouble(8), fundRiskPercent: e.GetDouble(9), rsi: e.GetDouble(10), rsiSlope: e.GetDouble(11), trendType: e.GetEnum<FuturesTrendType>(12), trendStrength: e.GetEnum<FuturesTrendStrengthType>(13), tradeSignal: e.GetEnum<TradeSignalType>(14), tdi: e.GetEnum<FuturesTrendDirectionType>(15), tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(16), mdi: e.GetDouble(17), mdiTrend: e.GetEnum<FuturesMDITrendType>(18), mdiUpTrendLimit: e.GetDouble(19), mdiDownTrendLimit: e.GetDouble(20), upTrendingTrigger: e.GetDouble(21), downTrendingTrigger: e.GetDouble(22), entryTrigger: e.GetDouble(23), exitTrigger: e.GetDouble(24), trendDelta: e.GetDouble(25), trendExtreme: e.GetDouble(26), trendReversal: e.GetDouble(27), fiftyDMA: e.GetDecimal(28), twoHundredDMA: e.GetDecimal(29), tradeExecuteState: e.GetEnum<TradeExecuteState>(30));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static RateOfReturnReadModel MapToRateOfReturn<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(symbol: e.GetString(0), valueDate: e.GetDateOnly(1), rateOfReturn: e.GetDouble(2));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static VixFuturesEodDataReadModel MapToVixFuturesEodData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), openPrice: e.GetDecimal(2), highPrice: e.GetDecimal(3), lowPrice: e.GetDecimal(4), closePrice: e.GetDecimal(5), volume: e.GetLong(6));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static long MapToMinTickId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetLong(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static decimal MapToPrice<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDecimal(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static YieldCurveRateReadModel MapToYieldCurveRate<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(valueDate: e.GetDateOnly(0), oneMonth: e.GetDouble(1), twoMonth: e.GetDouble(2), threeMonth: e.GetDouble(3), sixMonth: e.GetDouble(4), oneYear: e.GetDouble(5), twoYear: e.GetDouble(6), threeYear: e.GetDouble(7), fiveYear: e.GetDouble(8), sevenYear: e.GetDouble(9), tenYear: e.GetDouble(10), twentyYear: e.GetDouble(11), thirtyYear: e.GetDouble(12));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static MarketHolidayReadModel MapToMarketHoliday<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(currencyType: e.GetEnum<CurrencyType>(0), holidayDate: e.GetDateOnly(1), description: e.GetString(2));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesItiSignalAverageInfoDataModel MapToFuturesItiSignalAverageInfo<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), predictedDelta: e.GetDouble(2), futuresRSI: e.GetDouble(3));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static double MapToAveragePredictedDelta<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => e.GetDouble(0);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static NormalCurveDataReadModel MapToNormalCurveData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(stdDevIndex: e.GetInt(0), percent: e.GetDouble(1));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesTradeSignalId MapToFuturesTradeSignalId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), timePeriod: e.GetEnum<TimeFrameType>(2), sequenceId: e.GetLong(3));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="o">The o used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesContractV3ReadModel MapToFuturesContract(IObjectDataRecord o) => new(o.GetString(0), o.GetString(1), o.GetString(2), o.GetString(3), o.GetString(4), o.GetString(5), o.GetString(6), o.GetString(7), o.GetDateOnly(8), o.GetBool(9));
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static TradeLiveFeedReadModel MapToTradeLiveFeed<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), tradeLiveFeedState: e.GetEnum<TradeLiveFeedStateType>(2));
    /// <summary>
    /// Deletes a futures bar data record from the database.
    /// </summary>
    /// <param name = "e">The identifier of the futures bar data to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesBarDataAsync(FuturesBarDataId e)
    {
        var preparedParameters1 = new DeleteFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteFuturesBarData", MarketDataDbSql.DeleteFuturesBarData)
            .SetParameters(preparedParameters1)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Asynchronously deletes the closing price entry for the specified futures contract on the given date.
    /// </summary>
    /// <remarks>If no closing price exists for the specified contract and date, no action is taken. Ensure
    /// that the contract identifier and date correspond to an existing entry to avoid unnecessary operations.</remarks>
    /// <param name = "contractId">The unique identifier of the futures contract for which to delete the closing price.</param>
    /// <param name = "valueDate">The date of the closing price to delete. Must be a valid date.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesClosingPriceAsync(string contractId, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteFuturesClosingPrice", MarketDataDbSql.DeleteFuturesClosingPrice)
            .SetParameters(new DeleteFuturesClosingPrice(contractId, valueDate))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    /// <summary>
    /// Deletes futures EOD data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task DeleteFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
            await this.DeleteNativeFuturesEodAsync(contractId, valueDate)
            .ConfigureAwait(false);
            return;
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
            await _dbFactory.TradeTickQuoteDb.DeleteFuturesTickDataAsync(contractId, valueDate)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>
    /// Deletes a trade live feed record from the database.
    /// </summary>
    /// <param name = "orderId">The order identifier.</param>
    /// <param name = "tradeId">The trade identifier.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteTradeLiveFeed", MarketDataDbSql.DeleteTradeLiveFeed)
            .SetParameters(new DeleteTradeLiveFeed(orderId, tradeId))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    /// <summary>
    /// Deletes futures tick data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task DeleteVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var preparedParameters2 = new DeleteVixFuturesEodData(contractId,valueDate);
            await this.Use(nameof(MarketDataDbSql.DeleteVixFuturesEodData), MarketDataDbSql.DeleteVixFuturesEodData)
            .SetParameters(preparedParameters2)
            .ExecuteCommandAsync()
            .ConfigureAwait(false);
            return;
    }

    /// <summary>
    /// Deletes yield curve rate data
    /// </summary>
    /// <param name = "valueDate">The value date to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteYieldCurveRateAsync(DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var preparedParameters3 = new DeleteYieldCurveRate(valueDate);
        await (db.ExecuteQueuedCommandsAsync([db.Use($"{nameof(MarketDataDbSql)}.DeleteYieldCurveRate", MarketDataDbSql.DeleteYieldCurveRate)
            .SetParameters(preparedParameters3)
            .QueueCommand(),
            db.Use($"{nameof(MarketDataDbSql)}.DeleteYieldCurveRateByDate", MarketDataDbSql.DeleteYieldCurveRateByDate)
            .SetParameters(new DeleteYieldCurveRate(valueDate))
            .QueueCommand()], useTransaction: true))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes futures ITI signal data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="timePeriod">The time period supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task DeleteFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
            var nativeDb = _dbFactory.MarketDataDb;
        var preparedParameters5 = new DeleteFuturesItiSignal(contractId,valueDate,timePeriod.ToStringFast());
            var signal = nativeDb.Use("NativeIti.Delete", MarketDataDbSql.DeleteFuturesItiSignal)
            .SetParameters(preparedParameters5)
            .QueueCommand();
        var preparedParameters6 = new NativeItiDeleteState(contractId,timePeriod.ToStringFast(),MarketDataDbContextExtensions.GetFuturesItiCalendarBucketStart(valueDate,timePeriod),valueDate);
            var state = nativeDb.Use("NativeIti.DeleteState", MarketDataDbSql.DeleteNativeItiTimeFrameState)
            .SetParameters(preparedParameters6)
            .QueueCommand();
            await nativeDb.ExecuteQueuedCommandsAsync([signal,state],useTransaction:true)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>
    /// Deletes futures option tick data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
    {
            await _dbFactory.TradeTickQuoteDb.DeleteFuturesOptionTickDataAsync(contractId, valueDate)
            .ConfigureAwait(false);
            return;
    }
    /// <summary>
    /// Asynchronously deletes tick price data for a specified futures option contract on a given date.
    /// </summary>
    /// <remarks>This method removes all tick price data associated with the specified contract and date from
    /// the database. Ensure that the contract identifier and date are valid before calling this method.</remarks>
    /// <param name = "contractId">The unique identifier of the futures option contract whose tick price data is to be deleted. Cannot be null or
    /// empty.</param>
    /// <param name = "valueDate">The date for which the tick price data should be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteFuturesOptionTickPriceData", MarketDataDbSql.DeleteFuturesOptionTickPriceData)
            .SetParameters(new DeleteFuturesOptionTickPriceData(contractId, valueDate))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    /// <summary>
    /// Deletes a market holiday record from the database.
    /// </summary>
    /// <param name = "e">The market holiday to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidayAsync(MarketHolidayReadModel e)
    {
        var preparedParameters7 = new DeleteMarketHoliday(currencyType: e.CurrencyType.ToStringFast(), holidayDate: e.HolidayDate);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteMarketHoliday", MarketDataDbSql.DeleteMarketHoliday)
            .SetParameters(preparedParameters7)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Deletes market holiday records from the database for a given currency type within a specified date range.
    /// </summary>
    /// <param name = "currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidaysAsync(CurrencyType currencyType) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteMarketHolidays", MarketDataDbSql.DeleteMarketHolidays)
            .SetParameters(new DeleteMarketHolidays(currencyType: currencyType.ToStringFast()))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    /// <summary>Delete Rate Of Return using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task DeleteRateOfReturnAsync(string symbol, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteRateOfReturn", MarketDataDbSql.DeleteRateOfReturn)
            .SetParameters(new DeleteRateOfReturn(symbol, valueDate))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    /// <summary>
    /// Gets the futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <param name = "e">The identifier of the futures closing price to retrieve.</param>
    /// <returns>The <see cref = "FuturesClosingPriceReadModel"/>.</returns>
    public async Task<FuturesClosingPriceReadModel?> GetFuturesClosingPriceAsync(FuturesDataId e)
    {
        var preparedParameters8 = new GetFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate);
        return await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesClosingPrice", MarketDataDbSql.GetFuturesClosingPrice)
            .SetParameters(preparedParameters8)
            .ExecuteSingleAsync(MapToFuturesClosingPrice!))
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Gets yesterday's futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <returns>The <see cref = "FuturesClosingPriceReadModel"/>.</returns>
    /// <param name="e">The e used by this operation.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<FuturesClosingPriceReadModel?> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId e)
    {
        var preparedParameters9 = new GetYesterdaysFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate);
        return await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetYesterdaysFuturesClosingPrice", MarketDataDbSql.GetYesterdaysFuturesClosingPrice)
            .SetParameters(preparedParameters9)
            .ExecuteSingleAsync<FuturesClosingPriceReadModel>(MapToFuturesClosingPrice!))
                .ConfigureAwait(false);
    }
    /// <summary>
    /// get futures tick data
    /// </summary>
    /// <param name="e">The e supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId e)
    {
        return await _dbFactory.TradeTickQuoteDb.GetFuturesTickDataAsync(e)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
            return await _dbFactory.TradeTickQuoteDb.GetLastFuturesTickDataAsync(contractId, valueDate)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="tickTime">The tick time used by this operation.</param>
    /// <param name="token">The token used by this operation.</param>
    public async Task<FuturesTickDataV2ReadModel?> GetFuturesTickAtOrBeforeAsync(string contractId, DateOnly valueDate, TimeOnly tickTime, CancellationToken token = default)
    {
            return await _dbFactory.TradeTickQuoteDb.GetFuturesTickAtOrBeforeAsync(contractId, valueDate, tickTime, token)
            .ConfigureAwait(false);
    }

    /// <summary>Get Last Futures Tick Data By Tick Date using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="tickDate">The tick date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate)
    {
            var raw = await _dbFactory.TradeTickQuoteDb.GetFuturesTickAtOrBeforeAsync(contractId, DateOnly.FromDateTime(tickDate), TimeOnly.FromDateTime(tickDate))
            .ConfigureAwait(false);
            return raw?.TickTime == TimeOnly.FromDateTime(tickDate) ? raw : null;
    }

    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
    {
            return await _dbFactory.TradeTickQuoteDb.GetLastFuturesOptionTickDataAsync(contractId, valueDate)
            .ConfigureAwait(false);
    }

    /// <summary>Get Futures Option Chain Quote Data using the MarketDataDb persistence.</summary>
    /// <param name="underlyingContractId">The underlying contract id used by this operation.</param>
    /// <param name="expiryDate">The expiry date used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2139", Justification = "Log persistence failure with its request identity before preserving the original exception for the caller transaction boundary.")]
    public async Task<ICollection<FuturesOptionTickDataV2ReadModel>> GetFuturesOptionChainQuoteDataAsync(
        string underlyingContractId, DateOnly expiryDate, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
        var preparedParameters10 = new GetFuturesOptionChainQuoteData(underlyingContractId, expiryDate, valueDate);
            var rows = await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesOptionChainQuoteData", MarketDataDbSql.GetFuturesOptionChainQuoteData)
            .SetParameters(preparedParameters10)
            .ExecuteQueryAsync(MapToOptionChainQuote!, cancellationToken))
                .ConfigureAwait(false);
            if (_diagnosticLogger.IsEnabled(LogLevel.Information))
            {
                var rowCount = rows.Count;
                var elapsedMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                StorageDiagnosticLog.QuoteCompleted(_diagnosticLogger,
                    nameof(MarketDataDbContext), nameof(GetFuturesOptionChainQuoteDataAsync), underlyingContractId,
                    expiryDate, valueDate, rowCount, elapsedMilliseconds);
            }
            return rows;
        }
        catch (Exception exception)
        {
            StorageDiagnosticLog.QuoteFailed(_diagnosticLogger, exception,
                nameof(MarketDataDbContext), nameof(GetFuturesOptionChainQuoteDataAsync), underlyingContractId, expiryDate, valueDate);
            throw;
        }
    }
    /// <summary>Reads one historical quote session without mixing dates or including quotes after the cutoff.</summary>
    /// <param name="underlyingContractId">The exact canonical underlying futures contract identity.</param>
    /// <param name="expiryDate">The expiry date identifying the immutable partition.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <param name="notAfterUtc">The not after utc supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesOptionTickDataV2ReadModel>> GetLatestFuturesOptionChainQuoteDataAsync(
        string underlyingContractId, DateOnly expiryDate, DateOnly valueDate, DateTimeOffset notAfterUtc,
        CancellationToken cancellationToken = default)
        => await _dbFactory.MarketDataDb.Use(nameof(MarketDataDbSql.GetLatestFuturesOptionChainQuoteData), MarketDataDbSql.GetLatestFuturesOptionChainQuoteData)
            .SetParameters(new GetLatestFuturesOptionChainQuoteData(underlyingContractId, expiryDate, valueDate, checked((notAfterUtc - DateTimeOffset.UnixEpoch).Ticks * 100)))
            .ExecuteQueryAsync(MapToOptionChainQuote!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Reads the latest available saved volume and open interest independently, excluding future reference dates.</summary>
    /// <param name="underlyingContractId">The exact underlying futures contract.</param>
    /// <param name="expiryDate">The option expiry whose statistics are read.</param>
    /// <param name="valueDate">The latest admissible observation/reference date.</param>
    /// <param name="cancellationToken">Cancels the storage query.</param>
    /// <returns>Dated, nullable statistics suitable for seeding a live chain.</returns>
    public async Task<ImmutableArray<TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot>> ReadOptionChainStatisticsAsync(
        string underlyingContractId, DateOnly expiryDate, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var preparedParameters11 = new GetFuturesOptionChainQuoteData(underlyingContractId, expiryDate, valueDate);
        var rows = await _dbFactory.MarketDataDb.Use(nameof(MarketDataDbSql.GetOptionChainStatistics), MarketDataDbSql.GetOptionChainStatistics)
            .SetParameters(preparedParameters11)
            .ExecuteQueryAsync(MapToReadOptionChainStatisticsRow1, cancellationToken)
            .ConfigureAwait(false);
        return rows.ToImmutableArray();
    }
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
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesOptionTickPriceData", MarketDataDbSql.GetLastFuturesOptionTickPriceData)
            .SetParameters(new GetLastFuturesOptionTickPriceData(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!))
                .ConfigureAwait(false);
    /// <summary>
    /// get futures tick data id
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate)
    {
            return await _dbFactory.TradeTickQuoteDb.GetLastFuturesTickDataIdAsync(contractId, valueDate)
            .ConfigureAwait(false);
    }
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
        var preparedParameters12 = new GetFuturesBarData(contractId, symbol, valueDate, startDate, endDate);
        var futuresBarData = await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesBarData", MarketDataDbSql.GetFuturesBarData)
            .SetParameters(preparedParameters12)
            .ExecuteQueryAsync(MapToFuturesBarData!))
                .ConfigureAwait(false);
        return [.. futuresBarData.OrderBy(e => e.BarDate)];
    }

    /// <summary>
    /// gets all futures bar data.
    /// </summary>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync()
    {
        var futuresBarData = await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesBarDataAll", MarketDataDbSql.GetFuturesBarDataAll)
            .ExecuteQueryAsync(MapToFuturesBarData!))
                .ConfigureAwait(false);
        return futuresBarData;
    }

    /// <summary>
    /// gets the last futures bar data for a given contractId, symbol, and valueDate.
    /// </summary>
    /// <returns>The selected rows in the query's defined order.</returns>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    public async Task<FuturesBarDataReadModel> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var preparedParameters13 = new GetLastFuturesBarData(contractId, symbol, valueDate);
        var lastFuturesBarData = await (db.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesBarData", MarketDataDbSql.GetLastFuturesBarData)
            .SetParameters(preparedParameters13)
            .ExecuteSingleAsync(MapToFuturesBarData!))
                .ConfigureAwait(false);
        return lastFuturesBarData!;
    }

    /// <summary>
    /// Gets the count of futures bar data for a given FuturesBarDataId.
    /// </summary>
    /// <param name = "e">The futures bar data identifier.</param>
    /// <returns>The count of futures bar data.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> GetFuturesBarDataCountAsync(FuturesBarDataId e)
    {
        var preparedParameters14 = new GetFuturesBarDataCount(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate);
        return Convert.ToInt32(await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesBarDataCount", MarketDataDbSql.GetFuturesBarDataCount)
            .SetParameters(preparedParameters14)
            .ExecuteScalarAsync(MapToFuturesBarDataCount!))
                .ConfigureAwait(false));
    }
    /// <summary>
    /// Gets a collection of futures ITI signals for a given entity ID.
    /// </summary>
    /// <param name = "e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(FuturesItiSignalEntityId e)
    {
        var preparedParameters15 = new GetFuturesItiSignals(contractId: e.ContractId, valueDate: e.ValueDate, timePeriod: e.TimePeriod.ToStringFast());
        return await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesItiSignals", MarketDataDbSql.GetFuturesItiSignals)
            .SetParameters(preparedParameters15)
            .ExecuteQueryAsync(MapToFuturesItiSignal!))
                .ConfigureAwait(false);
    }
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
        var contractIds = (await (dbSec.GetFuturesContractsBySymbolAsync(symbol))
                .ConfigureAwait(false)).Select(static contract => contract.ContractId).ToHashSet(StringComparer.Ordinal);
        var db = _dbFactory.MarketDataDb;
        var valueDates = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1).Select(startDate.AddDays);
        foreach (var batch in valueDates.Chunk(ProjectionReadConcurrency))
        {
            var reads = batch.Select(valueDate => db.Use($"{nameof(MarketDataDbSql)}.GetFuturesItiSignalContractIdsByDate", MarketDataDbSql.GetFuturesItiSignalContractIdsByDate)
            .SetParameters(new GetFuturesItiSignalContractIdsByDate(valueDate))
            .ExecuteQueryAsync(MapToGetFuturesItiSignalsRow2));
            foreach (var indexedContractIds in await (Task.WhenAll(reads))
                .ConfigureAwait(false))
                contractIds.UnionWith(indexedContractIds.Where(contractId => MarketDataDbContextExtensions.IsFuturesContractForSymbol(contractId, symbol)));
        }

        return await (this.ReadFuturesItiSignalsByDateRangeAsync(contractIds, startDate, endDate))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets futures ITI signals for one concrete futures contract and date range.
    /// This avoids the securities-symbol lookup used by the cross-contract query.
    /// </summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsForContractAsync(string contractId, DateOnly startDate, DateOnly endDate) => this.ReadFuturesItiSignalsByDateRangeAsync([contractId], startDate, endDate);
    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="startDate">The start date supplied by the caller.</param>
    /// <param name="endDate">The end date supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await (dbSec.GetFuturesContractsBySymbolAsync(symbol))
                .ConfigureAwait(false)).Select(static contract => contract.ContractId).Distinct(StringComparer.Ordinal).ToArray();
        var modes = this.GetIntrinsicTimeModes().ToHashSet(StringComparer.Ordinal);
        var futuresItiSignals = await (this.ReadFuturesItiSignalsByDateRangeAsync(contractIds, startDate, endDate))
                .ConfigureAwait(false);
        return [.. futuresItiSignals.Where(signal => modes.Contains(signal.IntrinsicTimeMode.ToStringFast())).OrderBy(static signal => signal.ValueDate).ThenBy(static signal => signal.SequenceId)];
    }

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="startDate">The start date supplied by the caller.</param>
    /// <param name="endDate">The end date supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
        => GetFuturesItiSignalTrendClassDataAsync(symbol, startDate, endDate);

    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name = "e">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(FuturesBarDataReadModel e)
    {
        var preparedParameters16 = new InsertFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate, barDate: e.BarDate, barRateType: e.BarRateType.ToStringFast(), barValue: e.BarValue, upTrendTrigger: e.UpTrendTrigger, downTrendTrigger: e.DownTrendTrigger);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesBarData", MarketDataDbSql.InsertFuturesBarData)
            .SetParameters(preparedParameters16)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name = "futuresBarData">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(ICollection<FuturesBarDataReadModel> futuresBarData) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesBarData", MarketDataDbSql.InsertFuturesBarData)
            .SetParameters(futuresBarData.Select(e => new InsertFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate, barDate: e.BarDate, barRateType: e.BarRateType.ToStringFast(), barValue: e.BarValue, upTrendTrigger: e.UpTrendTrigger, downTrendTrigger: e.DownTrendTrigger)))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
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
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesBarData", MarketDataDbSql.InsertFuturesBarData)
            .SetParameters(EnumerateFuturesBarData().Select(e => new InsertFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate, barDate: e.BarDate, barRateType: e.BarRateType.ToStringFast(), barValue: e.BarValue, upTrendTrigger: e.UpTrendTrigger, downTrendTrigger: e.DownTrendTrigger)))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
        return rowCount;
        IEnumerable<FuturesBarDataReadModel> EnumerateFuturesBarData()
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
    /// <param name="e">The e supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel e)
    {
        var preparedParameters17 = new InsertFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate, closingPrice: e.ClosingPrice, createdOn: e.CreatedOn, createdBy: e.CreatedBy);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesClosingPrice", MarketDataDbSql.InsertFuturesClosingPrice)
            .SetParameters(preparedParameters17)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    ///
    /// </summary>
    /// <returns>Completion of the database operation.</returns>
    /// <param name="e">The e used by this operation.</param>
    public Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel e)
        => _dbFactory.TradeTickQuoteDb.InsertFuturesTickDataAsync(e);

    /// <summary>
    /// insert futures tick data collection
    /// </summary>
    /// <param name="tickData">The tick data supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> tickData)
    {
            await _dbFactory.TradeTickQuoteDb.InsertFuturesTickDataAsync(tickData)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>
    /// Inserts a new futures ITI signal record into the database.
    /// </summary>
    /// <param name = "e">The futures ITI signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesItiSignalAsync(FuturesItiSignalV2ReadModel e)
    {
            var nativeDb = _dbFactory.MarketDataDb;
            var signal = nativeDb.Use("NativeIti.Insert", MarketDataDbSql.InsertFuturesItiSignal)
            .SetParameters(MarketDataDbContextExtensions.CreateFuturesItiSignalParameters(e,e.SequenceId))
            .QueueCommand();
            var state = nativeDb.Use("NativeIti.State", MarketDataDbSql.UpsertFuturesItiTimeFrameState)
            .SetParameters(MarketDataDbContextExtensions.CreateFuturesItiTimeFrameStateParameters(e,e.SequenceId))
            .QueueCommand();
            await nativeDb.ExecuteQueuedCommandsAsync([signal,state],useTransaction:true)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>Get Futures Iti Time Frame State using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="calendarBucketStart">The calendar bucket start used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetFuturesItiTimeFrameStateAsync(string contractId, TimeFrameType timePeriod, DateOnly calendarBucketStart, CancellationToken cancellationToken = default) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesItiTimeFrameState", MarketDataDbSql.GetFuturesItiTimeFrameState)
            .SetParameters(new GetFuturesItiTimeFrameState(contractId, timePeriod.ToStringFast(), calendarBucketStart))
            .ExecuteSingleAsync(MapToFuturesItiTimeFrameState!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Inserts a Futures RSI Signal asynchronously into the database.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <param name="e">The e used by this operation.</param>
    public async Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel e)
    {
        var parameters = new InsertFuturesRsiSignal(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), e.PeriodLength, e.Timestamp, e.Price, e.PriceChange, e.PriceGain, e.PriceLoss, e.AveragePriceGain, e.AveragePriceLoss, e.RS, e.RSI, e.RSIAverage, e.RSISlope, e.SourceSequence, e.SourceEventTimestamp, e.Metadata?.CalculationConfigurationId, e.Metadata?.ObservationId.Value, e.Metadata?.MarketDataAsOfUtc.UtcDateTime, e.Metadata?.CalculationVersion, e.Metadata?.CalculationMethod.ToString(), e.Metadata is { } rsiMetadata ? rsiMetadata.SchemaVersion : null, e.Metadata?.IsValid, e.PreviousRsi, e.RegimeSlope, e.IsWarm);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesRsiSignal", MarketDataDbSql.InsertFuturesRsiSignal)
            .SetParameters(parameters)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts a single yield curve rate record into the database.
    /// </summary>
    /// <param name = "e">The YieldCurveRateReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e) => InsertYieldCurveRatesAsync(MarketDataImportBatch.Single(e));
    /// <summary>Persists a caller-qualified batch of YieldCurveRateReadModel rows.</summary>
    /// <param name="batch">The accepted rows, duplicate policy and source command identity.</param>
    /// <returns>Completion of the persistence operation.</returns>
    public async Task InsertYieldCurveRatesAsync(MarketDataImportBatch<YieldCurveRateReadModel> batch)
    {
            var acceptedAtUtc = DateTime.UtcNow;
            var transactions = new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings()
                .Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection,ConnectionString,"System.Data.Postgres"));
            await transactions.ExecuteOnceAsync(async (transaction, token) =>
            {
                await MarketDataDbContextExtensions.ImportYieldCurvesAsync(transaction,batch,acceptedAtUtc,token)
            .ConfigureAwait(false);
                return true;
            })
            .ConfigureAwait(false);
            return;
    }

    /// <summary>
    /// Gets the last FuturesOptionTickDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>The last <see cref = "FuturesOptionTickDataId"/>.</returns>
    public async Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate)
    {
            return await _dbFactory.TradeTickQuoteDb.GetLastFuturesOptionTickDataIdAsync(contractId, valueDate)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// Gets the FuturesOptionTickDataV2ReadModel for a given FuturesOptionTickDataId.
    /// </summary>
    /// <param name = "e">The futures option tick data identifier.</param>
    /// <returns>The <see cref = "FuturesOptionTickDataV2ReadModel"/>.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId e)
    {
        return await _dbFactory.TradeTickQuoteDb.GetFuturesOptionTickDataAsync(e)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// Asynchronously retrieves the tick price data for a specified futures option.
    /// </summary>
    /// <remarks>This method queries the market data database for the latest tick price information associated
    /// with the provided identifier. Ensure that the identifier is valid to avoid unexpected results.</remarks>
    /// <param name = "e">An identifier that specifies the futures option tick data to retrieve. This includes the contract ID, value
    /// date, and tick ID. Must represent a valid futures option tick.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the tick price data for the
    /// specified futures option, or null if no matching data is found.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickPriceDataAsync(FuturesOptionTickDataId e)
    {
        var preparedParameters18 = new GetFuturesOptionTickPriceData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId);
        return await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesOptionTickPriceData", MarketDataDbSql.GetFuturesOptionTickPriceData)
            .SetParameters(preparedParameters18)
            .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!))
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Inserts a single FuturesOptionTickData into the database.
    /// </summary>
    /// <param name = "e">The futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel e)
        => _dbFactory.TradeTickQuoteDb.InsertFuturesOptionTickDataAsync(e);

    /// <summary>
    /// Inserts a collection of FuturesOptionTickDataV2ReadModel into the database.
    /// </summary>
    /// <param name = "tickData">The collection of futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> tickData)
    {
            await _dbFactory.TradeTickQuoteDb.InsertFuturesOptionTickDataAsync(tickData)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>Enriches existing frozen quotes without replacing prices, timestamps, or newer ticks.</summary>
    /// <param name="underlyingContractId">The underlying contract id used by this operation.</param>
    /// <param name="expiryDate">The expiry date used by this operation.</param>
    /// <param name="quotes">The quotes used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task UpdateFuturesOptionChainQuoteGreeksAsync(string underlyingContractId, DateOnly expiryDate,
        ICollection<FuturesOptionTickDataV2ReadModel> quotes)
    {
        if (quotes.Count == 0) return;
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.UpdateOptionChainGreeks", MarketDataDbSql.UpdateOptionChainGreeks)
            .SetParameters(quotes.Select(x => new UpdateOptionChainGreeks(underlyingContractId, expiryDate, x)))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>Upsert Futures Option Chain Quote Data using the MarketDataDb persistence.</summary>
    /// <param name="underlyingContractId">The underlying contract id used by this operation.</param>
    /// <param name="tick">The tick used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Zero is the explicit missing-value sentinel in the persisted pricing contract; approximate comparison would classify valid nonzero values as missing.")]
    public async Task UpsertFuturesOptionChainQuoteDataAsync(
        string underlyingContractId, FuturesOptionTickDataV2ReadModel tick)
    {
        DateOnly expiry;
        try { expiry = DateOnly.FromDateTime(new FuturesOptionContractId(tick.ContractId).MaturityDate); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            var definition = await _dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(tick.ContractId)
            .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Option expiry is unavailable for {tick.ContractId}.");
            expiry = definition.ExpirationUtc is { } at
                ? DateOnly.FromDateTime(at.UtcDateTime) : definition.ContractMonth;
        }
        var preparedParameters19 = new UpsertFuturesOptionChainQuoteData(underlyingContractId, expiry, tick);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.UpsertFuturesOptionChainQuoteData", MarketDataDbSql.UpsertFuturesOptionChainQuoteData)
            .SetParameters(preparedParameters19)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>Insert Futures Option Tick Price Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task InsertFuturesOptionTickPriceDataAsync(FuturesOptionTickDataV2ReadModel e)
    {
        var tickId = e.TickId;
        var preparedParameters20 = new InsertFuturesOptionTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId, tickTime: e.TickTime, optionPrice: e.OptionPrice, bidPrice: e.BidPrice, askPrice: e.AskPrice, bidSize: e.BidSize, askSize: e.AskSize, impliedVolatility: e.ImpliedVolatility, underlyingPrice: e.UnderlyingPrice, delta: e.Delta, gamma: e.Gamma, vega: e.Vega, theta: e.Theta, rho: e.Rho);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesOptionTickPriceData", MarketDataDbSql.InsertFuturesOptionTickPriceData)
            .SetParameters(preparedParameters20)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Inserts a trade live feed record into the database.
    /// </summary>
    /// <param name="e">The e supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e)
    {
        var preparedParameters21 = new InsertTradeLiveFeed(orderId: e.OrderId, tradeId: e.TradeId, tradeLiveFeedState: e.TradeLiveFeedState.ToStringFast());
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertTradeLiveFeed", MarketDataDbSql.InsertTradeLiveFeed)
            .SetParameters(preparedParameters21)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Gets the FuturesDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesDataId.</returns>
    public async Task<FuturesDataId?> GetFuturesDataId(string contractId, DateOnly valueDate)
    {
        var preparedParameters22 = new GetFuturesDataId(contractId, valueDate);
        return await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesDataId", MarketDataDbSql.GetFuturesDataId)
            .SetParameters(preparedParameters22)
            .ExecuteSingleAsync(MapToFuturesDataId))
                .ConfigureAwait(false);
    } // Map the result to FuturesDataId
    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given FuturesDataId.
    /// </summary>
    /// <param name="e">The e supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId e)
    {
        return await _dbFactory.TradeTickQuoteDb.GetFuturesTickHLVDataAsync(e)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name="e">The e supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesTickHLVDataReadModel?> GetVixFuturesTickHLVDataAsync(VixFuturesEodDataEntityId e)
    {
        return await _dbFactory.TradeTickQuoteDb.GetFuturesTickHLVDataAsync(new FuturesDataId(e.ContractId, e.ValueDate))
            .ConfigureAwait(false);
    }
    /// <summary>
    /// Gets the FuturesEodDataV2ReadModel for a given contractId and valueDate.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    public Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => CurrentFuturesEodCache.Shared.ReadAsync(contractId, valueDate,
            () => this.ReadStoredFuturesEodDataAsync(contractId, valueDate));
    /// <summary>
    /// Asynchronously retrieves a collection of end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain all available end-of-day futures
    /// data.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a collection of  <see
    ///cref = "FuturesEodDataV2ReadModel"/> objects representing the end-of-day futures data.</returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync() => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesEodDataAll", MarketDataDbSql.GetFuturesEodDataAll)
            .ExecuteQueryAsync(MapToFuturesEodData!))
                .ConfigureAwait(false);

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
    public async Task<ICollection<FuturesIntraDayDataReadModel>> GetFuturesIntraDayDataAsync(string contractId, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesIntraDayData", MarketDataDbSql.GetFuturesIntraDayData)
            .SetParameters(new GetFuturesIntraDayData(contractId, valueDate))
            .ExecuteQueryAsync(MapToFuturesIntraDayData!))
                .ConfigureAwait(false);
    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain the latest available futures data at
    /// the end of the trading day.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see
    ///cref = "FuturesEodDataV2ReadModel"/> representing the latest end-of-day futures data, or <see langword="null"/> if
    /// no data is available.</returns>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    public async Task<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesEodData", MarketDataDbSql.GetLastFuturesEodData)
            .SetParameters(new GetLastFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesEodData!))
                .ConfigureAwait(false);
    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given contractId and date range.
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="startDate">The start date supplied by the caller.</param>
    /// <param name="endDate">The end date supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateOnly startDate, DateOnly endDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesEodDataByDateRange", MarketDataDbSql.GetFuturesEodDataByDateRange)
            .SetParameters(new GetFuturesEodDataByDateRange(contractId, startDate, endDate))
            .ExecuteQueryAsync(MapToFuturesEodData!))
                .ConfigureAwait(false);
    /// <summary>
    /// Gets the current FuturesEodDataV2ReadModel for a given FuturesDataId.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    /// <param name="valueDate">The value date used by this operation.</param>
    public async Task<FuturesEodDataV2ReadModel?> GetCurrentFuturesEodDataAsync(DateOnly valueDate)
    {
        var preparedParameters23 = new NativeValueDateParameters(valueDate);
        return await this.Use(nameof(MarketDataDbSql.GetCurrentFuturesEodData), MarketDataDbSql.GetCurrentFuturesEodData)
            .SetParameters(preparedParameters23)
            .ExecuteSingleAsync(MapToFuturesEodData!)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given date range.
    /// </summary>
    /// <param name="startDate">The start date supplied by the caller.</param>
    /// <param name="endDate">The end date supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetCurrentFuturesEodDataByDateRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        var preparedParameters24 = new NativeMarketDateRangeParameter(startDate,endDate);
            return await this.Use(nameof(MarketDataDbSql.GetCurrentFuturesEodDataByDateRange), MarketDataDbSql.GetCurrentFuturesEodDataByDateRange)
            .SetParameters(preparedParameters24)
            .ExecuteQueryAsync(MapToFuturesEodData)
            .ConfigureAwait(false);
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
        var values = (await (GetCurrentFuturesEodDataByDateRangeAsync(DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate)))
                .ConfigureAwait(false)).Where(e => string.Equals(e.Symbol, symbol, StringComparison.Ordinal)).Select(e => e.ClosePrice).ToArray();
        return values.Length == 0 ? null : new FuturesEodMovingAverageReadModel(symbol, (double)values.Average());
    }

    /// <summary>
    /// Gets a collection of FuturesEodClosingPriceReadModel for a given symbol and date range, limited by maxDays.
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
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
        var preparedParameters25 = new GetFuturesEodClosingPrices(contractId, startDate, endDate);
        var rows = _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesEodClosingPrices", MarketDataDbSql.GetFuturesEodClosingPrices)
            .SetParameters(preparedParameters25)
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
    /// Inserts a new record into the futures_eod_data_index table if it does not already exist.
    /// </summary>
    /// <param name = "e">The FuturesEodDataIndexReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesEodDataIndexAsync(FuturesEodDataIndexReadModel e)
    {
        var preparedParameters26 = new InsertFuturesEodDataIndex(valueDate: e.ValueDate, contractId: e.ContractId);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesEodDataIndex", MarketDataDbSql.InsertFuturesEodDataIndex)
            .SetParameters(preparedParameters26)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }


    /// <summary>
    /// Inserts a Futures TDI Signal asynchronously into the database.
    /// </summary>
    /// <param name = "futuresTdiSignal">The Futures TDI Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel futuresTdiSignal)
    {
        var preparedParameters27 = new InsertFuturesTdiSignal(contractId: futuresTdiSignal.ContractId, timePeriod: futuresTdiSignal.TimePeriod.ToStringFast(), configurationId: futuresTdiSignal.ConfigurationId, valueDate: futuresTdiSignal.ValueDate, timestamp: futuresTdiSignal.Timestamp, schemaVersion: futuresTdiSignal.SchemaVersion, rsiPeriod: futuresTdiSignal.RsiPeriod, priceLinePeriod: futuresTdiSignal.PriceLinePeriod, signalLinePeriod: futuresTdiSignal.SignalLinePeriod, marketBasePeriod: futuresTdiSignal.MarketBasePeriod, volatilityBandPeriod: futuresTdiSignal.VolatilityBandPeriod, volatilityBandDeviation: futuresTdiSignal.VolatilityBandDeviation, price: futuresTdiSignal.Price, rsi: futuresTdiSignal.Rsi, priceLine: futuresTdiSignal.PriceLine, signalLine: futuresTdiSignal.SignalLine, marketBaseLine: futuresTdiSignal.MarketBaseLine, upperVolatilityBand: futuresTdiSignal.UpperVolatilityBand, lowerVolatilityBand: futuresTdiSignal.LowerVolatilityBand, bandWidth: futuresTdiSignal.BandWidth, priceSignalDivergence: futuresTdiSignal.PriceSignalDivergence, crossType: futuresTdiSignal.Cross.ToString(), marketState: futuresTdiSignal.MarketState.ToString(), trendDirection: futuresTdiSignal.TDI.ToStringFast(), trendStrength: futuresTdiSignal.TDIStrength.ToStringFast(), sourceSequence: futuresTdiSignal.SourceSequence, sourceEventTimestamp: futuresTdiSignal.SourceEventTimestamp);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesTdiSignal", MarketDataDbSql.InsertFuturesTdiSignal)
            .SetParameters(preparedParameters27)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Inserts a Futures MACD Signal asynchronously into the database.
    /// </summary>
    /// <param name = "futuresMacdSignal">The Futures MACD Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertFuturesMacdSignalAsync(FuturesMacdSignalReadModel futuresMacdSignal)
    {
        var preparedParameters28 = new InsertFuturesMacdSignal(contractId: futuresMacdSignal.ContractId, valueDate: futuresMacdSignal.ValueDate, timePeriod: futuresMacdSignal.TimePeriod.ToStringFast(), signalEmaPeriod: futuresMacdSignal.SignalEmaPeriod, fastEmaPeriod: futuresMacdSignal.FastEmaPeriod, slowEmaPeriod: futuresMacdSignal.SlowEmaPeriod, timestamp: futuresMacdSignal.Timestamp, futuresPrice: futuresMacdSignal.FuturesPrice, fastEma: futuresMacdSignal.FastEma, slowEma: futuresMacdSignal.SlowEma, macdLine: futuresMacdSignal.MacdLine, signalLine: futuresMacdSignal.SignalLine, histogram: futuresMacdSignal.Histogram, macd: futuresMacdSignal.MACD.ToStringFast(), macdStrength: futuresMacdSignal.MACDStrength.ToStringFast(), configurationId: futuresMacdSignal.Metadata?.CalculationConfigurationId, observationId: futuresMacdSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresMacdSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresMacdSignal.Metadata?.SourceSequence, calculationVersion: futuresMacdSignal.Metadata?.CalculationVersion, calculationMethod: futuresMacdSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresMacdSignal.Metadata is { } macdMetadata ? macdMetadata.SchemaVersion : null, isValid: futuresMacdSignal.Metadata?.IsValid, isWarm: futuresMacdSignal.IsWarm, observationCount: futuresMacdSignal.ObservationCount);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesMacdSignal", MarketDataDbSql.InsertFuturesMacdSignal)
            .SetParameters(preparedParameters28)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Inserts a Futures ATR Signal asynchronously into the database.
    /// </summary>
    /// <param name = "futuresAtrSignal">The Futures ATR Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertFuturesAtrSignalAsync(FuturesAtrSignalReadModel futuresAtrSignal)
    {
        var preparedParameters29 = new InsertFuturesAtrSignal(contractId: futuresAtrSignal.ContractId, valueDate: futuresAtrSignal.ValueDate, timePeriod: futuresAtrSignal.TimePeriod.ToStringFast(), periodLength: futuresAtrSignal.PeriodLength, timestamp: futuresAtrSignal.Timestamp, futuresPrice: futuresAtrSignal.FuturesPrice, atrValue: futuresAtrSignal.AtrValue, trueRange: futuresAtrSignal.TrueRange, atr: futuresAtrSignal.ATR.ToStringFast(), atrStrength: futuresAtrSignal.ATRStrength.ToStringFast(), configurationId: futuresAtrSignal.Metadata?.CalculationConfigurationId, observationId: futuresAtrSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAtrSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAtrSignal.Metadata?.SourceSequence, calculationVersion: futuresAtrSignal.Metadata?.CalculationVersion, calculationMethod: futuresAtrSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAtrSignal.Metadata is { } atrMetadata ? atrMetadata.SchemaVersion : null, isValid: futuresAtrSignal.Metadata?.IsValid, previousAtrValue: futuresAtrSignal.PreviousAtrValue, atrBaseline: futuresAtrSignal.AtrBaseline, atrRatio: futuresAtrSignal.AtrRatio, isWarm: futuresAtrSignal.IsWarm);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesAtrSignal", MarketDataDbSql.InsertFuturesAtrSignal)
            .SetParameters(preparedParameters29)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Deletes futures ATR signal data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "timePeriod">The signal time frame.</param>
    /// <param name = "periodLength">The indicator period length.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task DeleteFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteFuturesAtrSignal", MarketDataDbSql.DeleteFuturesAtrSignal)
            .SetParameters(new DeleteFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    /// <summary>
    /// Inserts a Futures ADX Signal
    /// </summary>
    /// <param name = "futuresAdxSignal">The Futures ADX Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertFuturesAdxSignalAsync(FuturesAdxSignalReadModel futuresAdxSignal)
    {
        var preparedParameters30 = new InsertFuturesAdxSignal(contractId: futuresAdxSignal.ContractId, valueDate: futuresAdxSignal.ValueDate, timePeriod: futuresAdxSignal.TimePeriod.ToStringFast(), periodLength: futuresAdxSignal.PeriodLength, timestamp: futuresAdxSignal.Timestamp, futuresPrice: futuresAdxSignal.FuturesPrice, plusDI: futuresAdxSignal.PlusDI, minusDI: futuresAdxSignal.MinusDI, adxValue: futuresAdxSignal.AdxValue, adx: futuresAdxSignal.ADX.ToStringFast(), adxStrength: futuresAdxSignal.ADXStrength.ToStringFast(), configurationId: futuresAdxSignal.Metadata?.CalculationConfigurationId, observationId: futuresAdxSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAdxSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAdxSignal.Metadata?.SourceSequence, calculationVersion: futuresAdxSignal.Metadata?.CalculationVersion, calculationMethod: futuresAdxSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAdxSignal.Metadata is { } metadata ? metadata.SchemaVersion : null, isValid: futuresAdxSignal.Metadata?.IsValid, isWarm: futuresAdxSignal.IsWarm) { IncludeWarmState = true };
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesAdxSignal", MarketDataDbSql.InsertFuturesAdxSignal)
            .SetParameters(preparedParameters30)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Deletes futures ADX signal data for a given contract ID and value date.
    /// </summary>
    /// <param name = "contractId">The contract identifier.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "timePeriod">The signal time frame.</param>
    /// <param name = "periodLength">The indicator period length.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task DeleteFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteFuturesAdxSignal", MarketDataDbSql.DeleteFuturesAdxSignal)
            .SetParameters(new DeleteFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    /// <summary>
    /// Inserts a futures trade signal
    /// </summary>
    /// <param name = "FuturesTradeSignalV2ReadModel">The futures trade signal view model to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel FuturesTradeSignalV2ReadModel)
    {
        var sequenceId = FuturesTradeSignalV2ReadModel.SequenceId;
        var timePeriod = FuturesTradeSignalV2ReadModel.TimePeriod.ToStringFast();
        var db = _dbFactory.MarketDataDb;
        var preparedParameters31 = new InsertFuturesTradeSignal(contractId: FuturesTradeSignalV2ReadModel.ContractId, valueDate: FuturesTradeSignalV2ReadModel.ValueDate, timePeriod, sequenceId, timestamp: FuturesTradeSignalV2ReadModel.Timestamp, mean: FuturesTradeSignalV2ReadModel.Mean, stdDev: FuturesTradeSignalV2ReadModel.StdDev, futuresPrice: FuturesTradeSignalV2ReadModel.FuturesPrice, priceChangePercent: FuturesTradeSignalV2ReadModel.PriceChangePercent, fundRiskPercent: FuturesTradeSignalV2ReadModel.FundRiskPercent, rsi: FuturesTradeSignalV2ReadModel.RSI, rsiSlope: FuturesTradeSignalV2ReadModel.RSISlope, trendType: FuturesTradeSignalV2ReadModel.TrendType.ToStringFast(), trendStrength: FuturesTradeSignalV2ReadModel.TrendStrength.ToStringFast(), tradeSignal: FuturesTradeSignalV2ReadModel.TradeSignal.ToStringFast(), tdi: FuturesTradeSignalV2ReadModel.TDI.ToStringFast(), tdiStrength: FuturesTradeSignalV2ReadModel.TDIStrength.ToStringFast(), mdi: FuturesTradeSignalV2ReadModel.MDI, mdiTrend: FuturesTradeSignalV2ReadModel.MDITrend.ToStringFast(), mdiUpTrendLimit: FuturesTradeSignalV2ReadModel.MDIUpTrendLimit, mdiDownTrendLimit: FuturesTradeSignalV2ReadModel.MDIDownTrendLimit, upTrendingTrigger: FuturesTradeSignalV2ReadModel.UpTrendingTrigger, downTrendingTrigger: FuturesTradeSignalV2ReadModel.DownTrendingTrigger, entryTrigger: FuturesTradeSignalV2ReadModel.EntryTrigger, exitTrigger: FuturesTradeSignalV2ReadModel.ExitTrigger, trendDelta: FuturesTradeSignalV2ReadModel.TrendDelta, trendExtreme: FuturesTradeSignalV2ReadModel.TrendExtreme, trendReversal: FuturesTradeSignalV2ReadModel.TrendReversal, fiftyDma: FuturesTradeSignalV2ReadModel.FiftyDMA, twoHundredDma: FuturesTradeSignalV2ReadModel.TwoHundredDMA, tradeExecuteState: FuturesTradeSignalV2ReadModel.TradeExecuteState.ToStringFast());
        var insertSignal = db.Use($"{nameof(MarketDataDbSql)}.InsertFuturesTradeSignal", MarketDataDbSql.InsertFuturesTradeSignal)
            .SetParameters(preparedParameters31)
            .QueueCommand();
        var preparedParameters32 = new InsertFuturesTradeSignalIndex($"latest:{timePeriod}", "latest", sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod);
        var insertLatestIndex = db.Use($"{nameof(MarketDataDbSql)}.InsertFuturesTradeSignalIndex", MarketDataDbSql.InsertFuturesTradeSignalIndex)
            .SetParameters(preparedParameters32)
            .QueueCommand();
        var preparedParameters33 = new InsertFuturesTradeSignalIndex($"date:{timePeriod}:{FuturesTradeSignalV2ReadModel.ValueDate.DayNumber}", FuturesTradeSignalV2ReadModel.ContractId, sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod);
        var insertDateIndex = db.Use($"{nameof(MarketDataDbSql)}.InsertFuturesTradeSignalIndex", MarketDataDbSql.InsertFuturesTradeSignalIndex)
            .SetParameters(preparedParameters33)
            .QueueCommand();
        await (db.ExecuteQueuedCommandsAsync([insertSignal, insertLatestIndex, insertDateIndex], useTransaction: true))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts a rate of return record into the database asynchronously.
    /// </summary>
    /// <param name = "e">The rate of return data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertRateOfReturnAsync(RateOfReturnReadModel e)
    {
        var preparedParameters34 = new InsertRateOfReturn(symbol: e.Symbol, valueDate: e.ValueDate, rateOfReturn: e.RateOfReturn);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertRateOfReturn", MarketDataDbSql.InsertRateOfReturn)
            .SetParameters(preparedParameters34)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }
    /// <summary>
    /// Inserts a market holiday record into the database asynchronously.
    /// </summary>
    /// <param name = "e">The MarketHolidayReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertMarketHolidayAsync(MarketHolidayReadModel e)
    {
        var preparedParameters35 = new InsertMarketHoliday(currencyType: e.CurrencyType.ToStringFast(), holidayDate: e.HolidayDate, description: e.Description);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertMarketHoliday", MarketDataDbSql.InsertMarketHoliday)
            .SetParameters(preparedParameters35)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }


    /// <summary>Writes every admitted history row and coalesces current snapshots per contract/date.</summary>
    /// <param name="rows">The rows used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task PersistRealtimeFuturesEodBatchAsync(IReadOnlyList<BufferedFuturesEodRow> rows)
    {
        if (rows.Count == 0) return;
        await this.PersistNativeFuturesEodBatchAsync(rows)
            .ConfigureAwait(false);
    }

    /// <summary>Insert Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <param name="historySequenceId">The history identity assigned by the producer.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertFuturesEodDataAsync(FuturesEodDataV2ReadModel e, long historySequenceId)
        => PersistRealtimeFuturesEodBatchAsync([new(e, true) { HistorySequenceId = historySequenceId }]);

    /// <summary>Persists the caller-qualified batch of current futures EOD snapshots.</summary>
    /// <param name="batch">The batch with unique canonical EOD keys.</param>
    /// <returns>Completion of the persistence operation.</returns>
    public Task InsertFuturesEodDataAsync(FuturesEodBatch batch)
        => this.InsertFuturesEodBatchAsync(batch.Rows);

    /// <summary>
    /// Updates provider-supplied session prices and the two metrics that depend on
    /// the session open. The immutable intraday observation table is intentionally
    /// not appended for a statistics-only correction.
    /// </summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task UpdateFuturesEodSessionStatisticsAsync(FuturesEodDataV2ReadModel e)
    {
        var db = _dbFactory.MarketDataDb;
        var preparedParameters36 = new GetFuturesDataId(contractId: e.ContractId, valueDate: e.ValueDate);
        var existingData = await (db.Use($"{nameof(MarketDataDbSql)}.GetFuturesDataId", MarketDataDbSql.GetFuturesDataId)
            .SetParameters(preparedParameters36)
            .ExecuteSingleAsync(MapToFuturesDataId!))
                .ConfigureAwait(false);
        if (existingData is null)
            throw new InvalidOperationException($"Futures EOD row '{e.ContractId}:{e.ValueDate:yyyy-MM-dd}' does not exist.");
        var preparedParameters37 = new UpdateFuturesEodSessionStatistics(e.ContractId, e.ValueDate, e.Symbol, e.OpenPrice, e.HighPrice, e.LowPrice, e.Volume, e.DailyPercentChange, e.PriceDirection.ToStringFast());
        await db.Use(nameof(MarketDataDbSql.UpdateFuturesEodSessionStatistics), MarketDataDbSql.UpdateFuturesEodSessionStatistics)
            .SetParameters(preparedParameters37)
            .ExecuteCommandAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Upserts a VixFuturesEodDataReadModel into the database.
    /// </summary>
    /// <returns>Completion of the database operation.</returns>
    /// <summary>Persists coalesced VX snapshots without reapplying additive trade volume on retries.</summary>
    /// <param name="rows">The rows used by this operation.</param>
    public async Task PersistRealtimeVixEodBatchAsync(IReadOnlyList<VixFuturesEodDataReadModel> rows)
    {
            if (rows.Count == 0) return;
            var command = this.Use(nameof(MarketDataDbSql.InsertVixFuturesEodData), MarketDataDbSql.InsertVixFuturesEodData)
            .SetParameters(rows.Select(e => new InsertVixFuturesEodData(e.ContractId,e.ValueDate,e.OpenPrice,e.HighPrice,e.LowPrice,e.ClosePrice,e.Volume)))
            .QueueCommand();
            await ExecuteQueuedCommandsAsync([command],useTransaction: true)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>Insert Vix Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="observation">The caller-qualified tick and optional statistics.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task InsertVixFuturesEodDataAsync(VixTickObservation observation)
    {
            await this.InsertNativeVixTickAsync(observation)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI for a given entity ID.
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalMDIViewModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
    {
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.TrendOvershootExtended,
            IntrinsicTimeModeType.TrendOvershootReversal
        };
        var latest = await (Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, IntrinsicTimeTrendType.UpTrend, mode))))
                .ConfigureAwait(false);
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await (Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode))))
                .ConfigureAwait(false);
        return [.. rows.SelectMany(static values => values).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    /// <summary>Get Futures Iti Signal MDI using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.TrendOvershootExtended,
            IntrinsicTimeModeType.TrendOvershootReversal
        };
        var latest = await (Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, IntrinsicTimeTrendType.UpTrend, mode, cancellationToken))))
                .ConfigureAwait(false);
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await (Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode, cancellationToken: cancellationToken))))
                .ConfigureAwait(false);
        return [.. rows.SelectMany(static values => values).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI by trend for a given entity ID, intrinsic time trend, and intrinsic time group ID.
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
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
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.TrendOvershootExtended,
            IntrinsicTimeModeType.TrendOvershootReversal
        };
        var latest = await (Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, intrinsicTimeTrend, mode))))
                .ConfigureAwait(false);
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await (Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode))))
                .ConfigureAwait(false);
        return [.. rows.SelectMany(static values => values).Where(row => row.IntrinsicTimeTrend == intrinsicTimeTrend).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    /// <summary>Get Futures Iti Signal MDIBy Trend using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
    /// <param name="intrinsicTimeGroupId">The intrinsic time group id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId, CancellationToken cancellationToken)
    {
        _ = intrinsicTimeGroupId; // The legacy query never applied this argument.
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.TrendOvershootExtended,
            IntrinsicTimeModeType.TrendOvershootReversal
        };
        var latest = await (Task.WhenAll(modes.Select(mode => this.ReadLastFuturesItiTrendModeAsync(contractId, valueDate, intrinsicTimeTrend, mode, cancellationToken))))
                .ConfigureAwait(false);
        var maxValueDate = latest.Where(static row => row is not null).Select(static row => row!.ValueDate).DefaultIfEmpty().Max();
        if (maxValueDate == default)
            return [];
        var rows = await (Task.WhenAll(modes.Select(mode => this.ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode, cancellationToken: cancellationToken))))
                .ConfigureAwait(false);
        return [.. rows.SelectMany(static values => values).Where(row => row.IntrinsicTimeTrend == intrinsicTimeTrend).Select(MarketDataDbContextExtensions.ToFuturesItiSignalMdi)];
    }

    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <param name="timestamp">The timestamp supplied by the caller.</param>
    /// <param name="lookbackInterval">The lookback interval supplied by the caller.</param>
    /// <param name="startTime">The start time supplied by the caller.</param>
    /// <param name="endTime">The end time supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime)
    {
        var db = _dbFactory.MarketDataDb;
        var preparedParameters38 = new GetFuturesRsiSignalsForTrend(contractId, timePeriod.ToStringFast(), periodLength, valueDate, TimeOnly.FromDateTime(startTime), TimeOnly.FromDateTime(endTime));
        var rsiValues = await (db.Use($"{nameof(MarketDataDbSql)}.GetFuturesRsiSignalsForTrend", MarketDataDbSql.GetFuturesRsiSignalsForTrend)
            .SetParameters(preparedParameters38)
            .ExecuteQueryAsync(MapToRsi!))
                .ConfigureAwait(false);
        var upTrendCount = rsiValues.Count(static rsi => rsi >= 50);
        var downTrendCount = rsiValues.Count(static rsi => rsi < 50);
        var trendDirection = default(FuturesTrendType) switch
        {
            _ when upTrendCount > downTrendCount => FuturesTrendType.UpTrending,
            _ when upTrendCount < downTrendCount => FuturesTrendType.DownTrending,
            _ when upTrendCount == downTrendCount => FuturesTrendType.RangeBound,
            _ => FuturesTrendType.RangeBound
        };
        return new FuturesTrendDirectionReadModel(ContractId: contractId, ValueDate: valueDate, Timestamp: TimeOnly.FromDateTime(BusinessClock.GetLocalNow().DateTime), LookbackInterval: lookbackInterval, UpTrendCount: upTrendCount, DownTrendCount: downTrendCount, TrendDirection: trendDirection);
    }

    /// <summary>Get Futures Trend Direction From RSISignal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="timestamp">The timestamp used by this operation.</param>
    /// <param name="lookbackInterval">The lookback interval used by this operation.</param>
    /// <param name="startTime">The start time used by this operation.</param>
    /// <param name="endTime">The end time used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime, CancellationToken cancellationToken)
    {
        var db = _dbFactory.MarketDataDb;
        var preparedParameters39 = new GetFuturesRsiSignalsForTrend(contractId, timePeriod.ToStringFast(), periodLength, valueDate, TimeOnly.FromDateTime(startTime), TimeOnly.FromDateTime(endTime));
        var rsiValues = await db.Use($"{nameof(MarketDataDbSql)}.GetFuturesRsiSignalsForTrend", MarketDataDbSql.GetFuturesRsiSignalsForTrend)
            .SetParameters(preparedParameters39)
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
        return new FuturesTrendDirectionReadModel(contractId, valueDate, TimeOnly.FromDateTime(BusinessClock.GetLocalNow().DateTime), lookbackInterval, upTrendCount, downTrendCount, trendDirection);
    }

    /// <summary>
    /// return last futures intrinsic time indicator signal
    /// </summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate)
        => await this.ReadLastFuturesItiCategoryAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged, IntrinsicTimeModeType.TrendOvershootReversal)
            .ConfigureAwait(false);
    /// <summary>Get Last Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken)
        => await this.ReadLastFuturesItiCategoryAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged, IntrinsicTimeModeType.TrendOvershootReversal, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    /// <summary>Get Last Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesItiSignalByTimePeriod", MarketDataDbSql.GetLastFuturesItiSignalByTimePeriod)
            .SetParameters(new GetLastFuturesItiSignalByTimePeriod(contractId, valueDate, timePeriod.ToString()))
            .ExecuteSingleAsync(MapToFuturesItiSignal!))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesItiSignalByTimePeriod", MarketDataDbSql.GetLastFuturesItiSignalByTimePeriod)
            .SetParameters(new GetLastFuturesItiSignalByTimePeriod(contractId, valueDate, timePeriod.ToString()))
            .ExecuteSingleAsync(MapToFuturesItiSignal!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// return last futures intrinsic time indicator signal from trend direction change
    /// </summary>\
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate) => (await (this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged))
                .ConfigureAwait(false)).FirstOrDefault();
    /// <summary>Get Last Futures Iti Signal Trend Direction Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();
    /// <summary>
    /// return last futures intrinsic time indicator signal from trend extreme change
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate)
    {
        var direction = (await (this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged))
                .ConfigureAwait(false)).FirstOrDefault();
        return await this.ReadLastFuturesItiCategoryAsync(contractId, valueDate, IntrinsicTimeModeType.TrendExtremeChanged, IntrinsicTimeModeType.TrendOvershootExtended, direction?.SequenceId ?? 0)
            .ConfigureAwait(false);
    }

    /// <summary>Get Last Futures Iti Signal Trend Extreme Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var direction = (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();
        return await this.ReadLastFuturesItiCategoryAsync(contractId, valueDate, IntrinsicTimeModeType.TrendExtremeChanged, IntrinsicTimeModeType.TrendOvershootExtended, direction?.SequenceId ?? 0, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend reversal change
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate)
    {
        var direction = (await (this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged))
                .ConfigureAwait(false)).FirstOrDefault();
        return await this.ReadLastFuturesItiCategoryAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged, IntrinsicTimeModeType.TrendOvershootReversal, direction?.SequenceId ?? 0)
            .ConfigureAwait(false);
    }

    /// <summary>Get Last Futures Iti Signal Trend Reversal Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var direction = (await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();
        return await this.ReadLastFuturesItiCategoryAsync(contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged, IntrinsicTimeModeType.TrendOvershootReversal, direction?.SequenceId ?? 0, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the last Futures RSI signal by value date.
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name = "timePeriod">The time period.</param>
    /// <param name = "periodLength">The period length.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesRsiSignal", MarketDataDbSql.GetLastFuturesRsiSignal)
            .SetParameters(new GetLastFuturesRsiSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesRsiSignal))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Rsi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesRsiSignal", MarketDataDbSql.GetLastFuturesRsiSignal)
            .SetParameters(new GetLastFuturesRsiSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesRsiSignal, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures RSI signal by time period and period length.
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "timePeriod">The time period.</param>
    /// <param name = "periodLength">The period length.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesRsiDailySignal", MarketDataDbSql.GetLastFuturesRsiDailySignal)
            .SetParameters(new GetLastFuturesRsiDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesRsiSignal))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Rsi Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesRsiDailySignal", MarketDataDbSql.GetLastFuturesRsiDailySignal)
            .SetParameters(new GetLastFuturesRsiDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesRsiSignal, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures TDI signal for a given entity ID.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesTdiSignalReadModel"/>.</returns>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate) => await (GetLastFuturesTdiSignalAsync(contractId, valueDate, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Tdi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, string configurationId) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTdiSignal", MarketDataDbSql.GetLastFuturesTdiSignal)
            .SetParameters(new GetLastFuturesTdiSignal(contractId, timePeriod.ToStringFast(), configurationId, valueDate))
            .ExecuteSingleAsync(MapToFuturesTdiSignal!))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Tdi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => await (GetLastFuturesTdiSignalAsync(contractId, valueDate, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId, cancellationToken))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Tdi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, string configurationId, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTdiSignal", MarketDataDbSql.GetLastFuturesTdiSignal)
            .SetParameters(new GetLastFuturesTdiSignal(contractId, timePeriod.ToStringFast(), configurationId, valueDate))
            .ExecuteSingleAsync(MapToFuturesTdiSignal!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures MACD signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <param name="timePeriod">The time period supplied by the caller.</param>
    /// <param name="periodLength">The period length supplied by the caller.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesMacdSignalReadModel"/>.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await (GetLastFuturesMacdSignalAsync(contractId, valueDate, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesMacdSignal", MarketDataDbSql.GetLastFuturesMacdSignal)
            .SetParameters(new GetLastFuturesMacdSignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await (GetLastFuturesMacdSignalAsync(contractId, valueDate, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod, cancellationToken))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesMacdSignal", MarketDataDbSql.GetLastFuturesMacdSignal)
            .SetParameters(new GetLastFuturesMacdSignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    ///
    /// </summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="timePeriod">The time period supplied by the caller.</param>
    /// <param name="periodLength">The period length supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await (GetLastFuturesMacdDailySignalAsync(contractId, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Macd Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesMacdDailySignal", MarketDataDbSql.GetLastFuturesMacdDailySignal)
            .SetParameters(new GetLastFuturesMacdDailySignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Macd Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await (GetLastFuturesMacdDailySignalAsync(contractId, timePeriod, periodLength, FuturesMacdConfiguration.ConventionalFastEmaPeriod, FuturesMacdConfiguration.ConventionalSlowEmaPeriod, cancellationToken))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Macd Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesMacdDailySignal", MarketDataDbSql.GetLastFuturesMacdDailySignal)
            .SetParameters(new GetLastFuturesMacdDailySignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures ATR signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesAtrSignalReadModel"/>.</returns>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesAtrSignal", MarketDataDbSql.GetLastFuturesAtrSignal)
            .SetParameters(new GetLastFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Atr Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesAtrSignal", MarketDataDbSql.GetLastFuturesAtrSignal)
            .SetParameters(new GetLastFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>Get Last Futures Atr Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesDailyAtrSignal", MarketDataDbSql.GetLastFuturesDailyAtrSignal)
            .SetParameters(new GetLastFuturesAtrDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Atr Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesDailyAtrSignal", MarketDataDbSql.GetLastFuturesDailyAtrSignal)
            .SetParameters(new GetLastFuturesAtrDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures ADX signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesAdxSignalReadModel"/>.</returns>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesAdxSignal", MarketDataDbSql.GetLastFuturesAdxSignal)
            .SetParameters(new GetLastFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(row => MapToPostgresFuturesAdxSignal(row)))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Adx Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesAdxSignal", MarketDataDbSql.GetLastFuturesAdxSignal)
            .SetParameters(new GetLastFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(row => MapToPostgresFuturesAdxSignal(row), cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last Futures ADX daily signal
    /// </summary>
    /// <param name = "contractId">The contract ID.</param>
    /// <param name = "timePeriod">The value date.</param>
    /// <param name="periodLength">The period length supplied by the caller.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesAdxSignalReadModel"/>.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesAdxDailySignal", MarketDataDbSql.GetLastFuturesAdxDailySignal)
            .SetParameters(new GetLastFuturesAdxDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(row => MapToPostgresFuturesAdxSignal(row)))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Adx Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesAdxDailySignal", MarketDataDbSql.GetLastFuturesAdxDailySignal)
            .SetParameters(new GetLastFuturesAdxDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(row => MapToPostgresFuturesAdxSignal(row), cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last futures trade signal
    /// </summary>
    /// <param name = "contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name = "valueDate"> The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "FuturesTradeSignalV2ReadModel"/>.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTradeSignalById", MarketDataDbSql.GetLastFuturesTradeSignalById)
            .SetParameters(new GetLastFuturesTradeSignalById(contractId, valueDate, TimeFrameType.FifteenSeconds.ToStringFast()))
            .ExecuteSingleAsync(MapToFuturesTradeSignal!))
                .ConfigureAwait(false);
    /// <summary>Get Last Futures Trade Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTradeSignalById", MarketDataDbSql.GetLastFuturesTradeSignalById)
            .SetParameters(new GetLastFuturesTradeSignalById(contractId, valueDate, TimeFrameType.FifteenSeconds.ToStringFast()))
            .ExecuteSingleAsync(MapToFuturesTradeSignal!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// gets the last futures trade signal asynchronously.
    /// </summary>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync()
    {
        var preparedParameters40 = new GetLastFuturesTradeSignal($"latest:{TimeFrameType.FifteenSeconds.ToStringFast()}");
        var id = await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTradeSignal", MarketDataDbSql.GetLastFuturesTradeSignal)
            .SetParameters(preparedParameters40)
            .ExecuteSingleAsync(MapToFuturesTradeSignalId))
                .ConfigureAwait(false);
        return id is null ? null : await (GetLastFuturesTradeSignalAsync(id.ContractId, id.ValueDate))
                .ConfigureAwait(false);
    }

    /// <summary>Get Last Futures Trade Signal using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(CancellationToken cancellationToken)
    {
        var preparedParameters41 = new GetLastFuturesTradeSignal($"latest:{TimeFrameType.FifteenSeconds.ToStringFast()}");
        var id = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTradeSignal", MarketDataDbSql.GetLastFuturesTradeSignal)
            .SetParameters(preparedParameters41)
            .ExecuteSingleAsync(MapToFuturesTradeSignalId, cancellationToken)
            .ConfigureAwait(false);
        return id is null ? null : await GetLastFuturesTradeSignalAsync(id.ContractId, id.ValueDate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// gets all futures trade signals asynchronously.
    /// </summary>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalsAsync()
    {
        ICollection<FuturesTradeSignalV2ReadModel> resultSet = [];
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesTradeSignalAll", MarketDataDbSql.GetFuturesTradeSignalAll)
            .ExecuteMapReduceAsync(MapToFuturesTradeSignal, reducer => resultSet = [..reducer]))
                .ConfigureAwait(false);
        return resultSet;
    }

    /// <summary>
    /// Gets the last futures trade signal for a given symbol and value date asynchronously.
    /// </summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        List<string> contractIds = [.. (await (dbSec.GetFuturesContractsBySymbolAsync(symbol))
                .ConfigureAwait(false)).Select(e => e.ContractId)];
        var preparedParameters42 = new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate);
        return await (db.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTradeSignalBySymbol", MarketDataDbSql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(preparedParameters42)
            .ExecuteSingleAsync(MapToFuturesTradeSignal))
                .ConfigureAwait(false);
    }

    /// <summary>Get Last Futures Trade Signal By Symbol using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate, CancellationToken cancellationToken)
    {
        var securitiesDb = (ISecuritiesDbReadContext)_dbFactory.SecuritiesDb;
        var contracts = await securitiesDb.GetFuturesContractsBySymbolAsync(symbol, cancellationToken)
            .ConfigureAwait(false);
        List<string> contractIds = [.. contracts.Select(static contract => contract.ContractId)];
        var preparedParameters43 = new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate);
        return await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastFuturesTradeSignalBySymbol", MarketDataDbSql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(preparedParameters43)
            .ExecuteSingleAsync(MapToFuturesTradeSignal, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the last rate of return for a given symbol asynchronously.
    /// </summary>
    /// <param name = "symbol">The symbol to get the rate of return for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "RateOfReturnReadModel"/>.</returns>
    public async Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastRateOfReturn", MarketDataDbSql.GetLastRateOfReturn)
            .SetParameters(new GetLastRateOfReturn(symbol))
            .ExecuteSingleAsync(MapToRateOfReturn))
                .ConfigureAwait(false);
    /// <summary>Get Last Rate Of Return using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastRateOfReturn", MarketDataDbSql.GetLastRateOfReturn)
            .SetParameters(new GetLastRateOfReturn(symbol))
            .ExecuteSingleAsync(MapToRateOfReturn, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the last VIX futures EOD data for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "VixFuturesEodDataReadModel"/>.</returns>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    public async Task<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastVixFuturesEodData", MarketDataDbSql.GetLastVixFuturesEodData)
            .SetParameters(new GetLastVixFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToVixFuturesEodData))
                .ConfigureAwait(false);
    /// <summary>
    /// Gets the VIX futures EOD data for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name = "contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => CurrentVixEodCache.Shared.ReadAsync(contractId, valueDate, () => this.ReadStoredVixFuturesEodDataAsync(contractId, valueDate));
    /// <summary>
    /// Gets the VIX futures EOD data for a given by value date asynchronously.
    /// </summary>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
    {
        var preparedParameters44 = new NativeVixDateParameter(valueDate);
        return await this.Use(nameof(MarketDataDbSql.GetVixFuturesEodDataByValueDate), MarketDataDbSql.GetVixFuturesEodDataByValueDate)
            .SetParameters(preparedParameters44)
            .ExecuteQueryAsync(MapToVixFuturesEodData)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the last updated yield curve rate from the database.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "YieldCurveRateReadModel"/>.</returns>
    public async Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync() => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastYieldCurveRate", MarketDataDbSql.GetLastYieldCurveRate)
            .ExecuteSingleAsync(MapToYieldCurveRate!))
                .ConfigureAwait(false);
    /// <summary>Get Last Yield Curve Rate using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync(CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLastYieldCurveRate", MarketDataDbSql.GetLastYieldCurveRate)
            .ExecuteSingleAsync(MapToYieldCurveRate!, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Gets the yield curve rate for a given value date.
    /// </summary>
    /// <param name = "valueDate">The value date to retrieve the yield curve rate for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref = "YieldCurveRateReadModel"/> if found; otherwise, null.</returns>
    public async Task<YieldCurveRateReadModel?> GetYieldCurveRateAsync(DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetYieldCurveRate", MarketDataDbSql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate))
                .ConfigureAwait(false);
    /// <summary>Reads the caller-qualified range of Treasury yield curves.</summary>
    /// <param name="range">The inclusive range supplied by the query owner.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected curves.</returns>
    public Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(
        YieldCurveReadRange range, CancellationToken cancellationToken = default)
        => this.GetYieldCurveRatesCoreAsync(range, cancellationToken);
    /// <summary>
    /// Gets a collection of integer values representing the years for yield curve rates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the collection of integer years.</returns>
    public async Task<ICollection<int>> GetYieldCurveRateYearsAsync() => await (GetYieldCurveRateYearsAsync(CancellationToken.None))
                .ConfigureAwait(false);
    /// <summary>Get Yield Curve Rate Years using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<int>> GetYieldCurveRateYearsAsync(CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetYieldCurveRateYears", MarketDataDbSql.GetYieldCurveRateYears)
            .SetParameters(new GetYieldCurveRateYears(YieldCurveLookupId))
            .ExecuteQueryAsync(MapToYearMonth, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// Retrieves market holidays for a given currency type.
    /// </summary>
    /// <param name = "currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetMarketHolidays", MarketDataDbSql.GetMarketHolidays)
            .SetParameters(new GetMarketHolidays(currencyType: currencyType.ToStringFast()))
            .ExecuteQueryAsync(MapToMarketHoliday))
                .ConfigureAwait(false);
    /// <summary>Get Market Holidays using the MarketDataDb persistence.</summary>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetMarketHolidays", MarketDataDbSql.GetMarketHolidays)
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
    public async Task<TradeLiveFeedReadModel?> GetTradeLiveFeedAsync(int orderId, int tradeId) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetTradeLiveFeed", MarketDataDbSql.GetTradeLiveFeed)
            .SetParameters(new GetTradeLiveFeed(orderId, tradeId))
            .ExecuteSingleAsync(MapToTradeLiveFeed!))
                .ConfigureAwait(false);
    /// <summary>
    /// Retrieves market holidays for a given currency type within a specified date range.
    /// </summary>
    /// <param name = "currencyType">The currency type.</param>
    /// <param name = "startDate">The start date of the range.</param>
    /// <param name = "endDate">The end date of the range.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysByDateRangeAsync(CurrencyType currencyType, DateOnly startDate, DateOnly endDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetMarketHolidaysByDateRange", MarketDataDbSql.GetMarketHolidaysByDateRange)
            .SetParameters(new GetMarketHolidaysByDateRange(currencyType: currencyType.ToStringFast(), startDate, endDate))
            .ExecuteQueryAsync(MapToMarketHoliday))
                .ConfigureAwait(false);
    /// <summary>
    /// return number of trading days...
    /// </summary>
    /// <param name="startDate">The start date supplied by the caller.</param>
    /// <param name="endDate">The end date supplied by the caller.</param>
    /// <param name="marketType">The market type supplied by the caller.</param>
    /// <param name="currencyType">The currency type supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1006", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD)
    {
        return await GetTradingDayCountAsync(startDate, endDate, marketType, currencyType)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// return all normal curve data
    /// </summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<NormalCurveDataReadModel>> GetNormalCurveDataAsync() => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetNormalCurveData", MarketDataDbSql.GetNormalCurveData)
            .ExecuteQueryAsync(MapToNormalCurveData!))
                .ConfigureAwait(false);
    /// <summary>
    /// return futures trade signal id by value date
    /// </summary>
    /// <param name="valueDate">The value date supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesTradeSignalIdByValueDate", MarketDataDbSql.GetFuturesTradeSignalIdByValueDate)
            .SetParameters(new GetFuturesTradeSignalIdByValueDate($"date:{TimeFrameType.FifteenSeconds.ToStringFast()}:{valueDate.DayNumber}"))
            .ExecuteQueryAsync(MapToFuturesTradeSignalId))
                .ConfigureAwait(false);
    /// <summary>Get Futures Trade Signal Id By Value Date using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate, CancellationToken cancellationToken) => await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesTradeSignalIdByValueDate", MarketDataDbSql.GetFuturesTradeSignalIdByValueDate)
            .SetParameters(new GetFuturesTradeSignalIdByValueDate($"date:{TimeFrameType.FifteenSeconds.ToStringFast()}:{valueDate.DayNumber}"))
            .ExecuteQueryAsync(MapToFuturesTradeSignalId, cancellationToken)
            .ConfigureAwait(false);
    /// <summary>
    /// return normal curve table
    /// </summary>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<NormalCurveTableReadModel> GetNormalCurveTableAsync()
    {
        return new NormalCurveTableReadModel([.. await GetNormalCurveDataAsync()
            .ConfigureAwait(false)]);
    }

    /// <summary>
    /// return trading dates...
    /// </summary>
    /// <param name="startDate">The start date supplied by the caller.</param>
    /// <param name="endDate">The end date supplied by the caller.</param>
    /// <param name="marketType">The market type supplied by the caller.</param>
    /// <param name="currencyType">The currency type supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1006", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<DateOnly[]> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD)

    {
    var holidays = await GetMarketHolidaysAsync(currencyType, CancellationToken.None)
            .ConfigureAwait(false);
    return TomasAI.IFM.Domain.MarketData.Shared.Calendar.TradingCalendar.Dates(startDate, endDate, holidays, CancellationToken.None);
}
    /// <summary>Get Trading Dates using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<DateOnly[]> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken)

    {
    var holidays = await GetMarketHolidaysAsync(currencyType, cancellationToken)
            .ConfigureAwait(false);
    return TomasAI.IFM.Domain.MarketData.Shared.Calendar.TradingCalendar.Dates(startDate, endDate, holidays, cancellationToken);
}
    /// <summary>Get Trading Day Count using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1006", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> GetTradingDayCountAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD)

    {
    var holidays = await GetMarketHolidaysAsync(currencyType, CancellationToken.None)
            .ConfigureAwait(false);
    return TomasAI.IFM.Domain.MarketData.Shared.Calendar.TradingCalendar.Count(startDate, endDate, holidays, CancellationToken.None);
}
    /// <summary>Get Trading Day Count using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<int> GetTradingDayCountAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken)

    {
    var holidays = await GetMarketHolidaysAsync(currencyType, cancellationToken)
            .ConfigureAwait(false);
    return TomasAI.IFM.Domain.MarketData.Shared.Calendar.TradingCalendar.Count(startDate, endDate, holidays, cancellationToken);
}
    /// <summary>
    /// Checks if yield curve rate data exists for a given value date.
    /// </summary>
    /// <param name = "valueDate">The value date to check.</param>
    /// <returns>A task representing the asynchronous operation, containing a boolean indicating whether the data exists.</returns>
    public async Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate) => (await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetYieldCurveRate", MarketDataDbSql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate!))
                .ConfigureAwait(false)) is not null;
    /// <summary>Get Yield Curve Rate Exists using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate, CancellationToken cancellationToken) => (await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetYieldCurveRate", MarketDataDbSql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate!, cancellationToken)
            .ConfigureAwait(false)) is not null;

    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref = "FuturesItiSignalV2ReadModel"/>.</returns>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate) => await (this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged))
                .ConfigureAwait(false);
    /// <summary>Get Futures Iti Trend Direction Changed Signals using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken) => await this.ReadFuturesItiDayModeAsync(contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<IReadOnlyList<MarketDataProjectionInventoryRow>> RefreshCanonicalQueryIndexRowsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token) =>
        MarketDataDbContextExtensions.RefreshCanonicalQueryIndexRowsAsync(transaction, token);

    /// <inheritdoc/>
    public Task<MarketDataProjectionReadinessReadModel> ReadCanonicalQueryReadinessAsync(IEnlistedPostgresTransaction transaction, CancellationToken token) =>
        MarketDataDbContextExtensions.ReadCanonicalQueryReadinessAsync(transaction, token);

    /// <inheritdoc/>
    public Task<YieldCurveLookupInventory> RefreshYieldCurveLookupRowsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token) =>
        MarketDataDbContextExtensions.RefreshYieldCurveLookupRowsAsync(transaction, token);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ReadLockedTradeSignalPayloadsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token) =>
        MarketDataDbContextExtensions.ReadLockedTradeSignalPayloadsAsync(transaction, token);

    /// <inheritdoc/>
    public Task StoreTradeSignalQuarantineRowAsync(IEnlistedPostgresTransaction transaction, FuturesTradeSignalQuarantineRow row, CancellationToken token) =>
        MarketDataDbContextExtensions.StoreTradeSignalQuarantineRowAsync(transaction, row, token);

    /// <inheritdoc/>
    public Task ReplaceTradeSignalLookupRowsAsync(IEnlistedPostgresTransaction transaction, IReadOnlyList<FuturesTradeSignalLookupRow> rows, CancellationToken token) =>
        MarketDataDbContextExtensions.ReplaceTradeSignalLookupRowsAsync(transaction, rows, token);

    /// <summary>Writes the accepted outcome without provider calls on the source-event transaction.</summary>
    /// <param name="transaction">The transaction used by this operation.</param>
    /// <param name="outcome">The outcome used by this operation.</param>
    /// <param name="logCommandId">The log command id used by this operation.</param>
    /// <param name="payloadSha256">The payload sha256 used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task ProjectDownloadLogAsync(IEnlistedPostgresTransaction transaction, MarketDataDownloadOutcome outcome,
        Guid logCommandId, string payloadSha256, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var parameters = new DownloadLogParameters(outcome.Dataset.ToString(), outcome.Provider, outcome.Scope, outcome.ValueDate,
            outcome.RequestedAtUtc, outcome.ImportCommandId, logCommandId, outcome.SourceTerminalEventId, outcome.SchemaVersion,
            outcome.Status.ToString(), outcome.StartedAtUtc, outcome.FinishedAtUtc, outcome.ElapsedMilliseconds,
            outcome.DownloadedRecordCount, outcome.PersistedRecordCount, outcome.ErrorCode, outcome.ErrorMessage,
            payloadSha256, DateTime.UtcNow);
        await transaction.ExecuteAsync(MarketDataDbSql.DownloadLogInsert,(object?[])parameters.Bind(),cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Insert Market Data Download Log using the MarketDataDb persistence.</summary>
    /// <param name="outcome">The outcome used by this operation.</param>
    /// <param name="logCommandId">The log command id used by this operation.</param>
    /// <param name="payloadSha256">The payload sha256 used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task InsertMarketDataDownloadLogAsync(MarketDataDownloadOutcome outcome, Guid logCommandId, string payloadSha256, CancellationToken cancellationToken = default)
    {
        var preparedParameters45 = new DownloadLogParameters(outcome.Dataset.ToString(), outcome.Provider, outcome.Scope, outcome.ValueDate, outcome.RequestedAtUtc, outcome.ImportCommandId, logCommandId, outcome.SourceTerminalEventId, outcome.SchemaVersion, outcome.Status.ToString(), outcome.StartedAtUtc, outcome.FinishedAtUtc, outcome.ElapsedMilliseconds, outcome.DownloadedRecordCount, outcome.PersistedRecordCount, outcome.ErrorCode, outcome.ErrorMessage, payloadSha256, DateTime.UtcNow);
        await _dbFactory.MarketDataDb.Use("DownloadLog.Insert", MarketDataDbSql.DownloadLogInsert)
            .SetParameters(preparedParameters45)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Market Data Download Log using the MarketDataDb persistence.</summary>
    /// <param name="partition">The partition used by this operation.</param>
    /// <param name="attempt">The attempt used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<MarketDataDownloadLogResult> GetMarketDataDownloadLogAsync(MarketDataDownloadPartition partition, MarketDataDownloadCursor attempt, CancellationToken cancellationToken = default)
    {
        var preparedParameters46 = new DownloadLogReadParameters(partition.Dataset.ToString(), partition.Provider, partition.Scope, partition.ValueDate, attempt.RequestedAtUtc, attempt.ImportCommandId, 1, true);
        var rows = await _dbFactory.MarketDataDb.Use("DownloadLog.Exact", MarketDataDbSql.DownloadLogExact)
            .SetParameters(preparedParameters46)
            .ExecuteQueryAsync(MapToDownloadLog, cancellationToken)
            .ConfigureAwait(false);
        return new(rows.FirstOrDefault());
    }

    /// <summary>Get Market Data Download History using the MarketDataDb persistence.</summary>
    /// <param name="partition">The partition used by this operation.</param>
    /// <param name="pageSize">The page size used by this operation.</param>
    /// <param name="cursor">The cursor used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<MarketDataDownloadHistoryResult> GetMarketDataDownloadHistoryAsync(MarketDataDownloadPartition partition, int pageSize = 100, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        var sql = cursor is null ? MarketDataDbSql.DownloadLogHistory : MarketDataDbSql.DownloadLogHistoryAfter;
        var preparedParameters47 = new DownloadLogReadParameters(partition.Dataset.ToString(), partition.Provider, partition.Scope, partition.ValueDate, cursor?.RequestedAtUtc ?? DateTime.UnixEpoch, cursor?.ImportCommandId ?? Guid.Empty, pageSize + 1);
        var rows = await _dbFactory.MarketDataDb.Use(cursor is null ? "DownloadLog.History" : "DownloadLog.HistoryAfter", sql)
            .SetParameters(preparedParameters47)
            .ExecuteQueryAsync(MapToDownloadLog, cancellationToken)
            .ConfigureAwait(false);
        var items = rows.Take(pageSize).ToArray();
        var last = items.LastOrDefault()?.Outcome;
        return new(items, rows.Count > pageSize && last is not null ? new(last.RequestedAtUtc, last.ImportCommandId) : null);
    }

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
        if (result.LogCommandId != MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId) || !string.Equals(result.PayloadSha256,outcome.ComputeHash(),StringComparison.Ordinal))
            throw new InvalidOperationException("DownloadLog read model failed integrity validation.");
        return result;
    }

    internal const int EconomicCalendarMaximumRows = 10_000;
    internal const int EconomicCalendarMaximumRowsPerMonth = EconomicCalendarQueryLimits.MaximumRowsPerPartition;
    internal const int EconomicCalendarMaximumConcurrentQueries = 4;
    internal const int EconomicCalendarLookupId = 1;
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static EconomicCalendarCountryCodeReadModel MapToEconomicCalendarCountryCode(IObjectDataRecord row) => new(row.GetString(0));
    /// <summary>Get Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id) => GetEconomicCalendarAsync(id, CancellationToken.None);
    /// <summary>Get Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id, CancellationToken cancellationToken)
    {
        var eventDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(id.EventDate);
        var preparedParameters48 = new GetEconomicCalendarV2ById(id.CountryCode, MarketDataDbContextExtensions.EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName);
        return await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetEconomicCalendarV2ById", MarketDataDbSql.GetEconomicCalendarV2ById)
            .SetParameters(preparedParameters48)
            .ExecuteSingleAsync(MapToEconomicCalendar!, cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>Get Economic Calendars using the MarketDataDb persistence.</summary>
    /// <param name="eventDate">The event date used by this operation.</param>
    /// <param name="countryCode">The country code used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode) => GetEconomicCalendarsAsync(eventDate, countryCode, CancellationToken.None);
    /// <summary>Get Economic Calendars using the MarketDataDb persistence.</summary>
    /// <param name="eventDate">The event date used by this operation.</param>
    /// <param name="countryCode">The country code used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode, CancellationToken cancellationToken)
    {
        var startDate = eventDate.Date;
        var endDate = startDate == DateTime.MaxValue.Date ? DateTime.MaxValue : startDate.AddDays(1).AddTicks(-1);
        return GetEconomicCalendarsAsync(new EconomicCalendarDateRange(startDate, endDate, countryCode), cancellationToken);
    }

    /// <summary>Reads an already-qualified single-country economic calendar range.</summary>
    /// <param name="range">The normalized and bounded range supplied by the query owner.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The matching calendar events.</returns>
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(
        EconomicCalendarDateRange range, CancellationToken cancellationToken = default)
    {
        if (range.IsEmpty)
            return [];
        var request = range.ToPageRequest();
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

    /// <summary>Get Economic Calendar Page using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<EconomicCalendarPageReadModel> GetEconomicCalendarPageAsync(EconomicCalendarPageRequest request, CancellationToken cancellationToken = default)
    {
        var countries = request.CountryCodes.Select(static code => code.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var partitions = MarketDataDbContextExtensions.EconomicCalendarMonthBucketsDescending(request.StartDateUtc, request.EndDateUtc).SelectMany(month => countries.Select(country => new CalendarPartition(country, month))).ToArray();
        var fingerprint = MarketDataDbContextExtensions.GetPageRequestFingerprint(request, countries);
        var cursor = MarketDataDbContextExtensions.DecodePageToken(request.ContinuationToken, fingerprint, partitions.Length);
        var rows = new List<EconomicCalendarReadModel>(request.PageSize);
        for (var partitionIndex = cursor.PartitionIndex; partitionIndex < partitions.Length; partitionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partition = partitions[partitionIndex];
        var preparedParameters49 = new GetEconomicCalendars(partition.CountryCode, partition.MonthBucket, request.StartDateUtc, request.EndDateUtc);
            var partitionRows = await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetEconomicCalendars", MarketDataDbSql.GetEconomicCalendars)
            .SetParameters(preparedParameters49)
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

    /// <summary>Get Economic Calendar All using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1133", Justification = "Retained forwarding compatibility entry point; new callers use explicit bounded page queries.")]
    [Obsolete("Use GetEconomicCalendarPageAsync with explicit UTC bounds and country codes.")]
    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync() => GetEconomicCalendarAllAsync(CancellationToken.None);
    /// <summary>Get Economic Calendar All using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1133", Justification = "Retained forwarding compatibility entry point; new callers use explicit bounded page queries.")]
    [Obsolete("Use GetEconomicCalendarPageAsync with explicit UTC bounds and country codes.")]
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync(CancellationToken cancellationToken)
    {
        var countries = await GetEconomicCalendarCountryCodesAsync(cancellationToken)
            .ConfigureAwait(false);
        if (countries.Count == 0)
            return [];
        var now = BusinessClock.GetUtcNow().UtcDateTime;
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

    /// <summary>Get Economic Calendar Country Codes using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync() => GetEconomicCalendarCountryCodesAsync(CancellationToken.None);
    /// <summary>Get Economic Calendar Country Codes using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetEconomicCalendarCountryCodes", MarketDataDbSql.GetEconomicCalendarCountryCodes)
            .SetParameters(new GetEconomicCalendarCountryCodes(EconomicCalendarLookupId))
            .ExecuteQueryAsync(MapToEconomicCalendarCountryCode, cancellationToken))
                .ConfigureAwait(false);
    /// <summary>Delete Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task DeleteEconomicCalendarAsync(EconomicCalendarId id)
    {
        var eventDate = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(id.EventDate);
        var preparedParameters50 = new DeleteEconomicCalendarV2(id.CountryCode, MarketDataDbContextExtensions.EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName);
        await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.DeleteEconomicCalendarV2", MarketDataDbSql.DeleteEconomicCalendarV2)
            .SetParameters(preparedParameters50)
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>Insert Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="economicCalendar">The economic calendar used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar) => InsertEconomicCalendarsAsync(MarketDataImportBatch.Single(economicCalendar));
    /// <summary>Persists a caller-qualified batch of EconomicCalendarReadModel rows.</summary>
    /// <param name="batch">The accepted rows, duplicate policy and source command identity.</param>
    /// <returns>Completion of the persistence operation.</returns>
    public async Task InsertEconomicCalendarsAsync(MarketDataImportBatch<EconomicCalendarReadModel> batch)
    {
            await new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection,ConnectionString,"System.Data.Postgres"))
                .ExecuteOnceAsync(async (transaction,token) => { await MarketDataDbContextExtensions.ImportEconomicCalendarsAsync(transaction,batch,token)
            .ConfigureAwait(false); return true; })
            .ConfigureAwait(false);
            return;
    }

    /// <summary>Update Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <param name="economicCalendar">The economic calendar used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar)
    {
            var date = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(id.EventDate);
            await new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection,ConnectionString,"System.Data.Postgres"))
                .ExecuteOnceAsync(async (transaction,token) =>
                {
                    await transaction.ExecuteAsync(MarketDataDbSql.DeleteEconomicCalendarV2,(object?[])new DeleteEconomicCalendarV2(id.CountryCode,MarketDataDbContextExtensions.EconomicCalendarMonthBucket(date),date,id.EventName).Bind(),token)
            .ConfigureAwait(false);
                    await MarketDataDbContextExtensions.ImportEconomicCalendarsAsync(transaction,MarketDataImportBatch.Single(economicCalendar),token)
            .ConfigureAwait(false);
                    return true;
                })
            .ConfigureAwait(false);
            return;
    }





    internal const string FuturesItiSignalQueryProjection = "futures_iti_signal_queries";
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    internal static FuturesItiProjectionScopeData MapToFuturesItiProjectionScope<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord => new(row.GetString(0), row.GetDateOnly(1), row.GetString(2), row.GetString(3));
    internal static readonly MessagePackSerializerOptions MarketOutlookSerializerOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance).WithCompression(MessagePackCompression.Lz4BlockArray);
    /// <summary>Upsert Market Outlook Snapshot using the MarketDataDb persistence.</summary>
    /// <param name="snapshot">The snapshot used by this operation.</param>
    /// <param name="revision">The revision used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task UpsertMarketOutlookSnapshotAsync(MarketOutlookReadModel snapshot, long revision = 0, CancellationToken cancellationToken = default)
    {
        var payload = MessagePackSerializer.Serialize(snapshot, MarketOutlookSerializerOptions,cancellationToken:cancellationToken);
        var eod = MessagePackSerializer.Serialize(snapshot.FuturesEodData, MarketOutlookSerializerOptions,cancellationToken:cancellationToken);
        var tradeSignal = snapshot.FuturesTradeSignal is null ? null : MessagePackSerializer.Serialize(snapshot.FuturesTradeSignal, MarketOutlookSerializerOptions,cancellationToken:cancellationToken);
        var preparedParameters51 = new UpsertMarketOutlookSnapshot(snapshot.ContractId, snapshot.ValueDate, revision, snapshot.UpdatedAtUtc, eod, tradeSignal, snapshot.MissingInputs, payload);
        await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.UpsertMarketOutlookSnapshot", MarketDataDbSql.UpsertMarketOutlookSnapshot)
            .SetParameters(preparedParameters51)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Market Outlook Snapshot using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<MarketOutlookReadModel?> GetMarketOutlookSnapshotAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contractId) || valueDate == default)
            return null;
        var preparedParameters52 = new GetMarketOutlookSnapshot(contractId, valueDate);
        return await _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetMarketOutlookSnapshot", MarketDataDbSql.GetMarketOutlookSnapshot)
            .SetParameters(preparedParameters52)
            .ExecuteSingleAsync(MapToMarketOutlookSnapshot, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
    /// <summary>Insert Futures Ema Signal using the MarketDataDb persistence.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertFuturesEmaSignalAsync(FuturesEmaSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        var metadata = signal.Metadata;
        var preparedParameters53 = new InsertFuturesEmaSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10, signal.PreviousEma10, signal.Ema10Slope, signal.Ema20, signal.PreviousEma20, signal.Ema20Slope, signal.Ema50, signal.PreviousEma50, signal.Ema50Slope, signal.Ema200, signal.PreviousEma200, signal.Ema200Slope, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid);
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesEmaSignal", MarketDataDbSql.InsertFuturesEmaSignal)
            .SetParameters(preparedParameters53)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <summary>Insert Futures Bollinger Band Signal using the MarketDataDb persistence.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertFuturesBollingerBandSignalAsync(FuturesBbSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        var metadata = signal.Metadata;
        var preparedParameters54 = new InsertFuturesBollingerBandSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10Center, signal.StandardDeviation10, signal.Upper10, signal.Lower10, signal.Width10, signal.Position10, signal.Ema20Center, signal.StandardDeviation20, signal.Upper20, signal.Lower20, signal.Width20, signal.Position20, signal.Width20Baseline, signal.Width20Ratio, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid);
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesBollingerBandSignal", MarketDataDbSql.InsertFuturesBollingerBandSignal)
            .SetParameters(preparedParameters54)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <summary>Get Latest Futures Ema Signal using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<FuturesEmaSignalReadModel?> GetLatestFuturesEmaSignalAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, CancellationToken cancellationToken = default) => this.ReadLatestAsync(seriesIdentity, valueDate, EmaConfigurationId, MarketDataDbSql.GetLatestFuturesEmaSignal, nameof(MarketDataDbSql.GetLatestFuturesEmaSignal), MapToFuturesEmaSignal, cancellationToken);
    /// <inheritdoc/>
    /// <summary>Get Latest Futures Bollinger Band Signal using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<FuturesBbSignalReadModel?> GetLatestFuturesBollingerBandSignalAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, CancellationToken cancellationToken = default) => this.ReadLatestAsync(seriesIdentity, valueDate, BollingerBandConfigurationId, MarketDataDbSql.GetLatestFuturesBollingerBandSignal, nameof(MarketDataDbSql.GetLatestFuturesBollingerBandSignal), MapToFuturesBollingerBandSignal, cancellationToken);
    /// <summary>Get Latest Futures Bollinger Band Signal For Time Frame using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timeFrame">The time frame used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<FuturesBbSignalReadModel?> GetLatestFuturesBollingerBandSignalForTimeFrameAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, TimeFrameType timeFrame, CancellationToken cancellationToken = default)
    {
        return this.ReadMonthAsync(seriesIdentity, valueDate, valueDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), BollingerBandConfigurationId, MarketDataDbSql.GetLatestFuturesBollingerBandSignal, nameof(MarketDataDbSql.GetLatestFuturesBollingerBandSignal), MapToFuturesBollingerBandSignal, cancellationToken, timeFrame);
    }

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="value">The value used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="value">The value used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="value">The value used by this operation.</param>
    /// <param name="kind">The kind used by this operation.</param>
    /// <param name="sourceSequenceIndex">The source sequence index used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
    /// <summary>Insert Futures Vwap Signal using the MarketDataDb persistence.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertFuturesVwapSignalAsync(FuturesVwapSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        var preparedParameters55 = new InsertFuturesVwapSignal(signal.ContractId, signal.ValueDate, signal.ConfigurationId, signal.AsOfUtc.UtcDateTime, signal.LastTradeOrdinal, signal.SessionStartUtc.UtcDateTime, signal.SessionEndUtc.UtcDateTime, signal.CumulativePriceVolume, signal.CumulativeVolume, signal.EligibleTradeCount, signal.RejectedTradeCount, signal.LastPrice, signal.Vwap, signal.PriceMinusVwap, signal.PriceToVwapPercent, signal.LastTradeSourceSequence, signal.StreamEpochId, signal.IsWarm, signal.IsValid, signal.InvalidReason.ToString(), signal.IsTickExact, signal.CalculationMethod.ToString(), signal.SchemaVersion, signal.CalculationVersion);
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesVwapSignal", MarketDataDbSql.InsertFuturesVwapSignal)
            .SetParameters(preparedParameters55)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <summary>Get Latest Futures Vwap Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<FuturesVwapSignalReadModel?> GetLatestFuturesVwapSignalAsync(string contractId, DateOnly valueDate, string configurationId, CancellationToken cancellationToken = default) => _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLatestFuturesVwapSignal", MarketDataDbSql.GetLatestFuturesVwapSignal)
            .SetParameters(new GetLatestFuturesVwapSignal(contractId, valueDate, configurationId))
            .ExecuteSingleAsync(MapToFuturesVwapSignal!, cancellationToken);
    /// <inheritdoc/>
    /// <summary>Get Futures Vwap Signal History using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ICollection<FuturesVwapSignalReadModel>> GetFuturesVwapSignalHistoryAsync(string contractId, DateOnly valueDate, string configurationId, CancellationToken cancellationToken = default) => await (_dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetFuturesVwapSignalHistory", MarketDataDbSql.GetFuturesVwapSignalHistory)
            .SetParameters(new GetFuturesVwapSignalHistory(contractId, valueDate, configurationId))
            .ExecuteQueryAsync(MapToFuturesVwapSignal!, cancellationToken))
                .ConfigureAwait(false);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
    /// <summary>Insert Futures Vx Term Structure Signal using the MarketDataDb persistence.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertFuturesVxTermStructureSignalAsync(FuturesVxTermStructureSignalReadModel signal, CancellationToken cancellationToken = default)
    {
        var preparedParameters56 = new InsertFuturesVxTermStructureSignal(signal.ValueDate, signal.ConfigurationId, signal.CalculatedAtUtc.UtcDateTime, signal.FrontSourceSequence, signal.BackSourceSequence, signal.FrontVxContractId, signal.FrontExpiry, signal.FrontVxPrice, signal.BackVxContractId, signal.BackExpiry, signal.BackVxPrice, signal.FrontBackSpread, signal.FrontBackRatio, signal.TermStructurePercent, signal.TermStructureState.ToString(), signal.PriorFrontBackRatio, signal.PriorTermStructurePercent, signal.FrontSourceTimestampUtc.UtcDateTime, signal.BackSourceTimestampUtc.UtcDateTime, signal.IsWarm, signal.IsValid, signal.SchemaVersion, signal.CalculationVersion);
        return _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.InsertFuturesVxTermStructureSignal", MarketDataDbSql.InsertFuturesVxTermStructureSignal)
            .SetParameters(preparedParameters56)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <summary>Get Latest Futures Vx Term Structure Signal using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public Task<FuturesVxTermStructureSignalReadModel?> GetLatestFuturesVxTermStructureSignalAsync(DateOnly valueDate, string configurationId, CancellationToken cancellationToken = default) => _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetLatestFuturesVxTermStructureSignal", MarketDataDbSql.GetLatestFuturesVxTermStructureSignal)
            .SetParameters(new GetLatestFuturesVxTermStructureSignal(valueDate, configurationId))
            .ExecuteSingleAsync(MapToFuturesVxTermStructureSignal!, cancellationToken);
    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
    /// <summary>Insert Tick Trade Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent e)
    {
        return _dbFactory.TradeTickQuoteDb.InsertTickTradeDataAsync(e);
    }

    /// <summary>Writes a bounded quote segment as one native CQL nested-UDT-list value.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "InsertTickQuoteData transfers the encoded value through IScyllaOwnedBindValues.BindLifetime; the Scylla executor disposes it after binding/execution, including failure.")]
    public Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e)
    {
        return _dbFactory.TradeTickQuoteDb.InsertTickQuoteDataAsync(e);
    }

    internal const string CompositionPreparationSelect = MarketDataDbSql.MarketDataDbContext_Statement_1;
    internal const string CompositionPreparationInsert = MarketDataDbSql.MarketDataDbContext_Statement_2;
    /// <summary>Read using the MarketDataDb persistence.</summary>
    /// <param name="key">The key used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key, CancellationToken cancellationToken)
    {
        var parameters = new NativeCompositionReadParameters(key.WorkflowId, key.InputRevision);
        var result = await this.Database.Use("CompositionPreparation.Read", MarketDataDbSql.GetCompositionPreparation)
            .SetParameters(parameters)
            .ExecuteSingleAsync<CompositionPreparation?>(MapToReadRow3, cancellationToken)
            .ConfigureAwait(false);
        if (result is not null && result.Key != key)
            throw new InvalidOperationException("Workflow preparation conflicts with the accepted input hash.");
        return result;
    }

    /// <summary>Read using the MarketDataDb persistence.</summary>
    /// <param name="lookup">The exact caller-qualified trade source identity.</param>
    /// <param name="cancellationToken">The token used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<OptionTradeEvidence?> ReadAsync(OptionTradeEvidenceLookup lookup, CancellationToken cancellationToken = default)
        => this.ReadOptionTradeEvidenceCoreAsync(new(lookup.Contract, lookup.Date, lookup.SourceId), cancellationToken);

    /// <summary>Commit using the MarketDataDb persistence.</summary>
    /// <param name="proposed">The proposed used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<CompositionPreparation> CommitAsync(CompositionPreparation proposed, CancellationToken cancellationToken)
    {
        var preparedParameters59 = new NativeCompositionWriteParameters(proposed.Key.WorkflowId, proposed.Key.InputRevision, MessagePackBinarySerializer.Shared.Serialize(proposed)!);
        await this.Database.Use("CompositionPreparation.Commit", MarketDataDbSql.InsertCompositionPreparation)
            .SetParameters(preparedParameters59)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync(proposed.Key, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Committed composition preparation could not be read back.");
    }

    /// <summary>Write using the MarketDataDb persistence.</summary>
    /// <param name="evidence">The evidence used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        if (await this.TryInsertOptionTradeEvidenceAsync(evidence, cancellationToken)
            .ConfigureAwait(false)) return;
        var stored = await this.ReadAcceptedOptionTradeEvidenceAsync(evidence.Source, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(stored.Source.SourceDigest, evidence.Source.SourceDigest, StringComparison.Ordinal))
            throw new InvalidOperationException("Trade source identity collision or durable write not confirmed.");
    }

    /// <inheritdoc />
    public async ValueTask<bool> WriteFirstAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        if (await this.TryInsertOptionTradeEvidenceAsync(evidence, cancellationToken)
            .ConfigureAwait(false)) return true;
        var stored = await this.ReadAcceptedOptionTradeEvidenceAsync(evidence.Source, cancellationToken)
            .ConfigureAwait(false);
        // An exact retry still needs its persistence acknowledgment if the first reply was lost.
        var serializer = MessagePackBinarySerializer.Shared;
        return serializer.Serialize(stored)!.AsSpan().SequenceEqual(serializer.Serialize(evidence));
    }


    /// <summary>Publish using the MarketDataDb persistence.</summary>
    /// <param name="publication">The publication used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task PublishAsync(OptionIvPublication publication, CancellationToken cancellationToken = default)
    {
            await new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection,ConnectionString,"System.Data.Postgres"))
            .ExecuteOnceAsync(async (transaction,token) => { await MarketDataDbContextExtensions.PublishOptionIvAsync(transaction,publication,token)
            .ConfigureAwait(false); return true; },cancellationToken)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>Get Snapshot using the MarketDataDb persistence.</summary>
    /// <param name="environment">The environment used by this operation.</param>
    /// <param name="snapshotId">The snapshot id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId, CancellationToken cancellationToken = default)
    {
        Func<IObjectDataRecord, OptionIvMetricRevision> map = row =>
        {
            var value = MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>(row.GetBytes(2));
            if (!string.Equals(value.Snapshot.SnapshotId,snapshotId,StringComparison.Ordinal) || !string.Equals(value.Snapshot.SnapshotDigest,row.GetString(0),StringComparison.Ordinal) || value.PublicationSequence != row.GetLong(1))
                throw new InvalidDataException("Stored snapshot identity is invalid.");
            return value;
        };
        var preparedParameters60 = new OptionVolatilityParameters([environment, snapshotId]);
        return await this.Database.Use("OptionVolatility.Snapshot.Read", MarketDataDbSql.SelectSnapshot)
            .SetParameters(preparedParameters60)
            .ExecuteSingleAsync(map, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Latest using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request, CancellationToken cancellationToken = default)
    {
        var pointer = await this.ReadLatestAsync(request.Scope, cancellationToken)
            .ConfigureAwait(false);
        if (pointer is null)
            return new(null, VolatilityFreshnessStatus.Unavailable);
        var metric = await GetSnapshotAsync(request.Scope.Environment, pointer.SnapshotId, cancellationToken)
            .ConfigureAwait(false);
        if (metric is null || !string.Equals(metric.Snapshot.SnapshotDigest,pointer.SnapshotDigest,StringComparison.Ordinal))
            throw new InvalidDataException("Latest pointer references a missing or conflicting sealed snapshot.");
        return new(metric, TomasAI.IFM.Application.MarketData.Databento.Historical.MarketTradingTime.Elapsed(freshnessCalendar, pointer.AvailableAtUtc, request.RequestedAtUtc, request.MaximumAge) <= request.MaximumAge ? VolatilityFreshnessStatus.Accepted : VolatilityFreshnessStatus.Stale);
    }

    /// <summary>Get Observation History using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        return await new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection,ConnectionString,"System.Data.Postgres"))
            .ExecuteOnceAsync((transaction,token) => MarketDataDbContextExtensions.ReadOptionIvObservationHistoryAsync(transaction,request,token),cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Metric History using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        return await new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection,ConnectionString,"System.Data.Postgres"))
            .ExecuteOnceAsync((transaction,token) => MarketDataDbContextExtensions.ReadOptionIvMetricHistoryAsync(transaction,request,token),cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reads persisted latest volatility pointers for the supplied scopes.</summary>
    /// <param name="scopes">The scopes used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>> ReadLatestVolatilityPointersAsync(IEnumerable<VolatilityStorageScope> scopes, CancellationToken cancellationToken = default)
    {
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

    /// <summary>Try Write Observation using the MarketDataDb persistence.</summary>
    /// <param name="observation">The observation used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    /// <inheritdoc />
    public Task<FuturesTradeSessionBarReadModel?> GetStoredTradeSessionBarAsync(string contractId, TimeFrameType frame,
        DateOnly valueDate, DateTimeOffset intervalEndUtc, CancellationToken cancellationToken = default)
        => this.Database.Use(nameof(MarketDataDbSql.GetStoredTradeSessionBar), MarketDataDbSql.GetStoredTradeSessionBar)
            .SetParameters(new StoredTradeSessionBarKey(contractId, frame, valueDate, intervalEndUtc))
            .ExecuteSingleAsync<FuturesTradeSessionBarReadModel?>(row => MapToStoredTradeSessionBar(row, contractId, frame, valueDate), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FuturesTradeSessionBarReadModel>> GetStoredTradeSessionBarsAsync(string contractId,
        TimeFrameType frame, DateOnly valueDate, DateTimeOffset[] intervalEndsUtc, CancellationToken cancellationToken = default)
    {
        if (intervalEndsUtc.Length == 0) return [];
        var preparedParameters61 = new StoredTradeSessionBarKeys(contractId, frame, valueDate, intervalEndsUtc);
        return (await this.Database.Use(nameof(MarketDataDbSql.GetStoredTradeSessionBars), MarketDataDbSql.GetStoredTradeSessionBars)
            .SetParameters(preparedParameters61)
            .ExecuteQueryAsync(row => MapToStoredTradeSessionBar(row, contractId, frame, valueDate), cancellationToken)
            .ConfigureAwait(false)).ToArray();
    }

    internal static FuturesTradeSessionBarReadModel MapToStoredTradeSessionBar(IObjectDataRecord row, string contractId,
        TimeFrameType frame, DateOnly valueDate) => new()
    {
        MarketSeriesIdentity = MarketSeriesIdentity.ForContract(contractId), ContractId = contractId,
        TimeFrame = frame, ValueDate = valueDate, ObservationId = new(row.GetGuid(0)),
        IntervalStartUtc = new(DateTime.SpecifyKind(row.GetDateTime(1), DateTimeKind.Utc)),
        IntervalEndUtc = new(DateTime.SpecifyKind(row.GetDateTime(2), DateTimeKind.Utc)),
        Open = row.GetDecimal(3), High = row.GetDecimal(4), Low = row.GetDecimal(5), Close = row.GetDecimal(6),
        Volume = row.GetDecimal(7), TradeCount = row.GetLong(8), PriceVolumeSum = row.GetDecimal(9),
        FirstSourceSequence = row.GetLong(10), LastSourceSequence = row.GetLong(11),
        FirstMarketEventUtc = new(DateTime.SpecifyKind(row.GetDateTime(12), DateTimeKind.Utc)),
        LastMarketEventUtc = new(DateTime.SpecifyKind(row.GetDateTime(13), DateTimeKind.Utc)),
        CalculatedAtUtc = new(DateTime.SpecifyKind(row.GetDateTime(14), DateTimeKind.Utc)),
        SchemaVersion = checked((ushort)row.GetInt(15)), CalculationVersion = row.GetString(16),
        CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate, IsComplete = true, IsValid = true
    };

    /// <inheritdoc />
    public async ValueTask<bool> TryWriteObservationAsync(FuturesTradeSessionBarReadModel observation, CancellationToken cancellationToken)
    {
        var preparedParameters62 = new HistoricalObservationParameter(observation);
        return await this.Database.Use($"{nameof(MarketDataDbSql)}.TryInsertObservation", MarketDataDbSql.TryInsertObservation)
            .SetParameters(preparedParameters62)
            .ExecuteSingleAsync(MapToTryWriteObservationRow4, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Write Raw Eod using the MarketDataDb persistence.</summary>
    /// <param name="observation">The observation used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async ValueTask<bool> TryWriteRawEodAsync(FuturesEodObservationReadModel observation, CancellationToken cancellationToken)
    {
        var preparedParameters63 = new HistoricalRawEodParameter(observation);
        return await this.Database.Use($"{nameof(MarketDataDbSql)}.TryInsertRawEod", MarketDataDbSql.TryInsertRawEod)
            .SetParameters(preparedParameters63)
            .ExecuteSingleAsync(MapToTryWriteRawEodRow5, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Raw Eod using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, CancellationToken cancellationToken) => new(this.Database.Use($"{nameof(MarketDataDbSql)}.GetRawEod", MarketDataDbSql.GetRawEod)
            .SetParameters(new HistoricalRawEodKey(seriesIdentity.Format(), MarketDataDbContextExtensions.YearMonth(valueDate), valueDate))
            .ExecuteSingleAsync<FuturesEodObservationReadModel?>(MapToRawEod, cancellationToken));
    /// <summary>Get Raw Eod Range using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async ValueTask<IReadOnlyList<FuturesEodObservationReadModel>> GetRawEodRangeAsync(MarketSeriesIdentity seriesIdentity, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        List<FuturesEodObservationReadModel> values = [];
        for (var month = new DateOnly(startDate.Year, startDate.Month, 1); month <= endDate; month = month.AddMonths(1))
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            var lower = startDate > month ? startDate : month;
            var upper = endDate < monthEnd ? endDate : monthEnd;
        var preparedParameters64 = new HistoricalRawEodRangeKey(seriesIdentity.Format(), MarketDataDbContextExtensions.YearMonth(month), lower, upper);
            var rows = await this.Database.Use($"{nameof(MarketDataDbSql)}.GetRawEodRange", MarketDataDbSql.GetRawEodRange)
            .SetParameters(preparedParameters64)
            .ExecuteQueryAsync(MapToRawEod, cancellationToken)
            .ConfigureAwait(false);
            values.AddRange(rows);
        }

        return values.OrderBy(static value => value.ValueDate).ThenBy(static value => value.ContractId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Maps selected market-data columns to the typed result.</summary>
    /// <param name="row">The row used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
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
    /// <summary>Projects a committed configuration snapshot into immutable version and monotonic current tables.</summary>
    /// <param name="parameters">The parameters used by this operation.</param>
    /// <param name="revision">The revision used by this operation.</param>
    /// <param name="published">The published used by this operation.</param>
    /// <param name="token">The token used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task ProjectAsync(StrategyOptionChainParameterSet parameters, long revision, bool published, CancellationToken token)
    {
            var owner = new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings()
                .Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection, ConnectionString, "System.Data.Postgres"));
            await owner.ExecuteOnceAsync(async (transaction, cancellationToken) =>
            {
                await MarketDataDbContextExtensions.PersistOptionChainParametersAsync(transaction, parameters, revision, published, cancellationToken)
            .ConfigureAwait(false);
                return true;
            }, token)
            .ConfigureAwait(false);
            return;
    }

    /// <summary>Queries persisted current published rows; parameter queries never load command streams.</summary>
    /// <param name="environment">The environment used by this operation.</param>
    /// <param name="token">The token used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<ImmutableArray<StrategyOptionChainParameterSet>> ReadPublishedAsync(string environment, CancellationToken token)
    {
        await using var connection=CreateConnection().As<Npgsql.NpgsqlConnection>(ConnectionString);await connection.OpenAsync(token)
            .ConfigureAwait(false);
        await using var transaction=await connection.BeginTransactionAsync(token)
            .ConfigureAwait(false);
        var session=new ConfigurationDb.ConfigurationDbSession(connection,transaction);
        var rows=await session.QueryAsync(MarketDataDbSql.MarketDataDbContext_ReadPublishedAsync_1, [environment], MapToReadPublishedRow6, token)
            .ConfigureAwait(false);
        if(rows.Count>128)throw new InvalidDataException("Global strategy parameter capacity exceeded");
        var result=ImmutableArray.CreateBuilder<StrategyOptionChainParameterSet>();
        foreach(var row in rows.Where(x=>x.Published))
        {
            var value=await session.ReadTypedValueAsync(row.Id,row.Version,token)
            .ConfigureAwait(false)
                ??throw new InvalidDataException("Missing option-chain parameter values");
            var policy=value.Get<StrategyOptionChainParameterSet>();
            if(policy.Hash()!=row.Hash)throw new InvalidDataException("Option-chain parameter hash mismatch");
            if(policy.Enabled)result.Add(policy);
        }
        await transaction.CommitAsync(token)
            .ConfigureAwait(false);return result.ToImmutable();
    }

    /// <summary>Reads an exact immutable policy reference and its concrete values.</summary>
    /// <param name="setId">The set id supplied by the caller.</param>
    /// <param name="version">The exact immutable reference revision.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<StrategyOptionChainParameterSet?> ReadVersionAsync(Guid setId,int version,CancellationToken token)
    {
        await using var connection=CreateConnection().As<Npgsql.NpgsqlConnection>(ConnectionString);await connection.OpenAsync(token)
            .ConfigureAwait(false);
        await using var transaction=await connection.BeginTransactionAsync(token)
            .ConfigureAwait(false);
        var session=new ConfigurationDb.ConfigurationDbSession(connection,transaction);
        var hash=await session.ScalarAsync(MarketDataDbSql.GetOptionChainParameterVersion, [setId, version], token)
            .ConfigureAwait(false);
        if(hash is null)return null;
        var value=await session.ReadTypedValueAsync(setId,version,token)
            .ConfigureAwait(false)
            ??throw new InvalidDataException("Missing option-chain parameter values");
        var policy=value.Get<StrategyOptionChainParameterSet>();
        if(policy.Hash()!=(string)hash)throw new InvalidDataException("Option-chain parameter hash mismatch");
        await transaction.CommitAsync(token)
            .ConfigureAwait(false);return policy;
    }
    /// <inheritdoc/>
    public Task<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?> GetHistoricalAnalyticsAttemptProjectionAsync(
        Guid attemptId, CancellationToken cancellationToken = default) =>
        Use(nameof(MarketDataDbSql.GetHistoricalAnalyticsAttemptProjection), MarketDataDbSql.GetHistoricalAnalyticsAttemptProjection)
            .SetParameters(new HistoricalDataLoadAttemptParameter(attemptId))
            .ExecuteSingleAsync(MapToHistoricalAnalyticsAttemptProjection, cancellationToken);

    private static FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel? MapToHistoricalAnalyticsAttemptProjection(IObjectDataRecord row) =>
        JsonSerializer.Deserialize<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel>(row.GetString(0));

    /// <inheritdoc/>
    public Task<ICollection<HistoricalDataLoaderState>> ReadUnprojectedHistoricalDataLoadsAsync(CancellationToken cancellationToken) =>
        Use(nameof(MarketDataDbSql.ReadUnprojectedHistoricalDataLoads), MarketDataDbSql.ReadUnprojectedHistoricalDataLoads)
            .ExecuteQueryAsync(MapToHistoricalDataLoaderState, cancellationToken);

    /// <inheritdoc/>
    public Task BackfillHistoricalAnalyticsProgressAsync(FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel diagnostics, CancellationToken cancellationToken) =>
        Use(nameof(MarketDataDbSql.BackfillHistoricalAnalyticsProgress), MarketDataDbSql.BackfillHistoricalAnalyticsProgress)
            .SetParameters(new HistoricalAnalyticsProgressParameter(diagnostics.DataLoadAttemptId, JsonSerializer.Serialize(diagnostics)))
            .ExecuteCommandAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task UpsertHistoricalAnalyticsAttemptProjectionAsync(
        FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel diagnostics, long eventId, CancellationToken cancellationToken = default)
    {
        var preparedParameters65 = new HistoricalAnalyticsAttemptProjectionParameter(diagnostics.DataLoadAttemptId, eventId, JsonSerializer.Serialize(diagnostics));
        await Use(nameof(MarketDataDbSql.UpsertHistoricalAnalyticsAttemptProjection), MarketDataDbSql.UpsertHistoricalAnalyticsAttemptProjection)
            .SetParameters(preparedParameters65)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reads one persisted historical-load checkpoint and terminal manifest.</summary>
    /// <param name="attemptId">The stable load attempt identity.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The stored attempt, or null when absent.</returns>
    public Task<HistoricalDataLoaderState?> GetHistoricalDataLoadAsync(Guid attemptId, CancellationToken cancellationToken = default) =>
        Use($"{nameof(MarketDataDbSql)}.{nameof(MarketDataDbSql.GetHistoricalDataLoadByAttempt)}", MarketDataDbSql.GetHistoricalDataLoadByAttempt)
            .SetParameters(new HistoricalDataLoadAttemptParameter(attemptId))
            .ExecuteSingleAsync<HistoricalDataLoaderState?>(MapToHistoricalDataLoaderState, cancellationToken);

    /// <summary>Reads the completed historical-load owner of a request hash.</summary>
    /// <param name="requestSha256">The stable request digest.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The completed attempt, or null when absent.</returns>
    public Task<HistoricalDataLoaderState?> GetCompletedHistoricalDataLoadAsync(string requestSha256, CancellationToken cancellationToken = default)
    {
        var preparedParameters66 = new HistoricalDataLoadHashParameter(requestSha256);
        return Use($"{nameof(MarketDataDbSql)}.{nameof(MarketDataDbSql.GetCompletedHistoricalDataLoadByRequestHash)}", MarketDataDbSql.GetCompletedHistoricalDataLoadByRequestHash)
            .SetParameters(preparedParameters66)
            .ExecuteSingleAsync<HistoricalDataLoaderState?>(MapToHistoricalDataLoaderState, cancellationToken);
    }

    /// <summary>Atomically advances a historical-load checkpoint and its prepared progress projection.</summary>
    /// <param name="state">The supplied load state to persist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of the persistence operation.</returns>
    /// <param name="diagnostics">The progress projection prepared by the historical acquisition owner.</param>
    public async Task SaveHistoricalDataLoadAsync(HistoricalDataLoaderState state, FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel diagnostics, CancellationToken cancellationToken = default)
    {
        var preparedParameters67 = new SaveHistoricalDataLoadParameter(state, JsonSerializer.Serialize(diagnostics));
        await Use($"{nameof(MarketDataDbSql)}.{nameof(MarketDataDbSql.SaveHistoricalDataLoad)}", MarketDataDbSql.SaveHistoricalDataLoad)
            .SetParameters(preparedParameters67)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Maps checkpoint and optional manifest columns to the historical-load state.</summary>
    /// <param name="row">The selected database row.</param>
    /// <returns>The persisted state.</returns>
    internal static HistoricalDataLoaderState MapToHistoricalDataLoaderState(IObjectDataRecord row) => new()
    {
        DataLoadAttemptId = row.GetGuid(0),
        RequestSha256 = row.GetString(1),
        Status = (HistoricalDataLoaderStatus)row.GetShort(2),
        Checkpoint = new HistoricalAcquisitionCheckpoint
        {
            DataLoadAttemptId = row.GetGuid(0),
            Stage = (HistoricalAcquisitionStage)row.GetShort(3),
            ProviderJobId = row.GetString(4),
            ProviderFileId = row.GetString(5),
            BatchOrdinal = row.GetLong(6),
            SourcePosition = row.GetString(7)
        },
        ErrorMessage = row.GetString(8),
        UpdatedAtUtc = new DateTimeOffset(
            DateTime.SpecifyKind(row.GetDateTime(9), DateTimeKind.Utc)),
        Manifest = row.IsNull(10)
            ? null
            : JsonSerializer.Deserialize<MarketDataHistoricalManifest>(row.GetString(10)),
        Audit = row.IsNull(11)
            ? null
            : JsonSerializer.Deserialize<HistoricalDataLoaderAudit>(row.GetString(11))
    };

    /// <inheritdoc />
    public ValueTask<bool[]> WriteFirstBatchAsync(IReadOnlyList<OptionTradeEvidence> evidence, CancellationToken cancellationToken)
        => this.WriteFirstBatchCoreAsync(evidence, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<FuturesTradeSessionBarReadModel>> GetQualifiedIndicatorSeedBarsAsync(string contractId,
        TimeFrameType frame, DateOnly valueDate, DateTimeOffset[] intervalEndsUtc, string sourceVersion, CancellationToken cancellationToken)
        => this.ReadQualifiedIndicatorSeedsCoreAsync(contractId, frame, valueDate, intervalEndsUtc, sourceVersion, cancellationToken);
    /// <inheritdoc />
    public Task StoreQualifiedIndicatorSeedBarsAsync(string sourceVersion, FuturesTradeSessionBarReadModel[] bars, CancellationToken cancellationToken)
        => this.StoreQualifiedIndicatorSeedsCoreAsync(sourceVersion, bars, cancellationToken);
    internal readonly record struct EvidenceKey(string Contract, DateOnly Date, string Identity)
    {
        public static EvidenceKey From(OptionTradeSource source) => new(source.ContractId, source.ValueDate, source.Identity);
    }
    internal static EvidenceKey MapToEvidenceKey(IObjectDataRecord row) =>
        new(row.GetString(0), row.GetDateOnly(1), row.GetString(2));

    internal static OptionTradeEvidence MapToOptionTradeEvidence(IObjectDataRecord row, EvidenceKey key) =>
        MapToOptionTradeEvidence(key, row.GetString(0), row.GetBytes(1));

    internal static (DateOnly Date, string Slot, byte[] Payload) MapToVolatilityHistoryRow(System.Data.Common.DbDataReader row) =>
        (row.GetFieldValue<DateOnly>(0), row.GetString(1), row.GetFieldValue<byte[]>(2));

    internal static OptionTradeEvidence MapToOptionTradeEvidence(EvidenceKey key, string digest, byte[] payload)
    {
        if (payload.Length is 0 or > 131072)
            throw new InvalidDataException("Invalid trade evidence payload size.");
        var value = MessagePackBinarySerializer.Shared.Deserialize<OptionTradeEvidence>(payload)
            ?? throw new InvalidDataException("Missing trade evidence.");
        value.Validate();
        if (EvidenceKey.From(value.Source) != key || !string.Equals(value.Source.SourceDigest, digest, StringComparison.Ordinal))
            throw new InvalidDataException("Trade evidence identity mismatch.");
        return value;
    }    internal static FuturesTradeSessionBarReadModel MapToCachedIndicatorSeedBar(IObjectDataRecord row, string contractId,
        TimeFrameType frame, DateOnly valueDate)
    {
        var bar = MessagePackBinarySerializer.Shared.Deserialize<FuturesTradeSessionBarReadModel>(row.GetBytes(17));
        var end = new DateTimeOffset(DateTime.SpecifyKind(row.GetDateTime(2), DateTimeKind.Utc));
        if (bar is null || bar.ContractId != contractId || bar.TimeFrame != frame || bar.ValueDate != valueDate
            || bar.IntervalEndUtc != end || bar is not { IsComplete: true, IsValid: true })
            throw new InvalidDataException("Stored indicator seed payload does not match its qualified interval identity.");
        return bar;
    }    internal const int MaximumPayloadBytes = 1_048_576;
    /// <summary>Append Observation using the MarketDataDb persistence.</summary>
    /// <param name="environment">The environment used by this operation.</param>
    /// <param name="observation">The observation used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task AppendObservationAsync(string environment, OptionIvObservation observation, CancellationToken cancellationToken = default)
    {
            await new EventSourceDb.PostgresEventTransaction(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection,ConnectionString,"System.Data.Postgres"))
            .ExecuteOnceAsync(async (transaction,token) => { await MarketDataDbContextExtensions.AppendOptionIvObservationAsync(transaction,environment,observation,token)
            .ConfigureAwait(false); return true; },cancellationToken)
            .ConfigureAwait(false);
            return;
    }


    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot MapToReadOptionChainStatisticsRow1(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => new TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot(row.GetString(0), row.IsNull(1) ? null : row.GetLong(1), row.IsNull(2) ? null : row.GetDateOnly(2),
                row.IsNull(3) ? null : row.GetLong(3), row.IsNull(4) ? null : row.GetDateOnly(4));

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static string MapToGetFuturesItiSignalsRow2(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => row.GetString(0);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::TomasAI.IFM.Application.MarketData.Pricing.CompositionPreparation MapToReadRow3(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => MessagePackBinarySerializer.Shared.Deserialize<CompositionPreparation>(row.GetBytes(0));

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static bool MapToTryWriteObservationRow4(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => row.GetBool(0);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static bool MapToTryWriteRawEodRow5(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => row.GetBool(0);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static (global::System.Guid Id, int Version, bool Published, string Hash) MapToReadPublishedRow6(global::System.Data.Common.DbDataReader reader) => (Id:reader.GetGuid(0),Version:reader.GetInt32(1),Published:reader.GetBoolean(2),Hash:reader.GetString(3));
internal static global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.OptionIvObservation MapToReadObservationRow1(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => MarketDataDbContextExtensions.Deserialize<OptionIvObservation>(row.GetBytes(0));
internal static global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.OptionIvLatestPointer MapToReadLatestRow2(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row, global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.VolatilityStorageScope scope) => new OptionIvLatestPointer(scope, row.GetString(0), row.GetString(1), row.GetLong(2), new DateTimeOffset(DateTime.SpecifyKind(row.GetDateTime(3), DateTimeKind.Utc)));
internal static global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel MapToReadQualifiedIndicatorSeedsCoreRow3(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row, string contractId, global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType frame, global::System.DateOnly valueDate) => row.IsNull(17) ? MarketDataDbContext.MapToStoredTradeSessionBar(row, contractId, frame, valueDate)
                    : MarketDataDbContext.MapToCachedIndicatorSeedBar(row, contractId, frame, valueDate);
internal static (global::TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbContext.EvidenceKey Key, global::TomasAI.IFM.Application.MarketData.Pricing.OptionTradeEvidence Value) MapToWriteFirstBatchCoreRow4(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row)                     {
                        var key = MapToEvidenceKey(row);
                        return (Key: key, Value: MarketDataDbContext.MapToOptionTradeEvidence(key, row.GetString(3), row.GetBytes(4)));
                    }
internal static bool MapToTryInsertOptionTradeEvidenceRow5(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => row.GetBool(0);
internal static global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.MarketDataProjectionReadinessReadModel MapToReadCanonicalQueryReadinessRow6(global::System.Data.Common.DbDataReader reader) => new MarketDataProjectionReadinessReadModel(false, reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2)) { RawTicksManagedByTradeTickQuoteDb = true };
    internal static MarketDataProjectionInventoryRow MapToProjectionInventory(System.Data.Common.DbDataReader row) =>
        new(row.GetString(0), row.GetInt64(1), row.GetString(2));

    internal static YieldCurveRateReadModel MapToYieldCurveDbRow(System.Data.Common.DbDataReader row) =>
        MapToYieldCurveRate(new AdoNetDataRecord().SetReader(row));
internal static global::TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.VixFuturesEodDataReadModel MapToResolveSnapshotRow8(global::System.Data.Common.DbDataReader reader) => new VixFuturesEodDataReadModel(reader.GetString(0),reader.GetFieldValue<DateOnly>(1),reader.GetDecimal(2), reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetInt64(6));
internal static global::System.Guid MapToImportEconomicCalendarsRow9(global::System.Data.Common.DbDataReader reader) => reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0);
internal static int MapToRebuildYieldCurveIndexesRow10(global::System.Data.Common.DbDataReader reader) => reader.GetInt32(0);
internal static (global::System.Guid CommandId, bool MayWrite) MapToImportYieldCurvesRow11(global::System.Data.Common.DbDataReader reader) => (CommandId:reader.GetGuid(0),MayWrite:reader.GetBoolean(1));
internal static global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.OptionIvObservation MapToAppendOptionIvObservationRow12(global::System.Data.Common.DbDataReader row) => MarketDataDbContextExtensions.Deserialize<OptionIvObservation>(row.GetFieldValue<byte[]>(0));
internal static global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.OptionIvMetricRevision MapToPublishOptionIvRow13(global::System.Data.Common.DbDataReader row) => MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>(row.GetFieldValue<byte[]>(2));
internal static (string Id, string Digest, long Sequence, global::System.DateTime Available) MapToPublishOptionIvRow14(global::System.Data.Common.DbDataReader row) => (Id:row.GetString(0),Digest:row.GetString(1),Sequence:row.GetInt64(2),Available:row.GetDateTime(3));
internal static string MapToRebuildTradeSignalIndexesRow15(global::System.Data.Common.DbDataReader reader) => reader.GetString(0);
}
