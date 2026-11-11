using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;

using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Persists accepted Market Data projections and immutable evidence.</summary>
public interface IMarketDataDbWriteContext : IOptionTradeEvidenceWriter,
    TomasAI.IFM.Application.MarketData.Contracts.IStrategyOptionChainParameterWriteStore
{
    /// <summary>Applies the canonical index statements and returns the resulting physical inventory.</summary>
    /// <param name="transaction">The existing PostgreSQL maintenance transaction.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The indexed row identities.</returns>
    Task<IReadOnlyList<MarketDataProjectionInventoryRow>> RefreshCanonicalQueryIndexRowsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token);

    /// <summary>Reads index readiness under the supplied transaction.</summary>
    /// <param name="transaction">The existing PostgreSQL maintenance transaction.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The physical readiness flags.</returns>
    Task<MarketDataProjectionReadinessReadModel> ReadCanonicalQueryReadinessAsync(IEnlistedPostgresTransaction transaction, CancellationToken token);

    /// <summary>Refreshes the Treasury date and year lookups.</summary>
    /// <param name="transaction">The existing PostgreSQL maintenance transaction.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The canonical and copied row sets for owner verification.</returns>
    Task<YieldCurveLookupInventory> RefreshYieldCurveLookupRowsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token);

    /// <summary>Locks and reads raw trade signal rows for owner qualification.</summary>
    /// <param name="transaction">The existing PostgreSQL maintenance transaction.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The unmodified stored signal payloads.</returns>
    Task<IReadOnlyList<string>> ReadLockedTradeSignalPayloadsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token);

    /// <summary>Writes a prepared quarantine row without deleting the source.</summary>
    /// <param name="transaction">The existing PostgreSQL maintenance transaction.</param>
    /// <param name="row">The owner-qualified malformed signal and its identity.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the idempotent write.</returns>
    Task StoreTradeSignalQuarantineRowAsync(IEnlistedPostgresTransaction transaction, FuturesTradeSignalQuarantineRow row, CancellationToken token);

    /// <summary>Replaces the derived lookup rows with the supplied exact winners.</summary>
    /// <param name="transaction">The existing PostgreSQL maintenance transaction.</param>
    /// <param name="rows">The owner-prepared lookup rows.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the atomic replacement.</returns>
    Task ReplaceTradeSignalLookupRowsAsync(IEnlistedPostgresTransaction transaction, IReadOnlyList<FuturesTradeSignalLookupRow> rows, CancellationToken token);

    /// <summary>Atomically retains independently qualified seed intervals for the same contract, date and timeframe.</summary>
    /// <param name="sourceVersion">The source version supplied by the caller.</param>
    /// <param name="bars">The bars supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task StoreQualifiedIndicatorSeedBarsAsync(string sourceVersion, FuturesTradeSessionBarReadModel[] bars, CancellationToken cancellationToken);


    /// <summary>Materializes a committed download outcome on the caller-owned source-event transaction.</summary>
    /// <param name="transaction">The transaction used by this operation.</param>
    /// <param name="outcome">The outcome used by this operation.</param>
    /// <param name="logCommandId">The log command id used by this operation.</param>
    /// <param name="payloadSha256">The payload sha256 used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task ProjectDownloadLogAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        MarketDataDownloadOutcome outcome, Guid logCommandId, string payloadSha256, CancellationToken cancellationToken = default);

    /// <summary>Persist Realtime Futures Eod Batch using the MarketDataDb persistence.</summary>
    /// <param name="rows">The rows used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task PersistRealtimeFuturesEodBatchAsync(IReadOnlyList<BufferedFuturesEodRow> rows);
    /// <summary>Persists the latest immutable VX snapshot per contract/date.</summary>
    /// <param name="rows">The rows used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task PersistRealtimeVixEodBatchAsync(IReadOnlyList<VixFuturesEodDataReadModel> rows);

    /// <summary>Commit using the MarketDataDb persistence.</summary>
    /// <param name="proposed">The proposed used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<CompositionPreparation> CommitAsync(
        CompositionPreparation proposed,
        CancellationToken cancellationToken);
    /// <summary>Write using the MarketDataDb persistence.</summary>
    /// <param name="evidence">The evidence used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    new ValueTask WriteAsync(
        OptionTradeEvidence evidence,
        CancellationToken cancellationToken);
    /// <summary>Try Write Observation using the MarketDataDb persistence.</summary>
    /// <param name="observation">The observation used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    ValueTask<bool> TryWriteObservationAsync(
        FuturesTradeSessionBarReadModel observation,
        CancellationToken cancellationToken);
    /// <summary>Try Write Raw Eod using the MarketDataDb persistence.</summary>
    /// <param name="observation">The observation used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    ValueTask<bool> TryWriteRawEodAsync(
        FuturesEodObservationReadModel observation,
        CancellationToken cancellationToken);
    /// <summary>Append Observation using the MarketDataDb persistence.</summary>
    /// <param name="environment">The environment used by this operation.</param>
    /// <param name="observation">The observation used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task AppendObservationAsync(
        string environment,
        OptionIvObservation observation,
        CancellationToken cancellationToken = default);
    /// <summary>Publish using the MarketDataDb persistence.</summary>
    /// <param name="publication">The publication used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task PublishAsync(
        OptionIvPublication publication,
        CancellationToken cancellationToken = default);

    /// <summary>Insert Market Data Download Log using the MarketDataDb persistence.</summary>
    /// <param name="outcome">The outcome used by this operation.</param>
    /// <param name="logCommandId">The log command id used by this operation.</param>
    /// <param name="payloadSha256">The payload sha256 used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertMarketDataDownloadLogAsync(
        MarketDataDownloadOutcome outcome,
        Guid logCommandId,
        string payloadSha256,
        CancellationToken cancellationToken = default);

    /// <summary>Persists one exact or explicitly invalid VWAP session projection.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesVwapSignalAsync(FuturesVwapSignalReadModel signal,
        CancellationToken cancellationToken = default);
    /// <summary>Delete Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteEconomicCalendarAsync(EconomicCalendarId id);
    /// <summary>Insert Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="economicCalendar">The economic calendar used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar);
    /// <summary>Persists a caller-qualified batch of EconomicCalendarReadModel rows.</summary>
    /// <param name="batch">The accepted rows, duplicate policy and source command identity.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task InsertEconomicCalendarsAsync(MarketDataImportBatch<EconomicCalendarReadModel> batch);
    /// <summary>Update Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <param name="economicCalendar">The economic calendar used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar);
    /// <summary>Insert Tick Trade Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent e);
    /// <summary>Insert Tick Quote Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e);
    /// <summary>Delete Futures Bar Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesBarDataAsync(FuturesBarDataId e);
    /// <summary>Delete Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesEodDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Delete Futures Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Delete Vix Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Delete Yield Curve Rate using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteYieldCurveRateAsync(DateOnly valueDate);
    /// <summary>Delete Market Holiday using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteMarketHolidayAsync(MarketHolidayReadModel e);
    /// <summary>Delete Market Holidays using the MarketDataDb persistence.</summary>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteMarketHolidaysAsync(CurrencyType currencyType);
    /// <summary>Delete Rate Of Return using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteRateOfReturnAsync(string symbol, DateOnly valueDate);
    /// <summary>Delete Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod);
    /// <summary>Delete Futures Option Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Delete Futures Option Tick Price Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Delete Futures Closing Price using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesClosingPriceAsync(string contractId, DateOnly valueDate);

    /// <summary>Insert Futures Bar Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesBarDataAsync(FuturesBarDataReadModel e);
    /// <summary>Insert Futures Bar Data using the MarketDataDb persistence.</summary>
    /// <param name="futuresBarData">The futures bar data used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesBarDataAsync(ICollection<FuturesBarDataReadModel> futuresBarData);
    /// <summary>Insert Futures Bar Data using the MarketDataDb persistence.</summary>
    /// <param name="futuresBarData">The futures bar data used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<long> InsertFuturesBarDataAsync(IEnumerable<FuturesBarDataReadModel> futuresBarData);
    /// <summary>Insert Futures Closing Price using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel e);
    /// <summary>Insert Futures Eod Data Index using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesEodDataIndexAsync(FuturesEodDataIndexReadModel e);
    /// <summary>Insert Futures Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel e);
    /// <summary>Insert Futures Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> e);
    /// <summary>Insert Futures Option Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel e);
    /// <summary>Insert Futures Option Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> e);
    /// <summary>Upsert Futures Option Chain Quote Data using the MarketDataDb persistence.</summary>
    /// <param name="underlyingContractId">The underlying contract id used by this operation.</param>
    /// <param name="tick">The tick used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task UpsertFuturesOptionChainQuoteDataAsync(string underlyingContractId, FuturesOptionTickDataV2ReadModel tick);
    /// <summary>Update Futures Option Chain Quote Greeks using the MarketDataDb persistence.</summary>
    /// <param name="underlyingContractId">The underlying contract id used by this operation.</param>
    /// <param name="expiryDate">The expiry date used by this operation.</param>
    /// <param name="quotes">The quotes used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task UpdateFuturesOptionChainQuoteGreeksAsync(string underlyingContractId, DateOnly expiryDate, ICollection<FuturesOptionTickDataV2ReadModel> quotes);
    /// <summary>Insert Futures Option Tick Price Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesOptionTickPriceDataAsync(FuturesOptionTickDataV2ReadModel e);
    /// <summary>Insert Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesItiSignalAsync(FuturesItiSignalV2ReadModel e);
    /// <summary>Insert Futures Rsi Signal using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel e);
    /// <summary>Persists one EMA10/20/50/200 signal projection.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesEmaSignalAsync(FuturesEmaSignalReadModel signal, CancellationToken cancellationToken = default);
    /// <summary>Persists one EMA-centered BB10/20 signal projection.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesBollingerBandSignalAsync(FuturesBbSignalReadModel signal, CancellationToken cancellationToken = default);
    /// <summary>Persists one valid front/back VX term-structure projection.</summary>
    /// <param name="signal">The signal used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesVxTermStructureSignalAsync(FuturesVxTermStructureSignalReadModel signal,
        CancellationToken cancellationToken = default);
    /// <summary>Persists one extended ATR14 volatility signal projection.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel e);
    /// <summary>Insert Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesMacdSignalAsync(FuturesMacdSignalReadModel e);
    /// <summary>Insert Futures Atr Signal using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesAtrSignalAsync(FuturesAtrSignalReadModel e);
    /// <summary>Delete Futures Atr Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    /// <summary>Insert Futures Adx Signal using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesAdxSignalAsync(FuturesAdxSignalReadModel e);
    /// <summary>Delete Futures Adx Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    /// <summary>Delete Trade Live Feed using the MarketDataDb persistence.</summary>
    /// <param name="orderid">The orderid used by this operation.</param>
    /// <param name="tradeId">The trade id used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task DeleteTradeLiveFeedAsync(int orderid, int tradeId);
    /// <summary>Insert Futures Trade Signal using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel e);
    /// <summary>Upsert Market Outlook Snapshot using the MarketDataDb persistence.</summary>
    /// <param name="snapshot">The snapshot used by this operation.</param>
    /// <param name="revision">The revision used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task UpsertMarketOutlookSnapshotAsync(MarketOutlookReadModel snapshot, long revision = 0,
        CancellationToken cancellationToken = default);
    /// <summary>Insert Rate Of Return using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertRateOfReturnAsync(RateOfReturnReadModel e);
    /// <summary>Insert Yield Curve Rate using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e);
    /// <summary>Persists a caller-qualified batch of YieldCurveRateReadModel rows.</summary>
    /// <param name="batch">The accepted rows, duplicate policy and source command identity.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task InsertYieldCurveRatesAsync(MarketDataImportBatch<YieldCurveRateReadModel> batch);
    /// <summary>Insert Market Holiday using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertMarketHolidayAsync(MarketHolidayReadModel e);
    /// <summary>Insert Trade Live Feed using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e);

    /// <summary>Insert Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <param name="historySequenceId">The history identity assigned by the producer.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertFuturesEodDataAsync(FuturesEodDataV2ReadModel e, long historySequenceId);
    /// <summary>Persists the caller-qualified batch of current futures EOD snapshots.</summary>
    /// <param name="batch">The batch with unique canonical EOD keys.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task InsertFuturesEodDataAsync(FuturesEodBatch batch);
    /// <summary>Update Futures Eod Session Statistics using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task UpdateFuturesEodSessionStatisticsAsync(FuturesEodDataV2ReadModel e);
    /// <summary>Insert Vix Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="observation">The caller-qualified tick and optional statistics.</param>
    /// <returns>The completion of the persistence operation.</returns>
    Task InsertVixFuturesEodDataAsync(VixTickObservation observation);
    /// <summary>Atomically advances a historical-load checkpoint and inserts its immutable manifest.</summary>
    /// <param name="state">The supplied load state.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of persistence.</returns>
    /// <param name="diagnostics">The diagnostics supplied by the caller.</param>
    Task SaveHistoricalDataLoadAsync(HistoricalDataLoaderState state, FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel diagnostics, CancellationToken cancellationToken = default);

    /// <summary>Inserts prepared diagnostics for a legacy checkpoint if no progress has been projected.</summary>
    /// <param name="diagnostics">The legacy checkpoint projected by its owner.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of the idempotent insert.</returns>
    Task BackfillHistoricalAnalyticsProgressAsync(FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel diagnostics, CancellationToken cancellationToken);
    /// <summary>Writes the committed Analytics attempt projection.</summary>
    /// <param name="diagnostics">The diagnostics supplied by the caller.</param>
    /// <param name="eventId">The event id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpsertHistoricalAnalyticsAttemptProjectionAsync(FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel diagnostics, long eventId, CancellationToken cancellationToken = default);
}
