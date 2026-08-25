using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

public sealed class MarketDataSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<MarketDataSchemaDb>(connectionSettings[MarketDataDbContext.MarketDataDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("market_data_import_ownership", MarketDataSchemaCql.CreateMarketDataImportOwnershipTable, "DROP TABLE IF EXISTS market_data_import_ownership;"),
        new("market_outlook_snapshot", MarketDataSchemaCql.CreateMarketOutlookSnapshotTable, "DROP TABLE IF EXISTS market_outlook_snapshot;"),
        new("economic_calendar_v2", MarketDataSchemaCql.CreateEconomicCalendarV2Table, "DROP TABLE IF EXISTS economic_calendar_v2;"),
        new("economic_calendar_country_code", MarketDataSchemaCql.CreateEconomicCalendarCountryCodeTable, "DROP TABLE IF EXISTS economic_calendar_country_code;"),
        new("economic_calendar_cutover_v2", MarketDataSchemaCql.CreateEconomicCalendarCutoverV2Table, "DROP TABLE IF EXISTS economic_calendar_cutover_v2;"),
        new("tick_quote_item", MarketDataSchemaCql.CreateTickQuoteItemType, "DROP TYPE IF EXISTS tick_quote_item;"),
        new("tick_trade_data", MarketDataSchemaCql.CreateTickTradeDataTable, "DROP TABLE IF EXISTS tick_trade_data;"),
        new("tick_quote_data", MarketDataSchemaCql.CreateTickQuoteDataTable, "DROP TABLE IF EXISTS tick_quote_data;"),
        new("trade_live_feed", MarketDataSchemaCql.CreateTradeLiveFeedTable, "DROP TABLE IF EXISTS trade_live_feed;"),
        new("futures_iti_signal", MarketDataSchemaCql.CreateFuturesitiSignalTable, "DROP TABLE IF EXISTS futures_iti_signal;"),
        new("futures_iti_signal_by_contract_day", MarketDataSchemaCql.CreateFuturesItiSignalByContractDayTable, "DROP TABLE IF EXISTS futures_iti_signal_by_contract_day;"),
        new("futures_iti_signal_by_contract_month", MarketDataSchemaCql.CreateFuturesItiSignalByContractMonthTable, "DROP TABLE IF EXISTS futures_iti_signal_by_contract_month;"),
        new("futures_iti_signal_by_trend_mode_month", MarketDataSchemaCql.CreateFuturesItiSignalByTrendModeMonthTable, "DROP TABLE IF EXISTS futures_iti_signal_by_trend_mode_month;"),
        new("futures_iti_timeframe_state", MarketDataSchemaCql.CreateFuturesItiTimeFrameStateTable, "DROP TABLE IF EXISTS futures_iti_timeframe_state;"),
        new("futures_tick_data", MarketDataSchemaCql.CreateFuturesTickDataTable, "DROP TABLE IF EXISTS futures_tick_data;"),
        new("futures_tick_data_by_time", MarketDataSchemaCql.CreateFuturesTickDataByTimeTable, "DROP TABLE IF EXISTS futures_tick_data_by_time;"),
        new("futures_option_tick_data", MarketDataSchemaCql.CreateFuturesOptionTickDataTable, "DROP TABLE IF EXISTS futures_option_tick_data;"),
        new("futures_option_tick_price_data", MarketDataSchemaCql.CreateFuturesOptionTickPriceDataTable, "DROP TABLE IF EXISTS futures_option_tick_price_data;"),
        new("futures_bar_data", MarketDataSchemaCql.CreateFuturesBaraDataTable, "DROP TABLE IF EXISTS futures_bar_data;"),
        new("futures_closing_price", MarketDataSchemaCql.CreateFuturesClosingPriceTable, "DROP TABLE IF EXISTS futures_closing_price;"),
        new("futures_eod_data", MarketDataSchemaCql.CreateFuturesEodDataTable, "DROP TABLE IF EXISTS futures_eod_data;"),
        new("futures_eod_data_by_month", MarketDataSchemaCql.CreateFuturesEodDataByMonthTable, "DROP TABLE IF EXISTS futures_eod_data_by_month;"),
        new("futures_eod_data_fifty_dma", MarketDataSchemaCql.AddFuturesEodDataFiftyDmaColumn, "ALTER TABLE futures_eod_data DROP fiftyDMA;", ["conflicts with an existing column", "already exists"]),
        new("futures_eod_data_two_hundred_dma", MarketDataSchemaCql.AddFuturesEodDataTwoHundredDmaColumn, "ALTER TABLE futures_eod_data DROP twoHundredDMA;", ["conflicts with an existing column", "already exists"]),
        new("futures_eod_data_by_month_fifty_dma", MarketDataSchemaCql.AddFuturesEodDataByMonthFiftyDmaColumn, "ALTER TABLE futures_eod_data_by_month DROP fiftyDMA;", ["conflicts with an existing column", "already exists"]),
        new("futures_eod_data_by_month_two_hundred_dma", MarketDataSchemaCql.AddFuturesEodDataByMonthTwoHundredDmaColumn, "ALTER TABLE futures_eod_data_by_month DROP twoHundredDMA;", ["conflicts with an existing column", "already exists"]),
        new("futures_intra_day_data", MarketDataSchemaCql.CreateFuturesIntraDayDataTable, "DROP TABLE IF EXISTS futures_intra_day_data;"),
        new("vix_futures_eod_data", MarketDataSchemaCql.CreateVixFuturesEodDataTable, "DROP TABLE IF EXISTS vix_futures_eod_data;"),
        new("vix_futures_contract_index", MarketDataSchemaCql.CreateVixFuturesContractIndexTable, "DROP TABLE IF EXISTS vix_futures_contract_index;"),
        new("market_data_projection_month", MarketDataSchemaCql.CreateMarketDataProjectionMonthTable, "DROP TABLE IF EXISTS market_data_projection_month;"),
        new("market_data_projection_state_v2", MarketDataSchemaCql.CreateMarketDataProjectionStateV2Table, "DROP TABLE IF EXISTS market_data_projection_state_v2;"),
        new("market_data_projection_mutation", MarketDataSchemaCql.CreateMarketDataProjectionMutationTable, "DROP TABLE IF EXISTS market_data_projection_mutation;"),
        new("market_data_projection_scope_state_v3", MarketDataSchemaCql.CreateMarketDataProjectionScopeStateV3Table, "DROP TABLE IF EXISTS market_data_projection_scope_state_v3;"),
        new("market_data_projection_scope_mutation_v3", MarketDataSchemaCql.CreateMarketDataProjectionScopeMutationV3Table, "DROP TABLE IF EXISTS market_data_projection_scope_mutation_v3;"),
        new("futures_trade_signal", MarketDataSchemaCql.CreateFuturesTradeSignalTable, "DROP TABLE IF EXISTS futures_trade_signal;"),
        new("futures_trade_signal_lookup_by_scope", MarketDataSchemaCql.CreateFuturesTradeSignalLookupTable, "DROP TABLE IF EXISTS futures_trade_signal_lookup_by_scope;"),
        new("futures_trade_signal_quarantine", MarketDataSchemaCql.CreateFuturesTradeSignalQuarantineTable, "DROP TABLE IF EXISTS futures_trade_signal_quarantine;"),
        new("futures_eod_data_index", MarketDataSchemaCql.CreateFuturesEodDataIndexTable, "DROP TABLE IF EXISTS futures_eod_data_index;"),
        new("futures_iti_signal_index", MarketDataSchemaCql.CreateFuturesItiSignalIndexTable, "DROP TABLE IF EXISTS futures_iti_signal_index;"),
        new("futures_iti_trend_class_data", MarketDataSchemaCql.CreateFuturesItiTrendClassDataTable, "DROP TABLE IF EXISTS futures_iti_trend_class_data;"),
        new("futures_iti_trend_delta_data", MarketDataSchemaCql.CreateFuturesItiTrendDeltaDataTable, "DROP TABLE IF EXISTS futures_iti_trend_delta_data;"),
        new("futures_iti_trend_class_model", MarketDataSchemaCql.CreateFuturesItiTrendClassModelTable, "DROP TABLE IF EXISTS futures_iti_trend_class_model;"),
        new("yield_curve_rates", MarketDataSchemaCql.CreateYieldCurveRateTable, "DROP TABLE IF EXISTS yield_curve_rates;"),
        new("yield_curve_rate_by_date", MarketDataSchemaCql.CreateYieldCurveRateByDateTable, "DROP TABLE IF EXISTS yield_curve_rate_by_date;"),
        new("yield_curve_rate_year", MarketDataSchemaCql.CreateYieldCurveRateYearTable, "DROP TABLE IF EXISTS yield_curve_rate_year;"),
        new("rate_of_return", MarketDataSchemaCql.CreateRateOfReturn, "DROP TABLE IF EXISTS rate_of_return;"),
        new("futures_iti_trend_delta_model", MarketDataSchemaCql.CreateFuturesItiTrendDeltaModelTable, "DROP TABLE IF EXISTS futures_iti_trend_delta_model;"),
        new("futures_rsi_signal", MarketDataSchemaCql.CreateFuturesRsiSignalTable, "DROP TABLE IF EXISTS futures_rsi_signal;"),
        new("futures_rsi_signal_source_sequence", MarketDataSchemaCql.AddFuturesRsiSignalSourceSequenceColumn, "ALTER TABLE futures_rsi_signal DROP sourceSequence;", ["conflicts with an existing column", "already exists"]),
        new("futures_rsi_signal_source_event_timestamp", MarketDataSchemaCql.AddFuturesRsiSignalSourceEventTimestampColumn, "ALTER TABLE futures_rsi_signal DROP sourceEventTimestamp;", ["conflicts with an existing column", "already exists"]),
        new("futures_tdi_signal", MarketDataSchemaCql.CreateFuturesTdiSignalTable, "DROP TABLE IF EXISTS futures_tdi_signal;"),
        new("futures_traders_dynamic_index_signal", MarketDataSchemaCql.CreateFuturesTradersDynamicIndexSignalTable, "DROP TABLE IF EXISTS futures_traders_dynamic_index_signal;"),
        new("futures_macd_signal", MarketDataSchemaCql.CreateFuturesMacdSignalTable, "DROP TABLE IF EXISTS futures_macd_signal;"),
        new("futures_macd_signal_v2", MarketDataSchemaCql.CreateFuturesMacdSignalV2Table, "DROP TABLE IF EXISTS futures_macd_signal_v2;"),
        new("futures_adx_signal", MarketDataSchemaCql.CreateFuturesAdxSignalTable, "DROP TABLE IF EXISTS futures_adx_signal;"),
        new("futures_atr_signal", MarketDataSchemaCql.CreateFuturesAtrSignalTable, "DROP TABLE IF EXISTS futures_atr_signal;"),
        new("market_holiday", MarketDataSchemaCql.CreateMarketHolidayTable, "DROP TABLE IF EXISTS market_holiday;"),
        new("normal_curve_data", MarketDataSchemaCql.CreateNormalCurveDataTable, "DROP TABLE IF EXISTS normal_curve_data;"),
        new("futures_rsi_signal_signaltype", MarketDataSchemaCql.CreateFuturesRsiSignal_SignalTypeIndex, "DROP INDEX IF EXISTS futures_rsi_signal_signaltype;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
