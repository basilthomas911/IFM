using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Shared.Extensions;
using MessagePack.Resolvers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using EvidenceKey = TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbContext.EvidenceKey;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using System.Collections.Immutable;
using MessagePack;


using static TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbContext;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;


using TomasAI.IFM.Application.Storage.EventSourceDb;

using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;


using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;


using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;

using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;


using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
public static class MarketDataDbContextExtensions
{
    extension(MarketDataDbContext context)
    {
        /// <summary>Read Stored Futures Eod Data using the MarketDataDb persistence.</summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the ReadStoredFuturesEodDataAsync persistence helper.</summary>
        internal async Task<FuturesEodDataV2ReadModel?> ReadStoredFuturesEodDataAsync(string contractId, DateOnly valueDate)
        {
            var db = context._dbFactory.MarketDataDb;
        var preparedParameters1 = new GetFuturesEodData(contractId, valueDate);
            var futuresEodData = await (db.Use($"{nameof(MarketDataDbSql)}.GetFuturesEodData", MarketDataDbSql.GetFuturesEodData)
            .SetParameters(preparedParameters1)
            .ExecuteSingleAsync(MarketDataDbContext.MapToFuturesEodData!))
                .ConfigureAwait(false);
        var preparedParameters2 = new GetYesterdaysFuturesEodData(contractId, valueDate);
            futuresEodData ??= await (db.Use($"{nameof(MarketDataDbSql)}.GetYesterdaysFuturesEodData", MarketDataDbSql.GetYesterdaysFuturesEodData)
            .SetParameters(preparedParameters2)
            .ExecuteSingleAsync(MarketDataDbContext.MapToFuturesEodData!))
                .ConfigureAwait(false);
            return futuresEodData;
        }

        /// <summary>Read Stored Vix Futures Eod Data using the MarketDataDb persistence.</summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the ReadStoredVixFuturesEodDataAsync persistence helper.</summary>
        internal async Task<VixFuturesEodDataReadModel?> ReadStoredVixFuturesEodDataAsync(string contractId, DateOnly valueDate) => await (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetVixFuturesEodData", MarketDataDbSql.GetVixFuturesEodData)
            .SetParameters(new GetVixFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MarketDataDbContext.MapToVixFuturesEodData))
                .ConfigureAwait(false);

        /// <summary>
        /// Performs the <c>GetProjectionGuardScopeKeys</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string[] GetProjectionGuardScopeKeys() => Enumerable.Range(0, MarketDataDbContext.ProjectionGuardScopeCount).Select(bucket => string.Concat(MarketDataDbContext.ProjectionGuardScopePrefix, bucket.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        /// <summary>
        /// Performs the <c>InsertFuturesEodBatchAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="batch">The batch used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task InsertFuturesEodBatchAsync(IReadOnlyList<FuturesEodDataV2ReadModel> batch)
        {
            if (batch.Count == 0)
                return;
                await context.InsertNativeFuturesEodBatchAsync(batch)
            .ConfigureAwait(false);
                return;
        }

        /// <summary>
        /// Performs the <c>GetYieldCurveRatesCoreAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="range">The caller-qualified yield-curve range.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        internal async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesCoreAsync(YieldCurveReadRange range, CancellationToken cancellationToken)
        {
            var startDate = range.StartDate;
            var endDate = range.EndDate;
            if (endDate < startDate)
                return [];
        var preparedParameters3 = new GetYieldCurveRates(startDate, endDate);

            return await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbSql)}.GetYieldCurveRates", MarketDataDbSql.GetYieldCurveRates)
            .SetParameters(preparedParameters3)
            .ExecuteQueryAsync(MarketDataDbContext.MapToYieldCurveRate, cancellationToken)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>GetIntrinsicTimeModes</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal List<string> GetIntrinsicTimeModes() => [IntrinsicTimeModeType.TrendExtremeChanged.ToStringFast(), IntrinsicTimeModeType.TrendReversalChanged.ToStringFast(), IntrinsicTimeModeType.TrendDirectionChanged.ToStringFast(), IntrinsicTimeModeType.TrendOvershootExtended.ToStringFast(), IntrinsicTimeModeType.TrendOvershootReversal.ToStringFast()];
        /// <summary>
        /// Performs the <c>ReadCanonicalFuturesItiSignalsAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractIds">The contract ids used by this operation.</param>
        /// <param name="startDate">The start date used by this operation.</param>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadCanonicalFuturesItiSignalsAsync(IReadOnlyCollection<string> contractIds, DateOnly startDate, DateOnly endDate)
        {
        var preparedParameters4 = new NativeItiRange(contractIds,startDate,endDate);
            return await context._dbFactory.MarketDataDb.Use("NativeIti.Range", MarketDataDbSql.GetNativeItiRange)
            .SetParameters(preparedParameters4)
            .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadFuturesItiSignalsByDateRangeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractIds">The contract ids used by this operation.</param>
        /// <param name="startDate">The start date used by this operation.</param>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiSignalsByDateRangeAsync(IReadOnlyCollection<string> contractIds, DateOnly startDate, DateOnly endDate)
        {
            return await context.ReadCanonicalFuturesItiSignalsAsync(contractIds,startDate,endDate)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadFuturesItiDayModeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <param name="afterSequenceId">The after sequence id used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiDayModeAsync(string contractId, DateOnly valueDate, IntrinsicTimeModeType intrinsicTimeMode, long? afterSequenceId = null, CancellationToken cancellationToken = default)
        {
        var preparedParameters5 = new NativeItiDayMode(contractId,valueDate,intrinsicTimeMode.ToStringFast(),afterSequenceId ?? long.MinValue);
            return await context._dbFactory.MarketDataDb.Use("NativeIti.DayMode", MarketDataDbSql.GetNativeItiDayMode)
            .SetParameters(preparedParameters5)
            .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!, cancellationToken)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the latest ordinary or overshoot result in one structural category, preserving sequence boundaries.
        /// </summary>
        internal async Task<FuturesItiSignalV2ReadModel?> ReadLastFuturesItiCategoryAsync(
            string contractId, DateOnly valueDate, IntrinsicTimeModeType ordinaryMode,
            IntrinsicTimeModeType overshootMode, long? afterSequenceId = null,
            CancellationToken cancellationToken = default)
        {
            var rows = await Task.WhenAll(
                context.ReadFuturesItiDayModeAsync(contractId, valueDate, ordinaryMode, afterSequenceId, cancellationToken),
                context.ReadFuturesItiDayModeAsync(contractId, valueDate, overshootMode, afterSequenceId, cancellationToken))
            .ConfigureAwait(false);
            return rows.SelectMany(static values => values).OrderByDescending(static row => row.SequenceId).FirstOrDefault();
        }

        /// <summary>
        /// Performs the <c>ReadLastFuturesItiTrendModeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<FuturesItiSignalV2ReadModel?> ReadLastFuturesItiTrendModeAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, IntrinsicTimeModeType intrinsicTimeMode, CancellationToken cancellationToken = default)
        {
        var preparedParameters6 = new NativeItiLatestTrendMode(contractId,valueDate,intrinsicTimeTrend.ToStringFast(),intrinsicTimeMode.ToStringFast());
            return await context._dbFactory.MarketDataDb.Use("NativeIti.LatestTrendMode", MarketDataDbSql.GetNativeItiLatestTrendMode)
            .SetParameters(preparedParameters6)
            .ExecuteSingleAsync(MarketDataDbContext.MapToFuturesItiSignal!, cancellationToken)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadLatestAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="seriesIdentity">The series identity used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="configurationId">The configuration id used by this operation.</param>
        /// <param name="cql">The cql used by this operation.</param>
        /// <param name="operation">The operation used by this operation.</param>
        /// <param name="map">The map used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <typeparam name="T">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<T?> ReadLatestAsync<T>(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, string configurationId, string cql, string operation, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken)
            where T : class
        {
            var endOfValueDateUtc = valueDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            var result = await context.ReadMonthAsync(seriesIdentity, valueDate, endOfValueDateUtc, configurationId, cql, operation, map, cancellationToken)
            .ConfigureAwait(false);
            if (result is not null)
                return result;
            var previousMonth = valueDate.AddMonths(-1);
            return await context.ReadMonthAsync(seriesIdentity, previousMonth, endOfValueDateUtc, configurationId, cql, operation, map, cancellationToken)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadMonthAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="seriesIdentity">The series identity used by this operation.</param>
        /// <param name="partitionMonth">The partition month used by this operation.</param>
        /// <param name="marketDataAsOf">The market data as of used by this operation.</param>
        /// <param name="configurationId">The configuration id used by this operation.</param>
        /// <param name="cql">The cql used by this operation.</param>
        /// <param name="operation">The operation used by this operation.</param>
        /// <param name="map">The map used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <param name="timeFrame">The time frame used by this operation.</param>
        /// <typeparam name="T">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task<T?> ReadMonthAsync<T>(MarketSeriesIdentity seriesIdentity, DateOnly partitionMonth, DateTime marketDataAsOf, string configurationId, string cql, string operation, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken, TimeFrameType timeFrame = TimeFrameType.Daily)
            where T : class
        {
        var preparedParameters7 = new GetLatestFuturesRegimeSignal(seriesIdentity.Format(), timeFrame.ToString(), configurationId, MarketDataDbContextExtensions.Bucket(partitionMonth), marketDataAsOf);
            return context._dbFactory.MarketDataDb.Use($"MarketAnalytics.{operation}", cql)
            .SetParameters(preparedParameters7)
            .ExecuteSingleAsync(map, cancellationToken);
        }
        /// <summary>
        /// Performs the <c>ReadObservationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="environment">The environment used by this operation.</param>
        /// <param name="id">The id used by this operation.</param>
        /// <param name="token">The token used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<OptionIvObservation?> ReadObservationAsync(string environment, string id, CancellationToken token) => await context.Database.Use("OptionVolatility.Observation.Id.Read", MarketDataDbSql.SelectObservationById)
            .SetParameters(new OptionVolatilityParameters([environment, id]))
            .ExecuteSingleAsync(MarketDataDbContext.MapToReadObservationRow1, token)
            .ConfigureAwait(false);
        /// <summary>
        /// Performs the <c>ReadLatestAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="scope">The scope used by this operation.</param>
        /// <param name="token">The token used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<OptionIvLatestPointer?> ReadLatestAsync(VolatilityStorageScope scope, CancellationToken token) => await context.Database.Use("OptionVolatility.Latest.Read", MarketDataDbSql.SelectLatest)
            .SetParameters(new OptionVolatilityParameters([scope.Environment, scope.Series.SeriesId, scope.Series.MethodologyVersion, scope.MetricPolicyVersion, ]))
            .ExecuteSingleAsync((row) => MarketDataDbContext.MapToReadLatestRow2(row, scope), token)
            .ConfigureAwait(false);
            /// <summary>Commits history, current snapshots, and lookup copies together without a business event append.</summary>
        /// <param name="rows">The rows used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal async Task PersistNativeFuturesEodBatchAsync(IReadOnlyList<BufferedFuturesEodRow> rows)
        {
            var latest = rows.GroupBy(row => (row.Snapshot.ContractId, row.Snapshot.ValueDate, row.Snapshot.Symbol))
                .Select(group => group.Last().Snapshot).ToArray();
            var history = rows.Where(row => row.AppendHistory).Select(row =>
            {
                var e = row.Snapshot;
                return new InsertFuturesIntraDayData(e.ContractId, e.ValueDate,
                    row.HistorySequenceId ?? throw new InvalidOperationException("History sequence must be allocated before persistence."),
                    e.Symbol, e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice, e.Volume,
                    e.DailyPercentChange, e.DailyStdDev, e.DailyStdDevAmount, e.UpperBand, e.Mean,
                    e.LowerBand, e.MarketDirection.ToStringFast(), e.MarketVolatility.ToStringFast(),
                    e.PriceDirection.ToStringFast(), e.PriceVolatility.ToStringFast(), e.MarketDirectionIndicator,
                    e.WindowSize);
            }).ToArray();
            var commands = context.CreateNativeFuturesEodCommands(latest);
            if (history.Length > 0)
                commands.Add(context.Use(nameof(MarketDataDbSql.InsertFuturesIntraDayData), MarketDataDbSql.InsertFuturesIntraDayData)
            .SetParameters(history)
            .QueueCommand());
            await context.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
            .ConfigureAwait(false);
        }


        /// <summary>Atomically imports current EOD values and their lookup copies.</summary>
        /// <param name="rows">The rows used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal Task InsertNativeFuturesEodBatchAsync(IReadOnlyList<FuturesEodDataV2ReadModel> rows)
        {
            return context.ExecuteQueuedCommandsAsync(context.CreateNativeFuturesEodCommands(rows), useTransaction: true);
        }


        /// <summary>Deletes both current EOD materializations atomically; retained observation history is unchanged.</summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal Task DeleteNativeFuturesEodAsync(string contractId, DateOnly valueDate)
        {
        var preparedParameters8 = new DeleteFuturesEodData(contractId, valueDate);
            List<object> commands =
            [
                context.Use(nameof(MarketDataDbSql.DeleteFuturesEodData), MarketDataDbSql.DeleteFuturesEodData)
            .SetParameters(preparedParameters8)
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.DeleteFuturesEodDataByMonth), MarketDataDbSql.DeleteFuturesEodDataByMonth)
            .SetParameters(new DeleteFuturesEodDataByMonth(MarketDataDbContextExtensions.ToYearMonth(valueDate), valueDate, contractId))
            .QueueCommand()
            ];
            return context.ExecuteQueuedCommandsAsync(commands, useTransaction: true);
        }


        /// <summary>Builds typed native commands; no CQL translation occurs at execution.</summary>
        /// <param name="rows">The rows used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        internal List<object> CreateNativeFuturesEodCommands(IReadOnlyList<FuturesEodDataV2ReadModel> rows)
        {
            return
            [
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodData), MarketDataDbSql.InsertFuturesEodData)
            .SetParameters(rows.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataParameters(e, e.OpenPrice)))
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodDataByMonth), MarketDataDbSql.InsertFuturesEodDataByMonth)
            .SetParameters(rows.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(e, e.OpenPrice)))
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodDataIndex), MarketDataDbSql.InsertFuturesEodDataIndex)
            .SetParameters(rows.Select(e => new InsertFuturesEodDataIndex(e.ValueDate, e.ContractId)).Distinct())
            .QueueCommand()
            ];
        }

        /// <summary>Serializes read-modify-write for one VX contract/date. Session statistics replace cumulative values;
        /// otherwise the tick updates high/low/close and adds its size exactly once per call.</summary>
        /// <param name="tick">The tick used by this operation.</param>
        /// <param name="statistics">The statistics used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal async Task InsertNativeVixTickAsync(VixTickObservation observation)
        {
            var transactions = new PostgresEventTransaction(new DbConnectionSettings()
                .Add(EventSourceActorDbContext.EventSourceActorDbConnection,context.ConnectionString,"System.Data.Postgres"));
            await transactions.ExecuteOnceAsync(async (transaction, token) =>
            {
                var value = await MarketDataDbContextExtensions.ResolveSnapshotAsync(transaction, observation, token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertVixFuturesEodData, [value.ContractId,value.ValueDate,value.OpenPrice,value.HighPrice,value.LowPrice,value.ClosePrice,value.Volume],token)
            .ConfigureAwait(false);
                return true;
            })
            .ConfigureAwait(false);
        }


        /// <summary>Validates and executes the bounded seed or evidence persistence operation.</summary>
        internal async Task<IReadOnlyList<FuturesTradeSessionBarReadModel>> ReadQualifiedIndicatorSeedsCoreAsync(string contractId,
            TimeFrameType frame, DateOnly valueDate, DateTimeOffset[] intervalEndsUtc, string sourceVersion,
            CancellationToken cancellationToken)
        {
            if (intervalEndsUtc.Length == 0) return [];
        var preparedParameters10 = new IndicatorSeedCoverageKey(contractId, frame, valueDate, intervalEndsUtc, sourceVersion);

            return (await context.Database.Use(nameof(MarketDataDbSql.GetQualifiedIndicatorSeedBars), MarketDataDbSql.GetQualifiedIndicatorSeedBars)
            .SetParameters(preparedParameters10)
            .ExecuteQueryAsync((row) => MarketDataDbContext.MapToReadQualifiedIndicatorSeedsCoreRow3(row, contractId, frame, valueDate), cancellationToken)
            .ConfigureAwait(false)).ToArray();
        }

        /// <summary>Validates and executes the bounded seed or evidence persistence operation.</summary>
        internal async Task StoreQualifiedIndicatorSeedsCoreAsync(string sourceVersion, FuturesTradeSessionBarReadModel[] bars,
            CancellationToken cancellationToken)
        {
            if (bars.Length == 0) return;
        var preparedParameters11 = new IndicatorSeedCoverageBatch(sourceVersion, bars);

            await context.Database.Use(nameof(MarketDataDbSql.StoreQualifiedIndicatorSeedBars), MarketDataDbSql.StoreQualifiedIndicatorSeedBars)
            .SetParameters(preparedParameters11)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        }


        /// <summary>Validates and executes the bounded seed or evidence persistence operation.</summary>
        internal async ValueTask<bool[]> WriteFirstBatchCoreAsync(IReadOnlyList<OptionTradeEvidence> evidence, CancellationToken cancellationToken)
        {
            if (evidence.Count == 1)
                return [await context.WriteFirstAsync(evidence[0], cancellationToken)
            .ConfigureAwait(false)];

            var serializer = MessagePackBinarySerializer.Shared;
            var requests = new (EvidenceKey Key, string Digest, byte[] Payload)[evidence.Count];
            for (var index = 0; index < evidence.Count; index++)
            {
                var item = evidence[index];
                requests[index] = (EvidenceKey.From(item.Source), item.Source.SourceDigest, serializer.Serialize(item)!);
            }

            // Same-key requests within a window follow the first submitted attempt.
            // The database statement orders different keys before taking write locks.
            var first = requests.DistinctBy(static request => request.Key).ToArray();
            var insert = new OptionEvidenceInsertBatchParameters(
                first.Select(static r => r.Key.Contract).ToArray(),
                first.Select(static r => r.Key.Date).ToArray(),
                first.Select(static r => r.Key.Identity).ToArray(),
                first.Select(static r => r.Digest).ToArray(),
                first.Select(static r => r.Payload).ToArray());
            var accepted = await context.Database.Use("OptionTradeEvidence.InsertBatch", MarketDataDbSql.InsertOptionTradeEvidenceBatch)
                .SetParameters(insert)
            .ExecuteQueryAsync(MarketDataDbContext.MapToEvidenceKey, cancellationToken)
            .ConfigureAwait(false);

            var inserted = accepted.ToHashSet();
            var durable = first.Where(r => inserted.Contains(r.Key)).ToDictionary(static r => r.Key, static r => r.Payload);
            var conflicts = first.Where(r => !inserted.Contains(r.Key)).Select(static r => r.Key).ToArray();
            if (conflicts.Length != 0)
            {
                // A concurrent insert can win the unique-key lock after the insertion
                // statement's snapshot. Read conflicts in a separate command.
                var selection = new OptionEvidenceBatchSelectionParameters(
                    conflicts.Select(static k => k.Contract).ToArray(),
                    conflicts.Select(static k => k.Date).ToArray(),
                    conflicts.Select(static k => k.Identity).ToArray());
                var stored = await context.Database.Use("OptionTradeEvidence.ReadConflicts", MarketDataDbSql.GetOptionTradeEvidenceBatch)
                    .SetParameters(selection)
            .ExecuteQueryAsync(MarketDataDbContext.MapToWriteFirstBatchCoreRow4, cancellationToken)
            .ConfigureAwait(false);
                foreach (var row in stored)
                    durable.Add(row.Key, serializer.Serialize(row.Value)!);
                if (stored.Count != conflicts.Length)
                    throw new InvalidOperationException("Durable option trade evidence write not confirmed.");
            }

            return requests.Select(r => durable[r.Key].AsSpan().SequenceEqual(r.Payload)).ToArray();
        }


            /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
        internal async ValueTask<bool> TryInsertOptionTradeEvidenceAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
        {
            var source = evidence.Source;
        var preparedParameters12 = new NativeOptionEvidenceWriteParameters(source.ContractId, source.ValueDate, source.Identity, source.SourceDigest, MessagePackBinarySerializer.Shared.Serialize(evidence)!);
            return await context.Database.Use($"{nameof(MarketDataDbSql)}.InsertOptionTradeEvidence", MarketDataDbSql.InsertOptionTradeEvidence)
            .SetParameters(preparedParameters12)
            .ExecuteSingleAsync(MarketDataDbContext.MapToTryInsertOptionTradeEvidenceRow5, cancellationToken)
            .ConfigureAwait(false);
        }
        /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
        internal async ValueTask<OptionTradeEvidence> ReadAcceptedOptionTradeEvidenceAsync(OptionTradeSource source, CancellationToken cancellationToken)
            => await context.ReadOptionTradeEvidenceCoreAsync(new(source.ContractId, source.ValueDate, source.Identity), cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Durable option trade evidence write not confirmed.");

        /// <summary>Reads an exact evidence row for a qualified query or write confirmation.</summary>
        internal async Task<OptionTradeEvidence?> ReadOptionTradeEvidenceCoreAsync(MarketDataDbContext.EvidenceKey key, CancellationToken cancellationToken)
        {
            var parameters = new NativeOptionEvidenceReadParameters(key.Contract, key.Date, key.Identity);
            var rows = await context.Database.Use($"{nameof(MarketDataDbSql)}.GetOptionTradeEvidence", MarketDataDbSql.GetOptionTradeEvidence)
                .SetParameters(parameters)
                .ExecuteQueryAsync(row => MarketDataDbContext.MapToOptionTradeEvidence(row, key), cancellationToken)
                .ConfigureAwait(false);
            return rows.SingleOrDefault();
        }
}

    extension(DateOnly valueDate)
    {
        /// <summary>
        /// Performs the <c>ToYearMonth</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int ToYearMonth() => checked(valueDate.Year * 100 + valueDate.Month);
        /// <summary>
        /// Performs the <c>GetFuturesEodScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesEodScopeKey() => MarketDataDbContextExtensions.GetFuturesEodScopeKey(MarketDataDbContextExtensions.ToYearMonth(valueDate));
        /// <summary>
        /// Performs the <c>GetFuturesItiCalendarBucketStart</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="period">The period used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateOnly GetFuturesItiCalendarBucketStart(TimeFrameType period) => period switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(-(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => valueDate,
        };
        /// <summary>
        /// Performs the <c>Bucket</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int Bucket() => (valueDate.Year * 100) + valueDate.Month;
    }

    extension(int yearMonth)
    {
        /// <summary>
        /// Performs the <c>GetMonthStart</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateOnly GetMonthStart() => new(yearMonth / 100, yearMonth % 100, 1);
        /// <summary>
        /// Performs the <c>GetMonthEnd</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateOnly GetMonthEnd()
        {
            var monthStart = MarketDataDbContextExtensions.GetMonthStart(yearMonth);
            if (monthStart.Year == DateOnly.MaxValue.Year && monthStart.Month == DateOnly.MaxValue.Month)
                return DateOnly.MaxValue;
            return monthStart.AddMonths(1).AddDays(-1);
        }

        /// <summary>
        /// Performs the <c>GetFuturesEodScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesEodScopeKey() => yearMonth.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    extension(FuturesTickDataV2ReadModel row)
    {
        /// <summary>
        /// Performs the <c>GetFuturesTickIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetFuturesTickIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.ContractId);
            hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
            hash = MarketDataProjectionHash.Add(hash, row.TickId);
            hash = MarketDataProjectionHash.Add(hash, row.TickTime);
            hash = MarketDataProjectionHash.Add(hash, row.Price);
            return MarketDataProjectionHash.Add(hash, row.Size);
        }
    }

    extension(FuturesEodDataV2ReadModel row)
    {
        /// <summary>
        /// Performs the <c>GetFuturesEodIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetFuturesEodIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.ContractId);
            hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
            hash = MarketDataProjectionHash.Add(hash, row.Symbol);
            hash = MarketDataProjectionHash.Add(hash, row.OpenPrice);
            hash = MarketDataProjectionHash.Add(hash, row.HighPrice);
            hash = MarketDataProjectionHash.Add(hash, row.LowPrice);
            hash = MarketDataProjectionHash.Add(hash, row.ClosePrice);
            hash = MarketDataProjectionHash.Add(hash, row.Volume);
            hash = MarketDataProjectionHash.Add(hash, row.DailyPercentChange);
            hash = MarketDataProjectionHash.Add(hash, row.DailyStdDev);
            hash = MarketDataProjectionHash.Add(hash, row.DailyStdDevAmount);
            hash = MarketDataProjectionHash.Add(hash, row.UpperBand);
            hash = MarketDataProjectionHash.Add(hash, row.Mean);
            hash = MarketDataProjectionHash.Add(hash, row.LowerBand);
            hash = MarketDataProjectionHash.Add(hash, (int)row.MarketDirection);
            hash = MarketDataProjectionHash.Add(hash, (int)row.MarketVolatility);
            hash = MarketDataProjectionHash.Add(hash, (int)row.PriceDirection);
            hash = MarketDataProjectionHash.Add(hash, (int)row.PriceVolatility);
            hash = MarketDataProjectionHash.Add(hash, row.MarketDirectionIndicator);
            hash = MarketDataProjectionHash.Add(hash, row.WindowSize);
            hash = MarketDataProjectionHash.Add(hash, row.FiftyDMA);
            return MarketDataProjectionHash.Add(hash, row.TwoHundredDMA);
        }
    }

    extension(string contractId)
    {
        /// <summary>
        /// Performs the <c>GetVixContractIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetVixContractIdentity() => MarketDataDbContextExtensions.GetVixContractIdentity(MarketDataDbContextExtensions.GetVixContractBucket(contractId), contractId);
        /// <summary>
        /// Performs the <c>GetVixContractBucket</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int GetVixContractBucket() => (int)(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), contractId) % MarketDataDbContext.VixContractBucketCount);
        /// <summary>
        /// Performs the <c>GetFuturesTickScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesTickScopeKey(DateOnly valueDate) => string.Concat(contractId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", contractId, ":", valueDate.DayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        /// <summary>
        /// Performs the <c>GetVixContractIndexScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetVixContractIndexScopeKey() => MarketDataDbContextExtensions.GetVixContractIndexScopeKey(MarketDataDbContextExtensions.GetVixContractBucket(contractId));
        /// <summary>
        /// Performs the <c>IsFuturesContractForSymbol</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="symbol">The symbol used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsFuturesContractForSymbol(string symbol)
        {
            if (!contractId.StartsWith(symbol, StringComparison.Ordinal))
                return false;
            var suffix = contractId.AsSpan(symbol.Length);
            if (suffix.Length == 8 && DateOnly.TryParseExact(suffix, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return true;
            return suffix.Length is >= 2 and <= 5 && "FGHJKMNQUVXZ".Contains(suffix[0], System.StringComparison.Ordinal) && suffix[1..].IndexOfAnyExceptInRange('0', '9') < 0;
        }

        /// <summary>
        /// Performs the <c>GetFuturesItiDayScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesItiDayScopeKey(DateOnly valueDate) => $"day:{contractId.Length}:{contractId}:{valueDate:yyyyMMdd}";
        /// <summary>
        /// Performs the <c>GetFuturesItiMonthScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="yearMonth">The year month used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesItiMonthScopeKey(int yearMonth) => $"month:{contractId.Length}:{contractId}:{yearMonth}";
        /// <summary>
        /// Performs the <c>GetFuturesItiTimelineScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <param name="yearMonth">The year month used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesItiTimelineScopeKey(string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth) => $"timeline:{contractId.Length}:{contractId}:{intrinsicTimeTrend.Length}:{intrinsicTimeTrend}:{intrinsicTimeMode.Length}:{intrinsicTimeMode}:{yearMonth}";
        /// <summary>
        /// Performs the <c>GetFuturesItiProjectionScopeKeys</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string[] GetFuturesItiProjectionScopeKeys(DateOnly valueDate, string intrinsicTimeTrend, string intrinsicTimeMode)
        {
            var yearMonth = MarketDataDbContextExtensions.ToYearMonth(valueDate);
            return[MarketDataDbContextExtensions.GetFuturesItiDayScopeKey(contractId, valueDate), MarketDataDbContextExtensions.GetFuturesItiMonthScopeKey(contractId, yearMonth), MarketDataDbContextExtensions.GetFuturesItiTimelineScopeKey(contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth), ];
        }
    }

    extension(int bucket)
    {
        /// <summary>
        /// Performs the <c>GetVixContractIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetVixContractIdentity(string contractId)
        {
            var hash = MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), bucket);
            return MarketDataProjectionHash.Add(hash, contractId);
        }

        /// <summary>
        /// Performs the <c>GetVixContractIndexScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetVixContractIndexScopeKey() => bucket.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    extension(string scopeKey)
    {
        /// <summary>
        /// Performs the <c>GetProjectionGuardScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetProjectionGuardScopeKey() => string.Concat(MarketDataDbContext.ProjectionGuardScopePrefix, (MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), scopeKey) % MarketDataDbContext.ProjectionGuardScopeCount).ToString(System.Globalization.CultureInfo.InvariantCulture));
        /// <summary>
        /// Performs the <c>IsProjectionGuardScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsProjectionGuardScopeKey() => scopeKey.StartsWith(MarketDataDbContext.ProjectionGuardScopePrefix, StringComparison.Ordinal);
    }

    extension(IEnumerable<string> scopeKeys)
    {
        /// <summary>
        /// Performs the <c>AddProjectionGuardScopes</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string[] AddProjectionGuardScopes()
        {
            var dataScopes = scopeKeys.Distinct(StringComparer.Ordinal).ToArray();
            return dataScopes.Concat(dataScopes.Where(static scope => !MarketDataDbContextExtensions.IsProjectionGuardScopeKey(scope)).Select(MarketDataDbContextExtensions.GetProjectionGuardScopeKey)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
    }

    extension(IObjectRepository db)
    {
    }

    extension(TickProjectionGuardFailureStage stage)
    {
        /// <summary>
        /// Performs the <c>IsTickGuardFailureAutomaticallyRecoverable</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsTickGuardFailureAutomaticallyRecoverable() => stage is TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission or TickProjectionGuardFailureStage.AfterDataAcknowledged;
    }

    extension(FuturesEodDataV2ReadModel e)
    {
        /// <summary>
        /// Performs the <c>CreateFuturesEodDataParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="openPrice">The open price used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesEodData CreateFuturesEodDataParameters(decimal openPrice) => new(contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA);
        /// <summary>
        /// Performs the <c>CreateFuturesEodDataByMonthParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="openPrice">The open price used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesEodDataByMonth CreateFuturesEodDataByMonthParameters(decimal openPrice) => new(yearMonth: MarketDataDbContextExtensions.ToYearMonth(e.ValueDate), contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA);
    }

    extension(IObjectDataRecord row)
    {
        /// <summary>
        /// Performs the <c>GetNullableString</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="index">The index used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string? GetNullableString(int index) => row.IsNull(index) ? null : row.GetString(index);
    }

    extension(DateTime value)
    {
        /// <summary>
        /// Performs the <c>NormalizeEconomicCalendarTimestamp</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateTime NormalizeEconomicCalendarTimestamp() => MarketDataImportBatch.NormalizeCalendarTimestamp(value);

        /// <summary>
        /// Performs the <c>EconomicCalendarMonthBucket</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int EconomicCalendarMonthBucket() => value.Year * 100 + value.Month;
        /// <summary>
        /// Performs the <c>Utc</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateTimeOffset Utc() => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    extension(DateTime startDate)
    {
        /// <summary>
        /// Performs the <c>EconomicCalendarMonthBucketsDescending</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal IEnumerable<int> EconomicCalendarMonthBucketsDescending(DateTime endDate)
        {
            var firstMonth = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var month = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc); month >= firstMonth; month = month.AddMonths(-1))
                yield return MarketDataDbContextExtensions.EconomicCalendarMonthBucket(month);
        }
    }

    extension(EconomicCalendarReadModel row)
    {
        /// <summary>
        /// Performs the <c>IsAfterCursor</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="cursor">The cursor used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsAfterCursor(CalendarPageToken cursor) => cursor.LastEventDateTicks is null || row.EventDate.Ticks < cursor.LastEventDateTicks.Value || (row.EventDate.Ticks == cursor.LastEventDateTicks.Value && string.CompareOrdinal(row.EventName, cursor.LastEventName) > 0);
        /// <summary>
        /// Performs the <c>GetEconomicCalendarProjectionIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetEconomicCalendarProjectionIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.EventDate.Ticks);
            hash = MarketDataProjectionHash.Add(hash, row.CountryCode);
            hash = MarketDataProjectionHash.Add(hash, row.EventName);
            hash = MarketDataProjectionHash.Add(hash, row.Actual);
            hash = MarketDataProjectionHash.Add(hash, row.Forecast);
            hash = MarketDataProjectionHash.Add(hash, row.Prior);
            hash = MarketDataProjectionHash.Add(hash, row.Impact);
            hash = MarketDataProjectionHash.Add(hash, row.Unit);
            hash = MarketDataProjectionHash.Add(hash, row.Change);
            hash = MarketDataProjectionHash.Add(hash, row.ChangePercentage);
            hash = MarketDataProjectionHash.Add(hash, row.CreatedOn.Ticks);
            return MarketDataProjectionHash.Add(hash, row.CreatedBy);
        }
    }

    extension(EconomicCalendarPageRequest request)
    {
        /// <summary>
        /// Performs the <c>GetPageRequestFingerprint</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="countries">The countries used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetPageRequestFingerprint(string[] countries)
        {
            var identity = $"v1|{request.StartDateUtc.Ticks}|{request.EndDateUtc.Ticks}|{string.Join(',', countries)}|{request.PageSize}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        }
    }

    extension(CalendarPageToken token)
    {
        /// <summary>
        /// Performs the <c>EncodePageToken</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string EncodePageToken()
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(token);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }

    extension(string? token)
    {
        /// <summary>
        /// Performs the <c>DecodePageToken</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="fingerprint">The fingerprint used by this operation.</param>
        /// <param name="partitionCount">The partition count used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal CalendarPageToken DecodePageToken(string fingerprint, int partitionCount)
        {
            if (string.IsNullOrEmpty(token))
                return new CalendarPageToken(fingerprint, 0, null, null);
            try
            {
                var value = token.Replace('-', '+').Replace('_', '/');
                value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
                var cursor = JsonSerializer.Deserialize<CalendarPageToken>(Convert.FromBase64String(value)) ?? throw new FormatException();
                if (!string.Equals(cursor.Fingerprint, fingerprint, StringComparison.Ordinal) || cursor.PartitionIndex < 0 || cursor.PartitionIndex > partitionCount || cursor.LastEventName?.Any(char.IsControl) == true)
                    throw new FormatException();
                return cursor;
            }
            catch (Exception ex)when (ex is FormatException or JsonException)
            {
                throw new ArgumentException("The economic-calendar continuation token is invalid for this request.", nameof(token), ex);
            }
        }
    }

    extension<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord
    {
        /// <summary>
        /// Performs the <c>MapJsonPayload</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        internal string MapJsonPayload() => row.GetString(0);
    }

    extension(JsonElement root)
    {
        /// <summary>
        /// Performs the <c>GetString</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="name">The name used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetString(string name)
        {
            if (!MarketDataDbContextExtensions.TryGetProperty(root, name, out var property) || property.ValueKind == JsonValueKind.Null)
                return string.Empty;
            return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
        }

        /// <summary>
        /// Performs the <c>GetInt64</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="name">The name used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal long GetInt64(string name)
        {
            if (!MarketDataDbContextExtensions.TryGetProperty(root, name, out var property))
                return -1;
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
                return number;
            return long.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : -1;
        }

        /// <summary>
        /// Performs the <c>TryGetProperty</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="name">The name used by this operation.</param>
        /// <param name="value">The value used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool TryGetProperty(string name, out JsonElement value)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }

    extension(FuturesItiSignalV2ReadModel row)
    {
        /// <summary>
        /// Performs the <c>GetFuturesItiSignalIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetFuturesItiSignalIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.ContractId);
            hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
            hash = MarketDataProjectionHash.Add(hash, row.TimePeriod.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.SequenceId);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTime.Ticks);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeGroupId);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeLength);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicPrice);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeTrend.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeMode.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.TrendPrice);
            hash = MarketDataProjectionHash.Add(hash, row.TrendExtreme);
            hash = MarketDataProjectionHash.Add(hash, row.TrendReversal);
            hash = MarketDataProjectionHash.Add(hash, row.TrendDelta);
            hash = MarketDataProjectionHash.Add(hash, row.TargetDelta);
            hash = MarketDataProjectionHash.Add(hash, row.Lambda);
            hash = MarketDataProjectionHash.Add(hash, row.TradingDays);
            hash = MarketDataProjectionHash.Add(hash, row.Threshold);
            hash = MarketDataProjectionHash.Add(hash, row.UpTrendTrigger);
            hash = MarketDataProjectionHash.Add(hash, row.DownTrendTrigger);
            hash = MarketDataProjectionHash.Add(hash, row.TradeState.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.BandLevel);
            return MarketDataProjectionHash.Add(hash, row.ReversalLevel);
        }

        /// <summary>
        /// Performs the <c>ToFuturesItiSignalMdi</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal FuturesItiSignalMDIV2ReadModel ToFuturesItiSignalMdi() => new(contractId: row.ContractId, valueDate: row.ValueDate, intrinsicTime: row.IntrinsicTime, trendType: row.IntrinsicTimeTrend, mdi: row.IntrinsicPrice);
    }

    extension(FuturesItiSignalV2ReadModel e)
    {
        /// <summary>
        /// Performs the <c>CreateFuturesItiSignalParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="sequenceId">The sequence id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesItiSignal CreateFuturesItiSignalParameters(long sequenceId) => new(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandLevel, e.ReversalLevel);
        /// <summary>
        /// Performs the <c>CreateFuturesItiSignalMonthParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="sequenceId">The sequence id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesItiSignalByContractMonth CreateFuturesItiSignalMonthParameters(long sequenceId) => new(MarketDataDbContextExtensions.ToYearMonth(e.ValueDate), e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandLevel, e.ReversalLevel);
        /// <summary>
        /// Performs the <c>CreateFuturesItiTimeFrameStateParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="sequenceId">The sequence id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Zero is the explicit missing-value sentinel in the persisted pricing contract; approximate comparison would classify valid nonzero values as missing.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal UpsertFuturesItiTimeFrameState CreateFuturesItiTimeFrameStateParameters(long sequenceId) => new(e.ContractId, e.TimePeriod.ToStringFast(), MarketDataDbContextExtensions.GetFuturesItiCalendarBucketStart(e.ValueDate, e.TimePeriod), e.TimeFrameStartValueDate == default ? e.ValueDate : e.TimeFrameStartValueDate, e.ValueDate, sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandAnchorPrice == 0 ? e.IntrinsicPrice : e.BandAnchorPrice, e.BandPercentage == 0 ? 0.20 : e.BandPercentage, e.BandSize == 0 ? e.Threshold * 0.20 : e.BandSize, e.BandLevel, e.ReversalLevel);
    }

    extension(IObjectDataRecord value)
    {
        /// <summary>
        /// Performs the <c>Decimal</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="index">The index used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal decimal? Decimal(int index) => value.IsNull(index) ? null : value.GetDecimal(index);
    }

    extension(OptionIvLatestPointer pointer)
    {
        /// <summary>
        /// Performs the <c>LatestValues</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionVolatilityParameters LatestValues() => new([pointer.Scope.Environment, pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion, pointer.Scope.MetricPolicyVersion, pointer.SnapshotId, pointer.SnapshotDigest, pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime, ]);
        /// <summary>
        /// Performs the <c>AdvanceLatestValues</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionVolatilityParameters AdvanceLatestValues() => new([pointer.SnapshotId, pointer.SnapshotDigest, pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime, pointer.Scope.Environment, pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion, pointer.Scope.MetricPolicyVersion, ]);
    }

    extension(VolatilityHistoryPageRequest request)
    {
        /// <summary>
        /// Performs the <c>HistoryValues</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="includePolicy">The include policy used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionVolatilityParameters HistoryValues(bool includePolicy) => includePolicy ? new([request.Scope.Environment, request.Scope.Series.SeriesId, request.Scope.Series.MethodologyVersion, request.Scope.MetricPolicyVersion, request.Bucket.Value, request.FromValueDate, request.ToValueDate, ]) : new([request.Scope.Environment, request.Scope.Series.SeriesId, request.Scope.Series.MethodologyVersion, request.Bucket.Value, request.FromValueDate, request.ToValueDate, ]);
    }

    extension(OptionIvPublication publication)
    {
        /// <summary>
        /// Performs the <c>Pointer</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionIvLatestPointer Pointer()
        {
            var snapshot = publication.Metric.Snapshot;
            return new(new(publication.Environment, snapshot.Series, snapshot.MetricPolicyVersion), snapshot.SnapshotId, snapshot.SnapshotDigest, publication.Metric.PublicationSequence, snapshot.AvailableAtUtc);
        }

    }

    extension<T>(T value)
    {
        /// <summary>
        /// Performs the <c>Serialize</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        internal byte[] Serialize()
        {
            var payload = MessagePackBinarySerializer.Shared.Serialize(value) ?? throw new InvalidDataException("Option volatility serialization returned no payload.");
            if (payload.Length is 0 or > MarketDataDbContext.MaximumPayloadBytes)
                throw new InvalidDataException("Option volatility payload exceeds its bounded size.");
            return payload;
        }
    }

    extension(byte[] payload)
    {
        /// <summary>
        /// Performs the <c>Deserialize</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <typeparam name="T">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal T Deserialize<T>()
        {
            if (payload.Length is 0 or > MarketDataDbContext.MaximumPayloadBytes)
                throw new InvalidDataException("Invalid option volatility payload size.");
            return MessagePackBinarySerializer.Shared.Deserialize<T>(payload) ?? throw new InvalidDataException("Missing option volatility payload.");
        }
    }

    extension(DateOnly value)
    {
        /// <summary>
        /// Performs the <c>YearMonth</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int YearMonth() => checked(value.Year * 100 + value.Month);
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Locks the canonical and indexed tables for the caller's offline baseline transaction.</summary>
        public Task LockMarketDataBaselineAsync(CancellationToken token)
            => transaction.ExecuteAsync(MarketDataDbSql.LockMarketDataBaseline, [], token);

        /// <summary>Checks for a committed baseline stream without allocating metadata.</summary>
        public async Task<bool> HasMarketDataBaselineAsync(string stream, CancellationToken token)
            => await transaction.ScalarAsync(MarketDataDbSql.HasMarketDataBaseline, (object? [])new MarketDataBaselineStream(stream).Bind(), token)
            .ConfigureAwait(false) is not null;

        /// <summary>Reads canonical calendar rows under the selected baseline lock.</summary>
        public Task<IReadOnlyList<EconomicCalendarReadModel>> ReadBaselineEconomicCalendarAsync(CancellationToken token)
            => transaction.QueryAsync(MarketDataDbSql.ReadBaselineEconomicCalendar, [], MarketDataDbContext.MapToBaselineEconomicCalendar, token);

        /// <summary>Reads canonical or indexed Treasury rows as requested by the reconciliation owner.</summary>
        public Task<IReadOnlyList<YieldCurveRateReadModel>> ReadBaselineYieldCurvesAsync(bool indexed, CancellationToken token)
        {
            var query = indexed ? MarketDataDbSql.GetIndexedYieldCurvesForRebuild : MarketDataDbSql.GetCanonicalYieldCurvesForRebuild;
            return transaction.QueryAsync(query, [], MarketDataDbContext.MapToBaselineYieldCurve, token);
        }

        /// <summary>Reads committed heads for the supplied command-stream prefix.</summary>
        public Task<IReadOnlyList<TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamRevisionReadModel>> ReadMarketDataBaselineHeadsAsync(string prefix, CancellationToken token)
            => transaction.QueryAsync(MarketDataDbSql.ReadMarketDataBaselineHeads, (object? [])new MarketDataBaselineStream(prefix).Bind(), MarketDataDbContext.MapToBaselineStreamHead, token);


        /// <summary>Checks the usable indexes for the canonical PostgreSQL query paths.</summary>
        /// <param name="token">Cancels the database query.</param>
        /// <returns>Readiness of PostgreSQL queries, with raw tick ownership explicitly excluded.</returns>
        internal async Task<MarketDataProjectionReadinessReadModel> ReadCanonicalQueryReadinessAsync(CancellationToken token)
        {
            var rows = await transaction.QueryAsync(MarketDataDbSql.GetCanonicalQueryReadiness, [], MarketDataDbContext.MapToReadCanonicalQueryReadinessRow6, token)
            .ConfigureAwait(false);
            return rows.Single();
        }

        /// <summary>Applies the owned index statements and returns exact physical inventory rows.</summary>
        internal async Task<IReadOnlyList<MarketDataProjectionInventoryRow>> RefreshCanonicalQueryIndexRowsAsync(CancellationToken token)
        {
            await transaction.ExecuteAsync(Schema.MarketDataSchemaSql.RebuildCanonicalQueryIndexes, [], token)
                .ConfigureAwait(false);
            return await transaction.QueryAsync(MarketDataDbSql.GetCanonicalQueryInventory, [],
                MarketDataDbContext.MapToProjectionInventory, token)
                .ConfigureAwait(false);
        }

    /// <summary>Resolves exact replacement facts while holding the owning transaction's contract/date lock.</summary>
    /// <param name="tick">The tick used by this operation.</param>
    /// <param name="statistics">The statistics used by this operation.</param>
    /// <param name="token">The token used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<VixFuturesEodDataReadModel> ResolveSnapshotAsync(VixTickObservation observation, CancellationToken token)
    {
        var tick = observation.Tick;
        var statistics = observation.Statistics;
        var key = string.Create(CultureInfo.InvariantCulture,$"market_data.vix:{tick.ContractId}:{tick.ValueDate:yyyyMMdd}");
        await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,[key],token)
            .ConfigureAwait(false);
        var rows = await transaction.QueryAsync(MarketDataDbSql.GetVixFuturesEodData, [tick.ContractId, tick.ValueDate], MarketDataDbContext.MapToResolveSnapshotRow8, token)
            .ConfigureAwait(false);
        var old = rows.Count == 0 ? null : rows[0];
        var hasPrices = statistics is { HasPriceStatistics: true };
        var open = hasPrices ? statistics!.Value.OpenPrice : old?.OpenPrice ?? tick.Price;
        var high = hasPrices ? statistics!.Value.HighPrice : Math.Max(old?.HighPrice ?? tick.Price,tick.Price);
        var low = hasPrices ? statistics!.Value.LowPrice : Math.Min(old?.LowPrice ?? tick.Price,tick.Price);
        var volume = statistics is { HasVolume: true } ? statistics.Value.Volume : checked((old?.Volume ?? 0) + tick.Size);
        return new(tick.ContractId,tick.ValueDate,open,high,low,tick.Price,volume);
    }


        /// <summary>Writes accepted calendar rows and country lookups with command ownership checks.</summary>
        /// <param name="batch">The caller-qualified import rows and policy.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task ImportEconomicCalendarsAsync(MarketDataImportBatch<EconomicCalendarReadModel> batch, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            foreach (var row in batch.Rows)
            {
                var date = MarketDataImportBatch.NormalizeCalendarTimestamp(row.EventDate);
                var month = MarketDataDbContextExtensions.EconomicCalendarMonthBucket(date);
                var key = string.Create(CultureInfo.InvariantCulture,$"economic-calendar:{row.CountryCode.Length}:{row.CountryCode}:{date.Ticks}:{row.EventName}");
                await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,[key],token)
            .ConfigureAwait(false);
                if (batch.Policy == ImportDuplicatePolicy.Reject)
                {
                    var owners = await transaction.QueryAsync(MarketDataDbSql.GetEconomicCalendarV2CommandId, (object? [])new GetEconomicCalendarV2CommandId(row.CountryCode, month, date, row.EventName).Bind(), MarketDataDbContext.MapToImportEconomicCalendarsRow9, token)
            .ConfigureAwait(false);
                    if (owners.Count != 0 && owners[0] != batch.CommandId)
                        throw new MarketDataImportDuplicateException("Economic-calendar event is owned by another import command.");
                }
                var values = new InsertEconomicCalendarV2(row.CountryCode,month,date,row.EventName,row.Actual,row.Forecast,row.Prior,row.Impact,row.Unit,row.Change,row.ChangePercentage,
                    MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.CreatedOn),row.CreatedBy,batch.CommandId);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertEconomicCalendarV2,(object?[])values.Bind(),token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertEconomicCalendarCountryCode, (object?[])new InsertEconomicCalendarCountryCode(MarketDataDbContext.EconomicCalendarLookupId,row.CountryCode).Bind(),token)
            .ConfigureAwait(false);
            }
        }

        /// <summary>Copies canonical Treasury rows to their date and year indexes under the original lock.</summary>
        internal async Task<YieldCurveLookupInventory> RefreshYieldCurveLookupRowsAsync(CancellationToken token)
        {
            await transaction.ExecuteAsync(MarketDataDbSql.LockYieldCurveIndexes, [], token)
                .ConfigureAwait(false);
            var source = await transaction.QueryAsync(MarketDataDbSql.GetCanonicalYieldCurvesForRebuild, [],
                MarketDataDbContext.MapToYieldCurveDbRow, token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.ClearYieldCurveDateIndex, [], token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.ClearYieldCurveYearIndex, [], token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.RebuildYieldCurveDateIndex, [], token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.RebuildYieldCurveYearIndex, [], token)
                .ConfigureAwait(false);
            var target = await transaction.QueryAsync(MarketDataDbSql.GetIndexedYieldCurvesForRebuild, [],
                MarketDataDbContext.MapToYieldCurveDbRow, token)
                .ConfigureAwait(false);
            var years = await transaction.QueryAsync(MarketDataDbSql.GetYieldCurveIndexYears, [],
                MarketDataDbContext.MapToRebuildYieldCurveIndexesRow10, token)
                .ConfigureAwait(false);
            return new(source, target, years);
        }

        /// <summary>Writes accepted Treasury curves and their indexes while enforcing import ownership.</summary>
        /// <param name="batch">The caller-qualified import rows and policy.</param>
        /// <param name="acceptedAtUtc">The acceptedAtUtc used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task ImportYieldCurvesAsync(MarketDataImportBatch<YieldCurveRateReadModel> batch, DateTime acceptedAtUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            // Stable lock order includes empty dates and serializes overwrite imports with reject-policy imports.
            foreach (var row in batch.Rows)
            {
                var key = row.ValueDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
                await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,["market_data.treasury-curve:" + key],token)
            .ConfigureAwait(false);
                if (batch.Policy == ImportDuplicatePolicy.Reject)
                {
                    var owners = await transaction.QueryAsync(MarketDataDbSql.ReadMarketDataImportOwnership, ["treasury-curve", key], MarketDataDbContext.MapToImportYieldCurvesRow11, token)
            .ConfigureAwait(false);
                    if (owners.Count > 0)
                    {
                        if (owners[0].CommandId != batch.CommandId || !owners[0].MayWrite)
                            throw new MarketDataImportDuplicateException($"Treasury curve {key} is owned by another import command.");
                    }
                    else
                    {
                        var exists = (bool)(await transaction.ScalarAsync(MarketDataDbSql.YieldCurveExists, [row.ValueDate], token)
            .ConfigureAwait(false))!;
                        if (exists) throw new MarketDataImportDuplicateException($"Treasury curve {key} already exists.");
                        await transaction.ExecuteAsync(MarketDataDbSql.InsertMarketDataImportOwnership, [Text("treasury-curve"),Text(key),Uuid(batch.CommandId),Boolean(true),TimestampTz(acceptedAtUtc)],token)
            .ConfigureAwait(false);
                    }
                }
                var values = new InsertYieldCurveRate(1,row.ValueDate,row.OneMonth,row.TwoMonth,row.ThreeMonth,row.SixMonth,row.OneYear,
                    row.TwoYear,row.ThreeYear,row.FiveYear,row.SevenYear,row.TenYear,row.TwentyYear,row.ThirtyYear);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRate,(object?[])values.Bind(),token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRateByDate,(object?[])values.Bind(),token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRateYear,[Integer(1),Integer(row.ValueDate.Year)],token)
            .ConfigureAwait(false);
            }
        }

        /// <summary>Preserves immutable option-chain parameter versions and advances the current revision.</summary>
        /// <param name="parameters">The parameters used by the persistence operation.</param>
        /// <param name="revision">The revision used by the persistence operation.</param>
        /// <param name="published">The published used by the persistence operation.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistOptionChainParametersAsync(StrategyOptionChainParameterSet parameters,
            long revision, bool published, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            await transaction.WriteTypedValueAsync(TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterSetValue.From(parameters),cancellationToken)
            .ConfigureAwait(false);
            var payload=parameters.Hash();
            await transaction.ExecuteAsync(MarketDataDbSql.InsertOptionChainParameterVersion, Values(Uuid(parameters.ParameterSetId), Integer(parameters.Version), Text(payload)), cancellationToken)
            .ConfigureAwait(false);
            var stored = await transaction.ScalarAsync(MarketDataDbSql.GetOptionChainParameterVersion, Values(Uuid(parameters.ParameterSetId), Integer(parameters.Version)), cancellationToken)
            .ConfigureAwait(false);
            if (stored is not string content || !string.Equals(content, parameters.Hash(), StringComparison.Ordinal))
                throw new InvalidDataException("An immutable option-chain parameter version has conflicting content.");
            await transaction.ExecuteAsync(MarketDataDbSql.UpsertOptionChainParameterCurrent, Values(Text(parameters.Environment), Uuid(parameters.ParameterSetId), Integer(parameters.Version), Bigint(revision), Boolean(published), Text(payload)), cancellationToken)
            .ConfigureAwait(false);
        }

        /// <summary>Appends immutable volatility observation evidence and its dated history row.</summary>
        /// <param name="environment">The environment used by the persistence operation.</param>
        /// <param name="observation">The observation used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task AppendOptionIvObservationAsync(string environment, OptionIvObservation observation, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (observation.Revision > 1)
            {
                var previous = await transaction.QueryAsync(MarketDataDbSql.SelectObservationById, transaction.BindOptionVolatilityValues(environment, observation.SupersedesObservationId!), MarketDataDbContext.MapToAppendOptionIvObservationRow12, token)
            .ConfigureAwait(false);
                if (previous.Count == 0 || previous[0].Series != observation.Series || previous[0].ExchangeValueDate != observation.ExchangeValueDate
                    || !string.Equals(previous[0].SamplingSlot,observation.SamplingSlot,StringComparison.Ordinal) || previous[0].Revision >= observation.Revision)
                    throw new InvalidOperationException("An observation revision must supersede accepted immutable evidence.");
            }
            var payload = MarketDataDbContextExtensions.Serialize(observation);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertObservationById,transaction.BindOptionVolatilityValues(environment,observation.ObservationId,payload),token)
            .ConfigureAwait(false);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertObservationHistory,transaction.BindOptionVolatilityValues(environment,observation.Series.SeriesId,observation.Series.MethodologyVersion,
                VolatilityCalendarBucket.From(observation.ExchangeValueDate).Value,observation.ExchangeValueDate,observation.SamplingSlot,observation.Revision,observation.ObservationId,observation.AvailableAtUtc.UtcDateTime,payload),token)
            .ConfigureAwait(false);
        }

        /// <summary>Persists accepted volatility evidence, metric history and the monotonic latest pointer atomically.</summary>
        /// <param name="publication">The publication used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PublishOptionIvAsync(OptionIvPublication publication, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var metric = publication.Metric;
            var snapshot = metric.Snapshot;
            var scope = string.Join(":",publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion);
            await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,["option-volatility:"+scope],token)
            .ConfigureAwait(false);
            foreach(var observation in publication.SourceObservations.OrderBy(row => row.Revision).ThenBy(row => row.ObservationId,StringComparer.Ordinal))
                await MarketDataDbContextExtensions.AppendOptionIvObservationAsync(transaction,publication.Environment,observation,token)
            .ConfigureAwait(false);
            if (metric.Revision > 1)
            {
                var previous = await transaction.QueryAsync(MarketDataDbSql.SelectSnapshot, transaction.BindOptionVolatilityValues(publication.Environment, metric.SupersedesSnapshotId!), MarketDataDbContext.MapToPublishOptionIvRow13, token)
            .ConfigureAwait(false);
                if (previous.Count == 0 || previous[0].Snapshot.Series != snapshot.Series
                    || !string.Equals(previous[0].Snapshot.MetricPolicyVersion,snapshot.MetricPolicyVersion,StringComparison.Ordinal) || previous[0].Revision >= metric.Revision)
                    throw new InvalidOperationException("A metric revision must supersede accepted evidence in the same policy.");
            }
            var payload = MarketDataDbContextExtensions.Serialize(metric);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertSnapshot,transaction.BindOptionVolatilityValues(publication.Environment,snapshot.SnapshotId,snapshot.SnapshotDigest,metric.PublicationSequence,payload),token)
            .ConfigureAwait(false);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertMetricHistory,transaction.BindOptionVolatilityValues(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion,
                VolatilityCalendarBucket.From(snapshot.ExchangeValueDate).Value,snapshot.ExchangeValueDate,snapshot.SamplingSlot,metric.Revision,snapshot.SnapshotId,snapshot.AvailableAtUtc.UtcDateTime,metric.PublicationSequence,payload),token)
            .ConfigureAwait(false);
            var pointer = MarketDataDbContextExtensions.Pointer(publication);
            var existing = await transaction.QueryAsync(MarketDataDbSql.SelectLatest, transaction.BindOptionVolatilityValues(publication.Environment, snapshot.Series.SeriesId, snapshot.Series.MethodologyVersion, snapshot.MetricPolicyVersion), MarketDataDbContext.MapToPublishOptionIvRow14, token)
            .ConfigureAwait(false);
            if (existing.Count != 0 && existing[0].Sequence == metric.PublicationSequence
                && (!string.Equals(existing[0].Id,snapshot.SnapshotId,StringComparison.Ordinal) || !string.Equals(existing[0].Digest,snapshot.SnapshotDigest,StringComparison.Ordinal) || existing[0].Available != pointer.AvailableAtUtc.UtcDateTime))
                throw new InvalidOperationException("Publication sequence conflicts with a different latest pointer.");
            await transaction.ExecuteAsync(MarketDataDbSql.InsertLatest,transaction.BindOptionVolatilityValues(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion, snapshot.SnapshotId,snapshot.SnapshotDigest,metric.PublicationSequence,pointer.AvailableAtUtc.UtcDateTime),token)
            .ConfigureAwait(false);
        }

        /// <summary>Reads a bounded page of observation revisions eligible for the requested point in time.</summary>
        /// <param name="request">The request used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public Task<VolatilityPage<OptionIvObservation>> ReadOptionIvObservationHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken token = default)
            => MarketDataDbContextExtensions.ReadOptionIvHistoryAsync(transaction,request,false,MarketDataDbContextExtensions.Deserialize<OptionIvObservation>,token);

        /// <summary>Reads a bounded page of metric revisions eligible for the requested point in time.</summary>
        /// <param name="request">The request used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public Task<VolatilityPage<OptionIvMetricRevision>> ReadOptionIvMetricHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken token = default)
            => MarketDataDbContextExtensions.ReadOptionIvHistoryAsync(transaction,request,true,MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>,token);

        /// <summary>Reads immutable volatility history using a query-bound continuation cursor.</summary>
        /// <param name="request">The request used by the persistence operation.</param>
        /// <param name="metric">The metric used by the persistence operation.</param>
        /// <param name="decode">The decode used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <typeparam name="T">The persisted evidence type.</typeparam>
        /// <returns>The persistence operation result.</returns>
        internal async Task<VolatilityPage<T>> ReadOptionIvHistoryAsync<T>(VolatilityHistoryPageRequest request, bool metric, Func<byte[],T> decode, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { PagingState = null, PageSize = 0 })));
            VolatilityHistoryCursor? cursor = null;
            if (request.PagingState is not null)
            {
                try { cursor = JsonSerializer.Deserialize<VolatilityHistoryCursor>(request.PagingState); }
                catch (JsonException exception) { throw new ArgumentException("Invalid volatility history cursor.",nameof(request),exception); }
                if (cursor is null || !string.Equals(cursor.Fingerprint,fingerprint,StringComparison.Ordinal) || cursor.Metric != metric)
                    throw new ArgumentException("Volatility history cursor belongs to a different query.",nameof(request));
            }
            List<object?> values = [Text(request.Scope.Environment),Text(request.Scope.Series.SeriesId),Text(request.Scope.Series.MethodologyVersion)];
            if (metric) values.Add(Text(request.Scope.MetricPolicyVersion));
            values.AddRange([Integer(request.Bucket.Value),Date(request.FromValueDate),Date(request.ToValueDate),Text(request.SamplingSlot),TimestampTz(request.Mode == VolatilityHistoricalMode.AsKnown ? request.KnownAtUtc!.Value.UtcDateTime : null),Date(cursor?.Date),Text(cursor?.Slot),Integer(request.PageSize + 1)]);
            // Table and column fragments are fixed by the repository, never supplied by the caller.
            var sql = metric ? MarketDataDbSql.GetOptionIvMetricHistory : MarketDataDbSql.GetOptionIvObservationHistory;
            var rows = await transaction.QueryAsync(sql, [..values], MarketDataDbContext.MapToVolatilityHistoryRow, token)
            .ConfigureAwait(false);
            var visible = rows.Take(request.PageSize).ToArray();
            var next = rows.Count > request.PageSize ? JsonSerializer.SerializeToUtf8Bytes(new VolatilityHistoryCursor(fingerprint,metric,visible[^1].Date,visible[^1].Slot)) : null;
            return new(visible.Select(row => decode(row.Payload)).ToImmutableArray(),next);
        }

        /// <summary>Accepts identical volatility evidence and rejects conflicting facts for the same identity.</summary>
        /// <param name="sql">The sql used by the persistence operation.</param>
        /// <param name="values">The values used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        internal async Task WriteImmutableVolatilityEvidenceAsync(string sql, object?[] values, CancellationToken token)
        {
            if (await transaction.ScalarAsync(sql, values, token)
            .ConfigureAwait(false) is null)
                throw new InvalidOperationException("Volatility evidence identity conflicts with different immutable facts.");
        }

        /// <summary>Locks the source and lookup tables and returns the exact raw rows to qualify.</summary>
        internal async Task<IReadOnlyList<string>> ReadLockedTradeSignalPayloadsAsync(CancellationToken token)
        {
            await transaction.ExecuteAsync(MarketDataDbSql.LockTradeSignalIndexes, [], token)
                .ConfigureAwait(false);
            return await transaction.QueryAsync(MarketDataDbSql.ReadTradeSignalRepairRows, [],
                MarketDataDbContext.MapToRebuildTradeSignalIndexesRow15, token)
                .ConfigureAwait(false);
        }

        /// <summary>Persists a prepared quarantine row without deleting its source.</summary>
        internal Task StoreTradeSignalQuarantineRowAsync(FuturesTradeSignalQuarantineRow row, CancellationToken token)
        {
            var values = new InsertFuturesTradeSignalQuarantine(row.Fingerprint, row.Payload, row.Reason, row.ObservedAtUtc);
            return transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalQuarantine, (object?[])values.Bind(), token);
        }

        /// <summary>Replaces lookup rows with the exact owner-prepared winners.</summary>
        internal async Task ReplaceTradeSignalLookupRowsAsync(IReadOnlyList<FuturesTradeSignalLookupRow> rows, CancellationToken token)
        {
            await transaction.ExecuteAsync(MarketDataDbSql.ClearTradeSignalLookup, [], token)
                .ConfigureAwait(false);
            foreach (var row in rows)
            {
                var values = new InsertFuturesTradeSignalIndex(row.Key, row.Scope, row.SequenceId, row.ContractId,
                    row.ValueDate, row.TimePeriod);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex, (object?[])values.Bind(), token)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>Writes the accepted live-feed enabled state.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistTradeLiveFeedAsync(TradeLiveFeedTurnedOnEvent source,CancellationToken token)
        {
            var parameters = new InsertTradeLiveFeed(source.OrderId,source.TradeId,TradeLiveFeedStateType.On.ToStringFast());
            await transaction.ExecuteAsync(MarketDataDbSql.InsertTradeLiveFeed,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Removes the accepted live-feed subscription state.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task DeleteTradeLiveFeedAsync(TradeLiveFeedTurnedOffEvent source,CancellationToken token)
        {
            var parameters = new DeleteTradeLiveFeed(source.OrderId,source.TradeId);
            await transaction.ExecuteAsync(MarketDataDbSql.DeleteTradeLiveFeed,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Persists the raw EOD facts carried by the accepted source event.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedRawEodAsync(FuturesEodDataInsertedEvent source,CancellationToken token)
        {
            var value = source.RawObservation ?? throw new InvalidOperationException("This older EOD source lacks accepted session facts; use a verified baseline.");
            var parameters = new HistoricalRawEodParameter(value);
            _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertRawEod, (object? [])parameters.Bind(), token)
            .ConfigureAwait(false);
        }

        /// <summary>Persists the resolved VX snapshot carried by the accepted source event.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedVixEodAsync(VixFuturesEodDataInsertedEvent source,CancellationToken token)
        {
            var value = source.AcceptedSnapshot ?? throw new InvalidOperationException("This older VX source lacks accepted replacement facts; use a verified baseline.");
            var parameters = new InsertVixFuturesEodData(value.ContractId,value.ValueDate,value.OpenPrice,value.HighPrice,value.LowPrice,value.ClosePrice,value.Volume);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertVixFuturesEodData,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes an accepted futures bar.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesBarAsync(FuturesBarDataInsertedEvent source,CancellationToken token)
        {
            var value = source.FuturesBarData;
            var parameters = new InsertFuturesBarData(value.ContractId,value.Symbol,value.ValueDate,value.BarDate,value.BarRateType.ToStringFast(),value.BarValue,value.UpTrendTrigger,value.DownTrendTrigger);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesBarData,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Deletes the identified futures bar.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task DeleteAcceptedFuturesBarAsync(FuturesBarDataDeletedEvent source,CancellationToken token)
        {
            var value = source.BarDataId;
            var parameters = new DeleteFuturesBarData(value.ContractId,value.Symbol,value.ValueDate);
            await transaction.ExecuteAsync(MarketDataDbSql.DeleteFuturesBarData,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes an accepted session bar and its supplied daily EOD observation.</summary>
        /// <param name="bar">The bar used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <param name="eod">The accepted daily observation, or null when only a session bar is written.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistSessionBarFactsAsync(FuturesTradeSessionBarReadModel bar, FuturesEodObservationReadModel? eod, CancellationToken token)
        {
            var parameters = new HistoricalObservationParameter(bar);
            _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertObservation, (object? [])parameters.Bind(), token)
            .ConfigureAwait(false);
            if (eod is null) return;
            var raw = new HistoricalRawEodParameter(eod);
            _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertRawEod, (object? [])raw.Bind(), token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes the accepted dated closing price.</summary>
        /// <param name="accepted">The accepted used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedClosingPriceAsync(FuturesClosingPriceInsertedEvent accepted, CancellationToken token)
        {
            var closing = new InsertFuturesClosingPrice(accepted.FuturesClosingPriceId.ContractId, accepted.FuturesClosingPriceId.ValueDate,
                accepted.ClosingPrice, accepted.CreatedOn, accepted.CreatedBy);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesClosingPrice,(object?[])closing.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes the accepted outlook snapshot with its source-event revision.</summary>
        /// <param name="accepted">The accepted used by the persistence operation.</param>
        /// <param name="eventId">The eventId used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedMarketOutlookAsync(MarketOutlookSnapshotInsertedEvent accepted, long eventId, CancellationToken token)
        {
            var snapshot = accepted.MarketOutlook;
            var parameters = new UpsertMarketOutlookSnapshot(snapshot.ContractId, snapshot.ValueDate, eventId, snapshot.UpdatedAtUtc,
                MessagePackSerializer.Serialize(snapshot.FuturesEodData,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token),
                snapshot.FuturesTradeSignal is null ? null : MessagePackSerializer.Serialize(snapshot.FuturesTradeSignal,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token),
                snapshot.MissingInputs,MessagePackSerializer.Serialize(snapshot,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token));
            await transaction.ExecuteAsync(MarketDataDbSql.UpsertMarketOutlookSnapshot,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Adx signal values without recalculating them.</summary>
        /// <param name="futuresAdxSignal">The futuresAdxSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesAdxSignalAsync(FuturesAdxSignalReadModel futuresAdxSignal, CancellationToken token)
        {
            var parameters = new InsertFuturesAdxSignal(contractId: futuresAdxSignal.ContractId, valueDate: futuresAdxSignal.ValueDate, timePeriod: futuresAdxSignal.TimePeriod.ToStringFast(), periodLength: futuresAdxSignal.PeriodLength, timestamp: futuresAdxSignal.Timestamp, futuresPrice: futuresAdxSignal.FuturesPrice, plusDI: futuresAdxSignal.PlusDI, minusDI: futuresAdxSignal.MinusDI, adxValue: futuresAdxSignal.AdxValue, adx: futuresAdxSignal.ADX.ToStringFast(), adxStrength: futuresAdxSignal.ADXStrength.ToStringFast(), configurationId: futuresAdxSignal.Metadata?.CalculationConfigurationId, observationId: futuresAdxSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAdxSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAdxSignal.Metadata?.SourceSequence, calculationVersion: futuresAdxSignal.Metadata?.CalculationVersion, calculationMethod: futuresAdxSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAdxSignal.Metadata is { } metadata ? metadata.SchemaVersion : null, isValid: futuresAdxSignal.Metadata?.IsValid, isWarm: futuresAdxSignal.IsWarm) { IncludeWarmState = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesAdxSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Atr signal values without recalculating them.</summary>
        /// <param name="futuresAtrSignal">The futuresAtrSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesAtrSignalAsync(FuturesAtrSignalReadModel futuresAtrSignal, CancellationToken token)
        {
            var parameters = new InsertFuturesAtrSignal(contractId: futuresAtrSignal.ContractId, valueDate: futuresAtrSignal.ValueDate, timePeriod: futuresAtrSignal.TimePeriod.ToStringFast(), periodLength: futuresAtrSignal.PeriodLength, timestamp: futuresAtrSignal.Timestamp, futuresPrice: futuresAtrSignal.FuturesPrice, atrValue: futuresAtrSignal.AtrValue, trueRange: futuresAtrSignal.TrueRange, atr: futuresAtrSignal.ATR.ToStringFast(), atrStrength: futuresAtrSignal.ATRStrength.ToStringFast(), configurationId: futuresAtrSignal.Metadata?.CalculationConfigurationId, observationId: futuresAtrSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAtrSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAtrSignal.Metadata?.SourceSequence, calculationVersion: futuresAtrSignal.Metadata?.CalculationVersion, calculationMethod: futuresAtrSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAtrSignal.Metadata is { } atrMetadata ? atrMetadata.SchemaVersion : null, isValid: futuresAtrSignal.Metadata?.IsValid, previousAtrValue: futuresAtrSignal.PreviousAtrValue, atrBaseline: futuresAtrSignal.AtrBaseline, atrRatio: futuresAtrSignal.AtrRatio, isWarm: futuresAtrSignal.IsWarm);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesAtrSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Macd signal values without recalculating them.</summary>
        /// <param name="futuresMacdSignal">The futuresMacdSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesMacdSignalAsync(FuturesMacdSignalReadModel futuresMacdSignal, CancellationToken token)
        {
            var parameters = new InsertFuturesMacdSignal(contractId: futuresMacdSignal.ContractId, valueDate: futuresMacdSignal.ValueDate, timePeriod: futuresMacdSignal.TimePeriod.ToStringFast(), signalEmaPeriod: futuresMacdSignal.SignalEmaPeriod, fastEmaPeriod: futuresMacdSignal.FastEmaPeriod, slowEmaPeriod: futuresMacdSignal.SlowEmaPeriod, timestamp: futuresMacdSignal.Timestamp, futuresPrice: futuresMacdSignal.FuturesPrice, fastEma: futuresMacdSignal.FastEma, slowEma: futuresMacdSignal.SlowEma, macdLine: futuresMacdSignal.MacdLine, signalLine: futuresMacdSignal.SignalLine, histogram: futuresMacdSignal.Histogram, macd: futuresMacdSignal.MACD.ToStringFast(), macdStrength: futuresMacdSignal.MACDStrength.ToStringFast(), configurationId: futuresMacdSignal.Metadata?.CalculationConfigurationId, observationId: futuresMacdSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresMacdSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresMacdSignal.Metadata?.SourceSequence, calculationVersion: futuresMacdSignal.Metadata?.CalculationVersion, calculationMethod: futuresMacdSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresMacdSignal.Metadata is { } macdMetadata ? macdMetadata.SchemaVersion : null, isValid: futuresMacdSignal.Metadata?.IsValid, isWarm: futuresMacdSignal.IsWarm, observationCount: futuresMacdSignal.ObservationCount);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesMacdSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Rsi signal values without recalculating them.</summary>
        /// <param name="e">The e used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesRsiSignalAsync(FuturesRsiSignalReadModel e, CancellationToken token)
        {
            var parameters = new InsertFuturesRsiSignal(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), e.PeriodLength, e.Timestamp, e.Price, e.PriceChange, e.PriceGain, e.PriceLoss, e.AveragePriceGain, e.AveragePriceLoss, e.RS, e.RSI, e.RSIAverage, e.RSISlope, e.SourceSequence, e.SourceEventTimestamp, e.Metadata?.CalculationConfigurationId, e.Metadata?.ObservationId.Value, e.Metadata?.MarketDataAsOfUtc.UtcDateTime, e.Metadata?.CalculationVersion, e.Metadata?.CalculationMethod.ToString(), e.Metadata is { } rsiMetadata ? rsiMetadata.SchemaVersion : null, e.Metadata?.IsValid, e.PreviousRsi, e.RegimeSlope, e.IsWarm);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesRsiSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Tdi signal values without recalculating them.</summary>
        /// <param name="futuresTdiSignal">The futuresTdiSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesTdiSignalAsync(FuturesTdiSignalReadModel futuresTdiSignal, CancellationToken token)
        {
            var parameters = new InsertFuturesTdiSignal(contractId: futuresTdiSignal.ContractId, timePeriod: futuresTdiSignal.TimePeriod.ToStringFast(), configurationId: futuresTdiSignal.ConfigurationId, valueDate: futuresTdiSignal.ValueDate, timestamp: futuresTdiSignal.Timestamp, schemaVersion: futuresTdiSignal.SchemaVersion, rsiPeriod: futuresTdiSignal.RsiPeriod, priceLinePeriod: futuresTdiSignal.PriceLinePeriod, signalLinePeriod: futuresTdiSignal.SignalLinePeriod, marketBasePeriod: futuresTdiSignal.MarketBasePeriod, volatilityBandPeriod: futuresTdiSignal.VolatilityBandPeriod, volatilityBandDeviation: futuresTdiSignal.VolatilityBandDeviation, price: futuresTdiSignal.Price, rsi: futuresTdiSignal.Rsi, priceLine: futuresTdiSignal.PriceLine, signalLine: futuresTdiSignal.SignalLine, marketBaseLine: futuresTdiSignal.MarketBaseLine, upperVolatilityBand: futuresTdiSignal.UpperVolatilityBand, lowerVolatilityBand: futuresTdiSignal.LowerVolatilityBand, bandWidth: futuresTdiSignal.BandWidth, priceSignalDivergence: futuresTdiSignal.PriceSignalDivergence, crossType: futuresTdiSignal.Cross.ToString(), marketState: futuresTdiSignal.MarketState.ToString(), trendDirection: futuresTdiSignal.TDI.ToStringFast(), trendStrength: futuresTdiSignal.TDIStrength.ToStringFast(), sourceSequence: futuresTdiSignal.SourceSequence, sourceEventTimestamp: futuresTdiSignal.SourceEventTimestamp);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTdiSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Ema signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesEmaSignalAsync(FuturesEmaSignalReadModel signal, CancellationToken token)
        {
            var metadata = signal.Metadata;
            var parameters = new InsertFuturesEmaSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10, signal.PreviousEma10, signal.Ema10Slope, signal.Ema20, signal.PreviousEma20, signal.Ema20Slope, signal.Ema50, signal.PreviousEma50, signal.Ema50Slope, signal.Ema200, signal.PreviousEma200, signal.Ema200Slope, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesEmaSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Bb signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesBbSignalAsync(FuturesBbSignalReadModel signal, CancellationToken token)
        {
            var metadata = signal.Metadata;
            var parameters = new InsertFuturesBollingerBandSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10Center, signal.StandardDeviation10, signal.Upper10, signal.Lower10, signal.Width10, signal.Position10, signal.Ema20Center, signal.StandardDeviation20, signal.Upper20, signal.Lower20, signal.Width20, signal.Position20, signal.Width20Baseline, signal.Width20Ratio, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesBollingerBandSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Vwap signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesVwapSignalAsync(FuturesVwapSignalReadModel signal, CancellationToken token)
        {
            var parameters = new InsertFuturesVwapSignal(signal.ContractId, signal.ValueDate, signal.ConfigurationId, signal.AsOfUtc.UtcDateTime, signal.LastTradeOrdinal, signal.SessionStartUtc.UtcDateTime, signal.SessionEndUtc.UtcDateTime, signal.CumulativePriceVolume, signal.CumulativeVolume, signal.EligibleTradeCount, signal.RejectedTradeCount, signal.LastPrice, signal.Vwap, signal.PriceMinusVwap, signal.PriceToVwapPercent, signal.LastTradeSourceSequence, signal.StreamEpochId, signal.IsWarm, signal.IsValid, signal.InvalidReason.ToString(), signal.IsTickExact, signal.CalculationMethod.ToString(), signal.SchemaVersion, signal.CalculationVersion);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesVwapSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted VxTermStructure signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesVxTermStructureSignalAsync(FuturesVxTermStructureSignalReadModel signal, CancellationToken token)
        {
            var parameters = new InsertFuturesVxTermStructureSignal(signal.ValueDate, signal.ConfigurationId, signal.CalculatedAtUtc.UtcDateTime, signal.FrontSourceSequence, signal.BackSourceSequence, signal.FrontVxContractId, signal.FrontExpiry, signal.FrontVxPrice, signal.BackVxContractId, signal.BackExpiry, signal.BackVxPrice, signal.FrontBackSpread, signal.FrontBackRatio, signal.TermStructurePercent, signal.TermStructureState.ToString(), signal.PriorFrontBackRatio, signal.PriorTermStructurePercent, signal.FrontSourceTimestampUtc.UtcDateTime, signal.BackSourceTimestampUtc.UtcDateTime, signal.IsWarm, signal.IsValid, signal.SchemaVersion, signal.CalculationVersion);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesVxTermStructureSignal,(object?[])parameters.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted trade-signal facts and their current/date lookups.</summary>
        /// <param name="FuturesTradeSignalV2ReadModel">The FuturesTradeSignalV2ReadModel used by the persistence operation.</param>
        /// <param name="eventId">The eventId used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel FuturesTradeSignalV2ReadModel, long eventId, CancellationToken token)
        {
            var sequenceId = eventId;
            var timePeriod = FuturesTradeSignalV2ReadModel.TimePeriod.ToStringFast();
            var parameters0 = new InsertFuturesTradeSignal(contractId: FuturesTradeSignalV2ReadModel.ContractId, valueDate: FuturesTradeSignalV2ReadModel.ValueDate, timePeriod, sequenceId, timestamp: FuturesTradeSignalV2ReadModel.Timestamp, mean: FuturesTradeSignalV2ReadModel.Mean, stdDev: FuturesTradeSignalV2ReadModel.StdDev, futuresPrice: FuturesTradeSignalV2ReadModel.FuturesPrice, priceChangePercent: FuturesTradeSignalV2ReadModel.PriceChangePercent, fundRiskPercent: FuturesTradeSignalV2ReadModel.FundRiskPercent, rsi: FuturesTradeSignalV2ReadModel.RSI, rsiSlope: FuturesTradeSignalV2ReadModel.RSISlope, trendType: FuturesTradeSignalV2ReadModel.TrendType.ToStringFast(), trendStrength: FuturesTradeSignalV2ReadModel.TrendStrength.ToStringFast(), tradeSignal: FuturesTradeSignalV2ReadModel.TradeSignal.ToStringFast(), tdi: FuturesTradeSignalV2ReadModel.TDI.ToStringFast(), tdiStrength: FuturesTradeSignalV2ReadModel.TDIStrength.ToStringFast(), mdi: FuturesTradeSignalV2ReadModel.MDI, mdiTrend: FuturesTradeSignalV2ReadModel.MDITrend.ToStringFast(), mdiUpTrendLimit: FuturesTradeSignalV2ReadModel.MDIUpTrendLimit, mdiDownTrendLimit: FuturesTradeSignalV2ReadModel.MDIDownTrendLimit, upTrendingTrigger: FuturesTradeSignalV2ReadModel.UpTrendingTrigger, downTrendingTrigger: FuturesTradeSignalV2ReadModel.DownTrendingTrigger, entryTrigger: FuturesTradeSignalV2ReadModel.EntryTrigger, exitTrigger: FuturesTradeSignalV2ReadModel.ExitTrigger, trendDelta: FuturesTradeSignalV2ReadModel.TrendDelta, trendExtreme: FuturesTradeSignalV2ReadModel.TrendExtreme, trendReversal: FuturesTradeSignalV2ReadModel.TrendReversal, fiftyDma: FuturesTradeSignalV2ReadModel.FiftyDMA, twoHundredDma: FuturesTradeSignalV2ReadModel.TwoHundredDMA, tradeExecuteState: FuturesTradeSignalV2ReadModel.TradeExecuteState.ToStringFast());
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignal,(object?[])parameters0.Bind(),token)
            .ConfigureAwait(false);
            var parameters1 = new InsertFuturesTradeSignalIndex($"latest:{timePeriod}", "latest", sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(object?[])parameters1.Bind(),token)
            .ConfigureAwait(false);
            var parameters2 = new InsertFuturesTradeSignalIndex($"date:{timePeriod}:{FuturesTradeSignalV2ReadModel.ValueDate.DayNumber}", FuturesTradeSignalV2ReadModel.ContractId, sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(object?[])parameters2.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Writes accepted ITI signal facts and the current time-frame state.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="eventId">The eventId used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesItiSignalAsync(FuturesItiSignalV2ReadModel signal, long eventId, CancellationToken token)
        {
            var sequenceId = signal.SequenceId > 0 ? signal.SequenceId : eventId;
            var canonical = MarketDataDbContextExtensions.CreateFuturesItiSignalParameters(signal,sequenceId);
            var latest = MarketDataDbContextExtensions.CreateFuturesItiTimeFrameStateParameters(signal,sequenceId);
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesItiSignal,(object?[])canonical.Bind(),token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.UpsertFuturesItiTimeFrameState,(object?[])latest.Bind(),token)
            .ConfigureAwait(false);
        }

        /// <summary>Binds immutable volatility evidence values to PostgreSQL parameters.</summary>
        /// <param name="items">The ordered database values.</param>
        /// <returns>The typed PostgreSQL parameters.</returns>
        internal object?[] BindOptionVolatilityValues(params object?[] items)
            => (object?[])new OptionVolatilityParameters(items).Bind();
}


}

/// <summary>Opaque economic-calendar continuation state bound to the originating query.</summary>
/// <param name="Fingerprint">The query fingerprint used to reject tokens from other requests.</param>
/// <param name="PartitionIndex">The current country/month partition in query order.</param>
/// <param name="LastEventDateTicks">The last emitted event timestamp, or null before reading the partition.</param>
/// <param name="LastEventName">The last emitted event name used to disambiguate equal timestamps.</param>
internal sealed record CalendarPageToken(
    string Fingerprint,
    int PartitionIndex,
    long? LastEventDateTicks,
    string? LastEventName);

/// <summary>Identifies one country/month partition of the economic-calendar query.</summary>
/// <param name="CountryCode">The partition's country code.</param>
/// <param name="MonthBucket">The partition's year/month bucket.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CalendarPartition(string CountryCode, int MonthBucket);

/// <summary>Materialized ITI identity used to identify a persistence projection scope.</summary>
/// <param name="ContractId">The futures contract identifier.</param>
/// <param name="ValueDate">The observation's value date.</param>
/// <param name="IntrinsicTimeTrend">The persisted intrinsic-time trend.</param>
/// <param name="IntrinsicTimeMode">The persisted intrinsic-time mode.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct FuturesItiProjectionScopeData(
    string ContractId,
    DateOnly ValueDate,
    string IntrinsicTimeTrend,
    string IntrinsicTimeMode);

/// <summary>Stored ownership and write permission for an idempotent market-data import.</summary>
/// <param name="CommandId">The command that owns the persisted identity.</param>
/// <param name="MayWrite">Whether that owner may persist the requested facts.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct MarketDataImportOwnership(Guid CommandId, bool MayWrite);

/// <summary>Query-bound position in an immutable volatility evidence history page.</summary>
/// <param name="Fingerprint">The originating query fingerprint.</param>
/// <param name="Metric">Whether the page contains metric rather than observation evidence.</param>
/// <param name="Date">The last emitted value date.</param>
/// <param name="Slot">The last emitted sampling slot.</param>
internal sealed record VolatilityHistoryCursor(string Fingerprint, bool Metric, DateOnly Date, string Slot);
