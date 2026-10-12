using TomasAI.IFM.Application.MarketData.Contracts.Historical;

using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Keep the published context interface and named argument contract; inherited capability members intentionally describe the same database operation.")]
public interface IMarketDataDbReadContext
{
    /// <summary>Checks usable PostgreSQL canonical query indexes; raw tick readiness belongs to TradeTickQuoteDb.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<MarketDataProjectionReadinessReadModel> GetQueryProjectionReadinessAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Read using the MarketDataDb persistence.</summary>
    /// <param name="key">The key used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<CompositionPreparation?> ReadAsync(
        CompositionPreparationKey key,
        CancellationToken cancellationToken);
    /// <summary>Read using the MarketDataDb persistence.</summary>
    /// <param name="contract">The contract used by this operation.</param>
    /// <param name="date">The date used by this operation.</param>
    /// <param name="sourceId">The source id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<OptionTradeEvidence?> ReadAsync(
        string contract,
        DateOnly date,
        string sourceId,
        CancellationToken cancellationToken = default);
    /// <summary>Get Raw Eod using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken);
    /// <summary>Get Raw Eod Range using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    ValueTask<IReadOnlyList<FuturesEodObservationReadModel>> GetRawEodRangeAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);
    /// <summary>Get Snapshot using the MarketDataDb persistence.</summary>
    /// <param name="environment">The environment used by this operation.</param>
    /// <param name="snapshotId">The snapshot id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<OptionIvMetricRevision?> GetSnapshotAsync(
        string environment,
        string snapshotId,
        CancellationToken cancellationToken = default);
    /// <summary>Get Latest using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<LatestVolatilityResult> GetLatestAsync(
        LatestVolatilityRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>Get Observation History using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(
        VolatilityHistoryPageRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>Get Metric History using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(
        VolatilityHistoryPageRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>Rebuild Latest Cache using the MarketDataDb persistence.</summary>
    /// <param name="scopes">The scopes used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>> RebuildLatestCacheAsync(
        IEnumerable<VolatilityStorageScope> scopes,
        CancellationToken cancellationToken = default);

    /// <summary>Get Market Data Download Log using the MarketDataDb persistence.</summary>
    /// <param name="partition">The partition used by this operation.</param>
    /// <param name="attempt">The attempt used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<MarketDataDownloadLogResult> GetMarketDataDownloadLogAsync(
        MarketDataDownloadPartition partition,
        MarketDataDownloadCursor attempt,
        CancellationToken cancellationToken = default);
    /// <summary>Get Market Data Download History using the MarketDataDb persistence.</summary>
    /// <param name="partition">The partition used by this operation.</param>
    /// <param name="pageSize">The page size used by this operation.</param>
    /// <param name="cursor">The cursor used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<MarketDataDownloadHistoryResult> GetMarketDataDownloadHistoryAsync(
        MarketDataDownloadPartition partition,
        int pageSize = 100,
        MarketDataDownloadCursor? cursor = null,
        CancellationToken cancellationToken = default);
    /// <summary>Get Market Data Download Status using the MarketDataDb persistence.</summary>
    /// <param name="partition">The partition used by this operation.</param>
    /// <param name="requiredImportCommandId">The required import command id used by this operation.</param>
    /// <param name="cursor">The cursor used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<MarketDataDownloadStatusResult> GetMarketDataDownloadStatusAsync(
        MarketDataDownloadPartition partition,
        Guid? requiredImportCommandId = null,
        MarketDataDownloadCursor? cursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get Latest Futures Ema Signal using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesEmaSignalReadModel?> GetLatestFuturesEmaSignalAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);
    /// <summary>Get Latest Futures Bollinger Band Signal using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesBbSignalReadModel?> GetLatestFuturesBollingerBandSignalAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);
    /// <summary>Get Latest Futures Bollinger Band Signal For Time Frame using the MarketDataDb persistence.</summary>
    /// <param name="seriesIdentity">The series identity used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timeFrame">The time frame used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesBbSignalReadModel?> GetLatestFuturesBollingerBandSignalForTimeFrameAsync(
        MarketSeriesIdentity seriesIdentity, DateOnly valueDate, TimeFrameType timeFrame,
        CancellationToken cancellationToken = default);
    /// <summary>Gets the latest projected exact VWAP for one futures session.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesVwapSignalReadModel?> GetLatestFuturesVwapSignalAsync(
        string contractId, DateOnly valueDate, string configurationId,
        CancellationToken cancellationToken = default);
    /// <summary>Gets projected exact VWAP updates for one futures session.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesVwapSignalReadModel>> GetFuturesVwapSignalHistoryAsync(
        string contractId, DateOnly valueDate, string configurationId,
        CancellationToken cancellationToken = default);
    /// <summary>Gets the latest projected front/back VX curve for a trading date.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesVxTermStructureSignalReadModel?> GetLatestFuturesVxTermStructureSignalAsync(
        DateOnly valueDate,
        string configurationId,
        CancellationToken cancellationToken = default);
    /// <summary>Get Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="economicCalendarId">The economic calendar id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId economicCalendarId);
    /// <summary>Get Economic Calendar using the MarketDataDb persistence.</summary>
    /// <param name="economicCalendarId">The economic calendar id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId economicCalendarId, CancellationToken cancellationToken);
    /// <summary>Get Economic Calendars using the MarketDataDb persistence.</summary>
    /// <param name="eventDate">The event date used by this operation.</param>
    /// <param name="countryCode">The country code used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode);
    /// <summary>Get Economic Calendars using the MarketDataDb persistence.</summary>
    /// <param name="eventDate">The event date used by this operation.</param>
    /// <param name="countryCode">The country code used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode, CancellationToken cancellationToken);
    /// <summary>Get Economic Calendars using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="countryCode">The country code used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode);
    /// <summary>Get Economic Calendars using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="countryCode">The country code used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode, CancellationToken cancellationToken);
    /// <summary>Get Economic Calendar Page using the MarketDataDb persistence.</summary>
    /// <param name="request">The request used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<EconomicCalendarPageReadModel> GetEconomicCalendarPageAsync(EconomicCalendarPageRequest request, CancellationToken cancellationToken = default);
    /// <summary>Get Economic Calendar All using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync();
    /// <summary>Get Economic Calendar All using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync(CancellationToken cancellationToken);
    /// <summary>Get Economic Calendar Country Codes using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync();
    /// <summary>Get Economic Calendar Country Codes using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken);
    /// <summary>Get Futures Data Id using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesDataId?> GetFuturesDataId(string contractId, DateOnly valueDate);
    /// <summary>Get Yesterdays Futures Closing Price using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesClosingPriceReadModel?> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId id);
    /// <summary>Get Futures Closing Price using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesClosingPriceReadModel?> GetFuturesClosingPriceAsync(FuturesDataId e);
    /// <summary>Get Futures Tick HLVData using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId e);
    /// <summary>Get Last Futures Tick Data Id using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="futuresTickDataId">The futures tick data id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId futuresTickDataId);
    /// <summary>Get Last Futures Option Tick Data Id using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Option Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="futuresTickDataId">The futures tick data id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId futuresTickDataId);
    /// <summary>Get Futures Option Tick Price Data using the MarketDataDb persistence.</summary>
    /// <param name="futuresTickDataId">The futures tick data id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickPriceDataAsync(FuturesOptionTickDataId futuresTickDataId);
    /// <summary>Get Futures Bar Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate);
    /// <summary>Get Futures Bar Data using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync();
    /// <summary>Get Last Futures Bar Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesBarDataReadModel> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate);
    /// <summary>Get Futures Bar Data Count using the MarketDataDb persistence.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<int> GetFuturesBarDataCountAsync(FuturesBarDataId id);
    /// <summary>Get Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync();
    /// <summary>Get Futures Intra Day Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesIntraDayDataReadModel>> GetFuturesIntraDayDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Eod Data By Date Range using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateOnly startDate, DateOnly endDate);
    /// <summary>Get Futures Eod Moving Average using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesEodMovingAverageReadModel?> GetFuturesEodMovingAverageAsync(string symbol, DateTime startDate, DateTime endDate);
    /// <summary>Get Futures Eod Closing Prices using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="maxDays">The max days used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesEodClosingPriceReadModel>> GetFuturesEodClosingPricesAsync(string contractId, string symbol, DateOnly startDate, DateOnly endDate, int maxDays);
    /// <summary>Get Current Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesEodDataV2ReadModel?> GetCurrentFuturesEodDataAsync(DateOnly valueDate);
    /// <summary>Get Current Futures Eod Data By Date Range using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesEodDataV2ReadModel>> GetCurrentFuturesEodDataByDateRangeAsync(DateOnly startDate, DateOnly endDate);
    /// <summary>Get Futures Iti Signals using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(FuturesItiSignalEntityId e);
    /// <summary>Get Futures Iti Signals using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(string symbol, DateOnly startDate, DateOnly endDate);
    /// <summary>Get Futures Iti Signals For Contract using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsForContractAsync(string contractId, DateOnly startDate, DateOnly endDate);
    /// <summary>Get Futures Iti Signal Trend Class Data using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    /// <summary>Get Futures Iti Signal Trend Delta Data using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    /// <summary>Get Futures Iti Signal MDI using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Iti Signal MDI using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);

    /// <summary>Get Futures Iti Signal MDIBy Trend using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
    /// <param name="intrinsicTimeGroupId">The intrinsic time group id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId);
    /// <summary>Get Futures Iti Signal MDIBy Trend using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
    /// <param name="intrinsicTimeGroupId">The intrinsic time group id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId, CancellationToken cancellationToken);
    /// <summary>Get Futures Iti Trend Direction Changed Signals using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Iti Trend Direction Changed Signals using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Futures Trend Direction From RSISignal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="timestamp">The timestamp used by this operation.</param>
    /// <param name="lookbackInterval">The lookback interval used by this operation.</param>
    /// <param name="startTime">The start time used by this operation.</param>
    /// <param name="endTime">The end time used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime);
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
    Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod);
    /// <summary>Get Last Futures Iti Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, CancellationToken cancellationToken);
    /// <summary>Get Futures Iti Time Frame State using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="calendarBucketStart">The calendar bucket start used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetFuturesItiTimeFrameStateAsync(string contractId, TimeFrameType timePeriod, DateOnly calendarBucketStart, CancellationToken cancellationToken = default);
    /// <summary>Get Last Futures Iti Signal Trend Direction Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Iti Signal Trend Direction Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Iti Signal Trend Extreme Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Iti Signal Trend Extreme Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Iti Signal Trend Reversal Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Iti Signal Trend Reversal Change using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Tick At Or Before using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="tickTime">The tick time used by this operation.</param>
    /// <param name="token">The token used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTickDataV2ReadModel?> GetFuturesTickAtOrBeforeAsync(string contractId, DateOnly valueDate, TimeOnly tickTime, CancellationToken token = default);
    /// <summary>Get Last Futures Tick Data By Tick Date using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="tickDate">The tick date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate);
    /// <summary>Get Last Futures Option Tick Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Futures Option Chain Quote Data using the MarketDataDb persistence.</summary>
    /// <param name="underlyingContractId">The underlying contract id used by this operation.</param>
    /// <param name="expiryDate">The expiry date used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesOptionTickDataV2ReadModel>> GetFuturesOptionChainQuoteDataAsync(
        string underlyingContractId, DateOnly expiryDate, DateOnly valueDate, CancellationToken cancellationToken = default);
    /// <summary>Get Last Futures Option Tick Price Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Rsi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Rsi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Rsi Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Rsi Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Tdi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Tdi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Tdi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        string configurationId);
    /// <summary>Get Last Futures Tdi Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="configurationId">The configuration id used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        string configurationId,
        CancellationToken cancellationToken);
    /// <summary>Get Last Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod);
    /// <summary>Get Last Futures Macd Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Macd Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Macd Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Macd Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod);
    /// <summary>Get Last Futures Macd Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="signalEmaPeriod">The signal ema period used by this operation.</param>
    /// <param name="fastEmaPeriod">The fast ema period used by this operation.</param>
    /// <param name="slowEmaPeriod">The slow ema period used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int signalEmaPeriod, int fastEmaPeriod, int slowEmaPeriod, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Atr Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Atr Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Atr Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Atr Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Adx Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Adx Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Adx Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength);
    /// <summary>Get Last Futures Adx Daily Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="timePeriod">The time period used by this operation.</param>
    /// <param name="periodLength">The period length used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Trade Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Last Futures Trade Signal using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Last Futures Trade Signal using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync();
    /// <summary>Get Last Futures Trade Signal using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(CancellationToken cancellationToken);
    /// <summary>Get Market Outlook Snapshot using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<MarketOutlookReadModel?> GetMarketOutlookSnapshotAsync(
        string contractId, DateOnly valueDate, CancellationToken cancellationToken = default);
    /// <summary>Get Futures Trade Signals using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalsAsync();
    /// <summary>Get Last Futures Trade Signal By Symbol using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate);
    /// <summary>Get Last Futures Trade Signal By Symbol using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Last Rate Of Return using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol);
    /// <summary>Get Last Rate Of Return using the MarketDataDb persistence.</summary>
    /// <param name="symbol">The symbol used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol, CancellationToken cancellationToken);
    /// <summary>Get Last Vix Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Vix Futures Eod Data using the MarketDataDb persistence.</summary>
    /// <param name="contractId">The contract id used by this operation.</param>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    /// <summary>Get Vix Futures Eod Data By Value Date using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateOnly valueDate);
    /// <summary>Get Vix Futures Tick HLVData using the MarketDataDb persistence.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<FuturesTickHLVDataReadModel?> GetVixFuturesTickHLVDataAsync(VixFuturesEodDataEntityId e);

    /// <summary>Get Last Yield Curve Rate using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync();
    /// <summary>Get Last Yield Curve Rate using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync(CancellationToken cancellationToken);
    /// <summary>Get Yield Curve Rate using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<YieldCurveRateReadModel?> GetYieldCurveRateAsync(DateOnly valueDate);
    /// <summary>Get Yield Curve Rates using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate);
    /// <summary>Get Yield Curve Rates using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    /// <summary>Get Yield Curve Rate Years using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<int>> GetYieldCurveRateYearsAsync();
    /// <summary>Get Yield Curve Rate Years using the MarketDataDb persistence.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<int>> GetYieldCurveRateYearsAsync(CancellationToken cancellationToken);
    /// <summary>Get Yield Curve Rate Exists using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate);
    /// <summary>Get Yield Curve Rate Exists using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Market Holidays using the MarketDataDb persistence.</summary>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType);
    /// <summary>Get Market Holidays using the MarketDataDb persistence.</summary>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType, CancellationToken cancellationToken);
    /// <summary>Get Market Holidays By Date Range using the MarketDataDb persistence.</summary>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysByDateRangeAsync(CurrencyType currencyType, DateOnly startDate, DateOnly endDate);
    /// <summary>Get Trading Days using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<int> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD);
    /// <summary>Get Trading Dates using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<DateOnly[]> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD);
    /// <summary>Get Trading Dates using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<DateOnly[]> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken);
    /// <summary>Get Trading Day Count using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<int> GetTradingDayCountAsync(DateOnly startDate, DateOnly endDate, MarketType marketType = MarketType.Futures, CurrencyType currencyType = CurrencyType.USD);
    /// <summary>Get Trading Day Count using the MarketDataDb persistence.</summary>
    /// <param name="startDate">The start date used by this operation.</param>
    /// <param name="endDate">The end date used by this operation.</param>
    /// <param name="marketType">The market type used by this operation.</param>
    /// <param name="currencyType">The currency type used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<int> GetTradingDayCountAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken);

    /// <summary>Get Normal Curve Data using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<NormalCurveDataReadModel>> GetNormalCurveDataAsync();
    /// <summary>Get Normal Curve Table using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<NormalCurveTableReadModel> GetNormalCurveTableAsync();
    /// <summary>Get Streaming Request Id using the MarketDataDb persistence.</summary>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<int> GetStreamingRequestIdAsync();
    /// <summary>Get Futures Trade Signal Id By Value Date using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate);
    /// <summary>Get Futures Trade Signal Id By Value Date using the MarketDataDb persistence.</summary>
    /// <param name="valueDate">The value date used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate, CancellationToken cancellationToken);
    /// <summary>Get Trade Live Feed using the MarketDataDb persistence.</summary>
    /// <param name="orderId">The order id used by this operation.</param>
    /// <param name="tradeId">The trade id used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    Task<TradeLiveFeedReadModel?> GetTradeLiveFeedAsync(int orderId, int tradeId);






    /// <summary>Reads one historical-load checkpoint and terminal manifest.</summary>
    /// <param name="attemptId">The stable attempt identity.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The stored attempt, or null.</returns>
    Task<HistoricalDataLoaderState?> GetHistoricalDataLoadAsync(Guid attemptId, CancellationToken cancellationToken = default);
    /// <summary>Reads the completed historical-load owner of a request hash.</summary>
    /// <param name="requestSha256">The request digest.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The completed attempt, or null.</returns>
    Task<HistoricalDataLoaderState?> GetCompletedHistoricalDataLoadAsync(string requestSha256, CancellationToken cancellationToken = default);
}
