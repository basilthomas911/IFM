namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>SQL statements and query templates for MarketData storage.</summary>
public static class MarketDataDbSql
{
    /// <summary>AcquireMarketDataIdentityLock database statement.</summary>
    internal const string AcquireMarketDataIdentityLock =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1,0));
        """;

    /// <summary>Inserts legacy progress without replacing a projection written by a running loader.</summary>
    internal const string BackfillHistoricalAnalyticsProgress =
        """
        INSERT INTO historical_analytics_attempt_progress (attempt_id, payload) VALUES ($1, $2)
        ON CONFLICT (attempt_id) DO NOTHING;
        """;

    /// <summary>ClearTradeSignalLookup database statement.</summary>
    internal const string ClearTradeSignalLookup =
        """
        DELETE FROM market_data.futures_trade_signal_lookup_by_scope;
        """;

    /// <summary>ClearYieldCurveDateIndex database statement.</summary>
    internal const string ClearYieldCurveDateIndex =
        """
        DELETE FROM market_data.yield_curve_rate_by_date WHERE lookupid=1;
        """;

    /// <summary>ClearYieldCurveYearIndex database statement.</summary>
    internal const string ClearYieldCurveYearIndex =
        """
        DELETE FROM market_data.yield_curve_rate_year WHERE lookupid=1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteEconomicCalendarV2 =
        """
        DELETE
        FROM market_data.economic_calendar
        WHERE countrycode = $1 AND monthbucket = $2 AND eventdate >= date_trunc('second', $3::timestamptz)
            AND eventdate < date_trunc('second', $3::timestamptz) + INTERVAL '1 second' AND eventname = $4;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string DeleteFuturesAdxSignal =
        """
        DELETE
        FROM market_data.futures_adx_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3 AND valuedate = $4;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string DeleteFuturesAtrSignal =
        """
        DELETE
        FROM market_data.futures_atr_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3 AND valuedate = $4;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteFuturesBarData =
        """
        DELETE FROM market_data.futures_bar_data WHERE contractid = $1 AND symbol = $2 AND valuedate = $3;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteFuturesClosingPrice =
        """
        DELETE FROM market_data.futures_closing_price WHERE contractid = $1 AND valuedate = $2;
        """;

    /// <summary>SQL for delete futures eod data.</summary>
    internal const string DeleteFuturesEodData =
        """
        DELETE FROM market_data.futures_eod_data WHERE contractid = $1 AND valuedate = $2;
        """;

    /// <summary>SQL for delete futures eod data by month.</summary>
    internal const string DeleteFuturesEodDataByMonth =
        """
        DELETE
        FROM market_data.futures_eod_data_by_month WHERE yearmonth = $1 AND valuedate = $2 AND contractid = $3;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteFuturesItiSignal =
        """
        DELETE FROM market_data.futures_iti_signal WHERE contractid = $1 AND valuedate = $2 AND timeperiod = $3;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteFuturesOptionTickPriceData =
        """
        DELETE FROM market_data.futures_option_tick_price_data WHERE contractid = $1 AND valuedate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteMarketHoliday =
        """
        DELETE FROM market_data.market_holiday WHERE currencytype = $1 AND holidaydate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteMarketHolidays =
        """
        DELETE FROM market_data.market_holiday WHERE currencytype = $1;
        """;

    /// <summary>Deletes a timeframe state only when it represents the removed value date.</summary>
    internal const string DeleteNativeItiTimeFrameState =
        """
        DELETE
        FROM market_data.futures_iti_timeframe_state
        WHERE contractid = $1 AND timeperiod = $2 AND calendarbucketstart = $3 AND valuedate = $4;
        """;

    /// <summary>SQL for delete rate of return.</summary>
    internal const string DeleteRateOfReturn =
        """
        DELETE FROM market_data.rate_of_return WHERE symbol = $1 AND valuedate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string DeleteTradeLiveFeed =
        """
        DELETE FROM market_data.trade_live_feed WHERE orderid = $1 AND tradeid = $2;
        """;

    /// <summary>SQL for delete vix futures eod data.</summary>
    internal const string DeleteVixFuturesEodData =
        """
        DELETE FROM market_data.vix_futures_eod_data WHERE contractid = $1 AND valuedate = $2;
        """;

    /// <summary>SQL for delete yield curve rate.</summary>
    internal const string DeleteYieldCurveRate =
        """
        DELETE FROM market_data.yield_curve_rates WHERE id = 1 AND valuedate = $1;
        """;

    /// <summary>SQL for delete yield curve rate by date.</summary>
    internal const string DeleteYieldCurveRateByDate =
        """
        DELETE FROM market_data.yield_curve_rate_by_date WHERE lookupid = 1 AND valuedate = $1;
        """;

    /// <summary>SQL for download log exact.</summary>
    internal const string DownloadLogExact =
        """
        SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id,
            source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds,
            downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc
        FROM market_data.market_data_download_log
        WHERE dataset = $1 AND provider = $2 AND scope = $3 AND value_date = $4 AND requested_at_utc=$5 AND import_command_id=$6
        LIMIT 1;
        """;

    /// <summary>SQL for download log history.</summary>
    internal const string DownloadLogHistory =
        """
        SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id,
            source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds,
            downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc
        FROM market_data.market_data_download_log
        WHERE dataset = $1 AND provider = $2 AND scope = $3 AND value_date = $4
        ORDER BY requested_at_utc DESC,import_command_id DESC LIMIT $5;
        """;

    /// <summary>SQL for download log history after.</summary>
    internal const string DownloadLogHistoryAfter =
        """
        SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id,
            source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds,
            downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc
        FROM market_data.market_data_download_log
        WHERE dataset = $1 AND provider = $2 AND scope = $3 AND value_date = $4 AND (requested_at_utc,import_command_id)<($5,$6)
        ORDER BY requested_at_utc DESC,import_command_id DESC LIMIT $7;
        """;

    /// <summary>SQL for download log insert.</summary>
    internal const string DownloadLogInsert =
        """
        INSERT INTO market_data.market_data_download_log (dataset, provider, scope, value_date, requested_at_utc,
            import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc,
            finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code,
            error_message, payload_sha256, projected_at_utc)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19)
        ON CONFLICT (dataset,provider,scope,value_date,requested_at_utc,
            import_command_id) DO UPDATE SET log_command_id=EXCLUDED.log_command_id,
            source_terminal_event_id=EXCLUDED.source_terminal_event_id,schema_version=EXCLUDED.schema_version,
            status=EXCLUDED.status,started_at_utc=EXCLUDED.started_at_utc,finished_at_utc=EXCLUDED.finished_at_utc,
            elapsed_milliseconds=EXCLUDED.elapsed_milliseconds,downloaded_record_count=EXCLUDED.downloaded_record_count,
            persisted_record_count=EXCLUDED.persisted_record_count,error_code=EXCLUDED.error_code,
            error_message=EXCLUDED.error_message,payload_sha256=EXCLUDED.payload_sha256,
            projected_at_utc=EXCLUDED.projected_at_utc;
        """;

    /// <summary>Reconciles canonical query sources in one database snapshot without duplicate read tables.</summary>
    internal const string GetCanonicalQueryInventory =
        """
        SELECT 'eod' AS kind,COUNT(*) AS ROWS,md5(COALESCE(string_agg(md5(to_jsonb(t)::text),
            '' ORDER BY to_jsonb(t)::text),'')) AS fingerprint
          FROM market_data.futures_eod_data t
        UNION ALL SELECT 'vix_rows',COUNT(*),'' FROM market_data.vix_futures_eod_data
        UNION ALL SELECT 'vix',COUNT(*),md5(COALESCE(string_agg(contractid,'' ORDER BY contractid),'')) FROM
          (SELECT DISTINCT contractid FROM market_data.vix_futures_eod_data) t
        UNION ALL SELECT 'iti',COUNT(*),md5(COALESCE(string_agg(md5(to_jsonb(t)::text),'' ORDER BY to_jsonb(t)::text),''))
          FROM market_data.futures_iti_signal t;
        """;

    /// <summary>Checks canonical PostgreSQL query indexes; raw ticks belong to TradeTickQuoteDb.</summary>
    internal const string GetCanonicalQueryReadiness =
        """
        WITH required(group_name,index_name,table_name,columns) AS (VALUES
          ('eod','futures_eod_data_ordered','futures_eod_data',ARRAY['contractid','valuedate','symbol']),
          ('eod','futures_eod_value_date','futures_eod_data',ARRAY['valuedate','contractid','symbol']),
          ('vix','vix_futures_eod_data_ordered','vix_futures_eod_data',ARRAY['contractid','valuedate']),
          ('vix','vix_futures_eod_value_date','vix_futures_eod_data',ARRAY['valuedate','contractid']),
          ('iti','futures_iti_signal_ordered','futures_iti_signal',ARRAY['contractid','valuedate','timeperiod',
            'intrinsictimemode','intrinsictimetrend','intrinsictimegroupid','sequenceid'])),
        checks AS (SELECT r.group_name,COALESCE(i.indisvalid AND i.indisready
          AND i.indrelid=to_regclass('market_data.'||r.table_name)
          AND i.indpred IS NULL AND i.indexprs IS NULL
          AND ARRAY(SELECT a.attname::text FROM unnest(i.indkey) WITH ordinality k(attnum,ordinality)
              JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum
              WHERE k.ordinality<=i.indnkeyatts ORDER BY k.ordinality)=r.columns,FALSE) AS ready
          FROM required r LEFT JOIN pg_index i ON i.indexrelid=to_regclass('market_data.'||r.index_name))
        SELECT bool_and(ready) FILTER(WHERE group_name='eod'),
               bool_and(ready) FILTER(WHERE group_name='vix'),
               bool_and(ready) FILTER(WHERE group_name='iti') FROM checks;
        """;

    /// <summary>GetCanonicalYieldCurvesForRebuild database statement.</summary>
    internal const string GetCanonicalYieldCurvesForRebuild =
        """
        SELECT valuedate,onemonth,twomonth,threemonth,sixmonth,oneyear,twoyear,threeyear,fiveyear,sevenyear,tenyear,
            twentyyear,thirtyyear
        FROM market_data.yield_curve_rates WHERE id=1 ORDER BY valuedate;
        """;

    /// <summary>Reads the immutable completed owner of one request hash.</summary>
    internal const string GetCompletedHistoricalDataLoadByRequestHash =
        """
        SELECT c.data_load_attempt_id, c.request_sha256, c.status, c.stage,
               c.provider_job_id, c.provider_file_id, c.batch_ordinal,
               c.source_position, c.error_message, c.updated_at_utc,
               NULLIF(m.manifest_json, ''), NULLIF(m.audit_json, '')
        FROM historical_data_load_checkpoint c
        INNER JOIN historical_data_load_manifest m
          ON m.data_load_attempt_id = c.data_load_attempt_id
        WHERE c.request_sha256 = $1 AND c.status = 2 AND m.manifest_json <> ''
        LIMIT 1;
        """;

    /// <summary>SQL for get composition preparation.</summary>
    internal const string GetCompositionPreparation =
        """
        SELECT payload FROM market_data.composition_preparation WHERE workflow_id=$1 AND input_revision=$2;
        """;

    /// <summary>SQL for get current futures eod data.</summary>
    internal const string GetCurrentFuturesEodData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", symbol AS "Symbol", openprice AS "OpenPrice",
            highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume",
            dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize", fiftydma AS "FiftyDMA",
            twohundreddma AS "TwoHundredDMA"
        FROM market_data.futures_eod_data
        WHERE valuedate <= $1 ORDER BY valuedate DESC, contractid ASC, symbol ASC LIMIT 1;
        """;

    /// <summary>SQL for get current futures eod data by date range.</summary>
    internal const string GetCurrentFuturesEodDataByDateRange =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", symbol AS "Symbol", openprice AS "OpenPrice",
            highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume",
            dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize", fiftydma AS "FiftyDMA",
            twohundreddma AS "TwoHundredDMA"
        FROM market_data.futures_eod_data
        WHERE valuedate >= $1 AND valuedate <= $2 ORDER BY valuedate DESC,contractid,symbol;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetEconomicCalendarCountryCodes =
        """
        SELECT countrycode AS "CountryCode"
        FROM market_data.economic_calendar_country_code
        WHERE lookupid = $1 ORDER BY countrycode COLLATE "C" LIMIT 512;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetEconomicCalendarV2ById =
        """
        SELECT eventdate AS "EventDate", countrycode AS "CountryCode", eventname AS "EventName", actual AS "Actual",
            forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change",
            changepercentage AS "ChangePercentage", createdon AS "CreatedOn", createdby AS "CreatedBy"
        FROM market_data.economic_calendar
        WHERE countrycode = $1 AND monthbucket = $2 AND eventdate >= date_trunc('second', $3::timestamptz)
            AND eventdate < date_trunc('second', $3::timestamptz) + INTERVAL '1 second' AND eventname = $4;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetEconomicCalendarV2CommandId =
        """
        SELECT commandid AS "CommandId"
        FROM market_data.economic_calendar
        WHERE countrycode = $1 AND monthbucket = $2 AND eventdate >= date_trunc('second', $3::timestamptz)
            AND eventdate < date_trunc('second', $3::timestamptz) + INTERVAL '1 second' AND eventname = $4;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetEconomicCalendars =
        """
        SELECT eventdate AS "EventDate", countrycode AS "CountryCode", eventname AS "EventName", actual AS "Actual",
            forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change",
            changepercentage AS "ChangePercentage", createdon AS "CreatedOn", createdby AS "CreatedBy"
        FROM market_data.economic_calendar
        WHERE countrycode = $1 AND monthbucket = $2 AND eventdate >= $3 AND eventdate <= $4
        ORDER BY eventdate DESC, eventname COLLATE "C" LIMIT 2501;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesBarData =
        """
        SELECT contractid AS "ContractId", symbol AS "Symbol", valuedate AS "ValueDate", bardate AS "BarDate",
            barratetype AS "BarRateType", barvalue AS "BarValue", uptrendtrigger AS "UpTrendTrigger",
            downtrendtrigger AS "DownTrendTrigger"
        FROM market_data.futures_bar_data
        WHERE contractid = $1 AND symbol = $2 AND valuedate = $3 AND bardate >= $4 AND bardate <= $5
        ORDER BY bardate DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesBarDataAll =
        """
        SELECT contractid AS "ContractId", symbol AS "Symbol", valuedate AS "ValueDate", bardate AS "BarDate",
            barratetype AS "BarRateType", barvalue AS "BarValue", uptrendtrigger AS "UpTrendTrigger",
            downtrendtrigger AS "DownTrendTrigger"
        FROM market_data.futures_bar_data ORDER BY contractid, symbol, valuedate, bardate DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesBarDataCount =
        """
        SELECT COUNT(*) AS "Value"
        FROM market_data.futures_bar_data
        WHERE contractid = $1 AND symbol = $2 AND valuedate = $3 GROUP BY contractid, symbol, valuedate;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesClosingPrice =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", closingprice AS "ClosingPrice",
            createdon AS "CreatedOn", createdby AS "CreatedBy"
        FROM market_data.futures_closing_price WHERE contractid = $1 AND valuedate = $2;
        """;

    /// <summary>SQL for get futures data id.</summary>
    internal const string GetFuturesDataId =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate"
        FROM market_data.futures_eod_data WHERE contractid = $1 AND valuedate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesEodClosingPrices =
        """
        SELECT symbol AS "Symbol", valuedate AS "ValueDate", closeprice AS "ClosingPrice"
        FROM market_data.futures_eod_data
        WHERE contractid = $1 AND valuedate >= $2 AND valuedate <= $3 ORDER BY valuedate DESC;
        """;

    /// <summary>SQL for get futures eod data.</summary>
    internal const string GetFuturesEodData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", symbol AS "Symbol", openprice AS "OpenPrice",
            highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume",
            dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize", fiftydma AS "FiftyDMA",
            twohundreddma AS "TwoHundredDMA"
        FROM market_data.futures_eod_data WHERE contractid = $1 AND valuedate = $2 LIMIT 1;
        """;

    /// <summary>SQL for get futures eod data all.</summary>
    internal const string GetFuturesEodDataAll =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", symbol AS "Symbol", openprice AS "OpenPrice",
            highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume",
            dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize", fiftydma AS "FiftyDMA",
            twohundreddma AS "TwoHundredDMA"
        FROM market_data.futures_eod_data;
        """;

    /// <summary>SQL for get futures eod data by date range.</summary>
    internal const string GetFuturesEodDataByDateRange =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", symbol AS "Symbol", openprice AS "OpenPrice",
            highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume",
            dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize", fiftydma AS "FiftyDMA",
            twohundreddma AS "TwoHundredDMA"
        FROM market_data.futures_eod_data
        WHERE contractid = $1 AND valuedate >= $2 AND valuedate <= $3 ORDER BY valuedate DESC, symbol ASC;
        """;

    /// <summary>SQL for get futures intra day data.</summary>
    internal const string GetFuturesIntraDayData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", sequenceid AS "SequenceId", symbol AS "Symbol",
            openprice AS "OpenPrice", highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice",
            volume AS "Volume", dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize"
        FROM market_data.futures_intra_day_data
        WHERE contractid = $1 AND valuedate = $2 ORDER BY sequenceid DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesItiSignalContractIdsByDate =
        """
        SELECT DISTINCT contractid AS "ContractId"
        FROM market_data.futures_iti_signal WHERE valuedate = $1 ORDER BY contractid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesItiSignals =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_signal
        WHERE contractid = $1 AND valuedate = $2 AND timeperiod = $3
        ORDER BY valuedate DESC, sequenceid DESC, timeperiod, intrinsictimemode, intrinsictimetrend, intrinsictimegroupid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesItiSignalsCanonicalByContract =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_signal
        WHERE contractid = $1
        ORDER BY valuedate DESC, sequenceid DESC, timeperiod, intrinsictimemode, intrinsictimetrend, intrinsictimegroupid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesItiSignalsCanonicalByContractDay =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_signal
        WHERE contractid = $1 AND valuedate = $2
        ORDER BY valuedate DESC, sequenceid DESC, timeperiod, intrinsictimemode, intrinsictimetrend, intrinsictimegroupid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesItiTimeFrameState =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", timeframestartvaluedate AS "TimeFrameStartValueDate",
            bandanchorprice AS "BandAnchorPrice", bandpercentage AS "BandPercentage", bandsize AS "BandSize",
            bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_timeframe_state
        WHERE contractid = $1 AND timeperiod = $2 AND calendarbucketstart = $3;
        """;

    /// <summary>SQL for get futures option chain quote data.</summary>
    internal const string GetFuturesOptionChainQuoteData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", tickid AS "TickId", ticktime AS "TickTime",
            optionprice AS "OptionPrice", bidprice AS "BidPrice", askprice AS "AskPrice", bidsize AS "BidSize",
            asksize AS "AskSize", impliedvolatility AS "ImpliedVolatility", underlyingprice AS "UnderlyingPrice",
            delta AS "Delta", gamma AS "Gamma", vega AS "Vega", theta AS "Theta", rho AS "Rho", volume AS "Volume",
            openinterest AS "OpenInterest", volumevaluedate AS "VolumeValueDate", openinterestvaluedate AS "OpenInterestValueDate"
        FROM market_data.futures_option_chain_quote_data
        WHERE underlyingcontractid = $1 AND expirydate = $2 AND valuedate = $3 ORDER BY contractid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesOptionTickPriceData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", tickid AS "TickId", ticktime AS "TickTime",
            optionprice AS "OptionPrice", bidprice AS "BidPrice", askprice AS "AskPrice", bidsize AS "BidSize",
            asksize AS "AskSize", impliedvolatility AS "ImpliedVolatility", underlyingprice AS "UnderlyingPrice",
            delta AS "Delta", gamma AS "Gamma", vega AS "Vega", theta AS "Theta", rho AS "Rho"
        FROM market_data.futures_option_tick_price_data WHERE contractid = $1 AND valuedate = $2 AND tickid = $3;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesRsiSignalsForTrend =
        """
        SELECT rsi AS "RSI"
        FROM market_data.futures_rsi_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3 AND valuedate = $4 AND timestamp >= $5 AND timestamp <= $6
        ORDER BY timestamp DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesTradeSignalAll =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", timestamp AS "Timestamp", mean AS "Mean", stddev AS "StdDev",
            futuresprice AS "FuturesPrice", pricechangepercent AS "PriceChangePercent",
            fundriskpercent AS "FundRiskPercent", rsi AS "RSI", rsislope AS "RSISlope", trendtype AS "TrendType",
            trendstrength AS "TrendStrength", tradesignal AS "TradeSignal", tdi AS "TDI", tdistrength AS "TDIStrength",
            mdi AS "MDI", mditrend AS "MDITrend", mdiuptrendlimit AS "MDIUpTrendLimit",
            mdidowntrendlimit AS "MDIDownTrendLimit", uptrendingtrigger AS "UpTrendingTrigger",
            downtrendingtrigger AS "DownTrendingTrigger", entrytrigger AS "EntryTrigger", exittrigger AS "ExitTrigger",
            trenddelta AS "TrendDelta", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            fiftydma AS "FiftyDMA", twohundreddma AS "TwoHundredDMA", tradeexecutestate AS "TradeExecuteState"
        FROM market_data.futures_trade_signal ORDER BY valuedate DESC, sequenceid DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetFuturesTradeSignalIdByValueDate =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod", sequenceid AS "SequenceId"
        FROM market_data.futures_trade_signal_lookup_by_scope
        WHERE scope = $1 ORDER BY sequenceid DESC, contractid;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    internal const string GetFuturesVwapSignalHistory =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", configurationid AS "ConfigurationId",
            sessionstartutc AS "SessionStartUtc", sessionendutc AS "SessionEndUtc", asofutc AS "AsOfUtc",
            cumulativepricevolume AS "CumulativePriceVolume", cumulativevolume AS "CumulativeVolume",
            eligibletradecount AS "EligibleTradeCount", rejectedtradecount AS "RejectedTradeCount",
            lastprice AS "LastPrice", vwap AS "Vwap", priceminusvwap AS "PriceMinusVwap",
            pricetovwappercent AS "PriceToVwapPercent", lasttradesourcesequence AS "LastTradeSourceSequence",
            streamepochid AS "StreamEpochId", lasttradeordinal AS "LastTradeOrdinal", iswarm AS "IsWarm",
            isvalid AS "IsValid", invalidreason AS "InvalidReason", istickexact AS "IsTickExact",
            calculationmethod AS "CalculationMethod", schemaversion AS "SchemaVersion", calculationversion AS "CalculationVersion"
        FROM market_data.futures_vwap_signal
        WHERE contractid = $1 AND valuedate = $2 AND configurationid = $3
        ORDER BY asofutc DESC, lasttradeordinal DESC;
        """;

    /// <summary>Creates or advances a checkpoint and stores its immutable terminal manifest.</summary>
    internal const string GetHistoricalAnalyticsAttemptProjection =
        """
        SELECT CASE WHEN e.payload::jsonb ->> 'Status' IN ('Completed', 'Failed')
                    THEN e.payload ELSE COALESCE(p.payload, e.payload) END
        FROM historical_analytics_attempt_projection e
        full JOIN historical_analytics_attempt_progress p USING (attempt_id)
        WHERE attempt_id = $1;
        """;

    /// <summary>Reads one exact attempt.</summary>
    internal const string GetHistoricalDataLoadByAttempt =
        """
        SELECT c.data_load_attempt_id, c.request_sha256, c.status, c.stage,
               c.provider_job_id, c.provider_file_id, c.batch_ordinal,
               c.source_position, c.error_message, c.updated_at_utc,
               NULLIF(m.manifest_json, ''), NULLIF(m.audit_json, '')
        FROM historical_data_load_checkpoint c
        LEFT JOIN historical_data_load_manifest m
          ON m.data_load_attempt_id = c.data_load_attempt_id
        WHERE c.data_load_attempt_id = $1;
        """;

    /// <summary>GetIndexedYieldCurvesForRebuild database statement.</summary>
    internal const string GetIndexedYieldCurvesForRebuild =
        """
        SELECT valuedate,onemonth,twomonth,threemonth,sixmonth,oneyear,twoyear,threeyear,fiveyear,sevenyear,tenyear,
            twentyyear,thirtyyear
        FROM market_data.yield_curve_rate_by_date WHERE lookupid=1 ORDER BY valuedate;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string GetLastFuturesAdxDailySignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            periodlength AS "PeriodLength", timestamp AS "Timestamp", futuresprice AS "FuturesPrice", plusdi AS "PlusDI",
            minusdi AS "MinusDI", adxvalue AS "AdxValue", adx AS "ADX", adxstrength AS "ADXStrength",
            configurationid AS "ConfigurationId", observationid AS "ObservationId", marketdataasof AS "MarketDataAsOf",
            sourcesequence AS "SourceSequence", calculationversion AS "CalculationVersion",
            calculationmethod AS "CalculationMethod", schemaversion AS "SchemaVersion", isvalid AS "IsValid", iswarm AS "IsWarm"
        FROM market_data.futures_adx_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3
        ORDER BY valuedate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string GetLastFuturesAdxSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            periodlength AS "PeriodLength", timestamp AS "Timestamp", futuresprice AS "FuturesPrice", plusdi AS "PlusDI",
            minusdi AS "MinusDI", adxvalue AS "AdxValue", adx AS "ADX", adxstrength AS "ADXStrength",
            configurationid AS "ConfigurationId", observationid AS "ObservationId", marketdataasof AS "MarketDataAsOf",
            sourcesequence AS "SourceSequence", calculationversion AS "CalculationVersion",
            calculationmethod AS "CalculationMethod", schemaversion AS "SchemaVersion", isvalid AS "IsValid", iswarm AS "IsWarm"
        FROM market_data.futures_adx_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3 AND valuedate = $4
        ORDER BY marketdataasof DESC nulls last, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string GetLastFuturesAtrSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            periodlength AS "PeriodLength", timestamp AS "Timestamp", futuresprice AS "FuturesPrice",
            atrvalue AS "AtrValue", truerange AS "TrueRange", atr AS "ATR", atrstrength AS "ATRStrength",
            previousatrvalue AS "PreviousAtrValue", atrbaseline AS "AtrBaseline", atrratio AS "AtrRatio",
            iswarm AS "IsWarm", configurationid AS "ConfigurationId", observationid AS "ObservationId",
            marketdataasof AS "MarketDataAsOf", sourcesequence AS "SourceSequence",
            calculationversion AS "CalculationVersion", calculationmethod AS "CalculationMethod",
            schemaversion AS "SchemaVersion", isvalid AS "IsValid"
        FROM market_data.futures_atr_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3 AND valuedate = $4
        ORDER BY marketdataasof DESC nulls last, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesBarData =
        """
        SELECT contractid AS "ContractId", symbol AS "Symbol", valuedate AS "ValueDate", bardate AS "BarDate",
            barratetype AS "BarRateType", barvalue AS "BarValue", uptrendtrigger AS "UpTrendTrigger",
            downtrendtrigger AS "DownTrendTrigger"
        FROM market_data.futures_bar_data
        WHERE contractid = $1 AND symbol = $2 AND valuedate = $3 ORDER BY bardate DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string GetLastFuturesDailyAtrSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            periodlength AS "PeriodLength", timestamp AS "Timestamp", futuresprice AS "FuturesPrice",
            atrvalue AS "AtrValue", truerange AS "TrueRange", atr AS "ATR", atrstrength AS "ATRStrength",
            previousatrvalue AS "PreviousAtrValue", atrbaseline AS "AtrBaseline", atrratio AS "AtrRatio",
            iswarm AS "IsWarm", configurationid AS "ConfigurationId", observationid AS "ObservationId",
            marketdataasof AS "MarketDataAsOf", sourcesequence AS "SourceSequence",
            calculationversion AS "CalculationVersion", calculationmethod AS "CalculationMethod",
            schemaversion AS "SchemaVersion", isvalid AS "IsValid"
        FROM market_data.futures_atr_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3
        ORDER BY valuedate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>SQL for get last futures eod data.</summary>
    internal const string GetLastFuturesEodData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", symbol AS "Symbol", openprice AS "OpenPrice",
            highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume",
            dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize", fiftydma AS "FiftyDMA",
            twohundreddma AS "TwoHundredDMA"
        FROM market_data.futures_eod_data
        WHERE contractid = $1 AND valuedate < $2 ORDER BY valuedate DESC, symbol ASC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesItiSignalByTimePeriod =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_signal
        WHERE contractid = $1 AND valuedate = $2 AND timeperiod = $3 ORDER BY sequenceid DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string GetLastFuturesMacdDailySignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            signalemaperiod AS "SignalEmaPeriod", fastemaperiod AS "FastEmaPeriod", slowemaperiod AS "SlowEmaPeriod",
            timestamp AS "Timestamp", futuresprice AS "FuturesPrice", fastema AS "FastEma", slowema AS "SlowEma",
            macdline AS "MacdLine", signalline AS "SignalLine", histogram AS "Histogram", macd AS "MACD",
            macdstrength AS "MACDStrength", iswarm AS "IsWarm", observationcount AS "ObservationCount",
            configurationid AS "ConfigurationId", observationid AS "ObservationId", marketdataasof AS "MarketDataAsOf",
            sourcesequence AS "SourceSequence", calculationversion AS "CalculationVersion",
            calculationmethod AS "CalculationMethod", schemaversion AS "SchemaVersion", isvalid AS "IsValid"
        FROM market_data.futures_macd_signal
        WHERE contractid = $1 AND timeperiod = $2 AND signalemaperiod = $3 AND fastemaperiod = $4 AND slowemaperiod = $5
        ORDER BY valuedate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    internal const string GetLastFuturesMacdSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            signalemaperiod AS "SignalEmaPeriod", fastemaperiod AS "FastEmaPeriod", slowemaperiod AS "SlowEmaPeriod",
            timestamp AS "Timestamp", futuresprice AS "FuturesPrice", fastema AS "FastEma", slowema AS "SlowEma",
            macdline AS "MacdLine", signalline AS "SignalLine", histogram AS "Histogram", macd AS "MACD",
            macdstrength AS "MACDStrength", iswarm AS "IsWarm", observationcount AS "ObservationCount",
            configurationid AS "ConfigurationId", observationid AS "ObservationId", marketdataasof AS "MarketDataAsOf",
            sourcesequence AS "SourceSequence", calculationversion AS "CalculationVersion",
            calculationmethod AS "CalculationMethod", schemaversion AS "SchemaVersion", isvalid AS "IsValid"
        FROM market_data.futures_macd_signal
        WHERE contractid = $1 AND timeperiod = $2 AND signalemaperiod = $3 AND fastemaperiod = $4 AND slowemaperiod = $5
            AND valuedate = $6
        ORDER BY marketdataasof DESC nulls last, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesOptionTickPriceData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", tickid AS "TickId", ticktime AS "TickTime",
            optionprice AS "OptionPrice", bidprice AS "BidPrice", askprice AS "AskPrice", bidsize AS "BidSize",
            asksize AS "AskSize", impliedvolatility AS "ImpliedVolatility", underlyingprice AS "UnderlyingPrice",
            delta AS "Delta", gamma AS "Gamma", vega AS "Vega", theta AS "Theta", rho AS "Rho"
        FROM market_data.futures_option_tick_price_data
        WHERE contractid = $1 AND valuedate = $2 ORDER BY tickid DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesRsiDailySignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            periodlength AS "PeriodLength", timestamp AS "Timestamp", price AS "FuturesPrice",
            pricechange AS "PriceChange", pricegain AS "PriceGain", priceloss AS "PriceLoss",
            averagepricegain AS "AveragePriceGain", averagepriceloss AS "AveragePriceLoss", rs AS "RS", rsi AS "RSI",
            rsiaverage AS "RSIAverage", rsislope AS "RSISlope", sourcesequence AS "SourceSequence",
            sourceeventtimestamp AS "SourceEventTimestamp", configurationid AS "ConfigurationId",
            observationid AS "ObservationId", marketdataasof AS "MarketDataAsOf",
            calculationversion AS "CalculationVersion", calculationmethod AS "CalculationMethod",
            schemaversion AS "SchemaVersion", isvalid AS "IsValid", previousrsi AS "PreviousRsi",
            regimeslope AS "RegimeSlope", iswarm AS "IsWarm"
        FROM market_data.futures_rsi_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3
        ORDER BY valuedate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesRsiSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            periodlength AS "PeriodLength", timestamp AS "Timestamp", price AS "FuturesPrice",
            pricechange AS "PriceChange", pricegain AS "PriceGain", priceloss AS "PriceLoss",
            averagepricegain AS "AveragePriceGain", averagepriceloss AS "AveragePriceLoss", rs AS "RS", rsi AS "RSI",
            rsiaverage AS "RSIAverage", rsislope AS "RSISlope", sourcesequence AS "SourceSequence",
            sourceeventtimestamp AS "SourceEventTimestamp", configurationid AS "ConfigurationId",
            observationid AS "ObservationId", marketdataasof AS "MarketDataAsOf",
            calculationversion AS "CalculationVersion", calculationmethod AS "CalculationMethod",
            schemaversion AS "SchemaVersion", isvalid AS "IsValid", previousrsi AS "PreviousRsi",
            regimeslope AS "RegimeSlope", iswarm AS "IsWarm"
        FROM market_data.futures_rsi_signal
        WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3 AND valuedate = $4
        ORDER BY marketdataasof DESC nulls last, timestamp DESC LIMIT 1;
        """;

    /// <summary>SQL for get last futures tdi signal.</summary>
    internal const string GetLastFuturesTdiSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            timestamp AS "Timestamp", schemaversion AS "SchemaVersion", configurationid AS "ConfigurationId",
            rsiperiod AS "RsiPeriod", pricelineperiod AS "PriceLinePeriod", signallineperiod AS "SignalLinePeriod",
            marketbaseperiod AS "MarketBasePeriod", volatilitybandperiod AS "VolatilityBandPeriod",
            volatilitybanddeviation AS "VolatilityBandDeviation", price AS "Price", rsi AS "Rsi",
            priceline AS "PriceLine", signalline AS "SignalLine", marketbaseline AS "MarketBaseLine",
            uppervolatilityband AS "UpperVolatilityBand", lowervolatilityband AS "LowerVolatilityBand",
            bandwidth AS "BandWidth", pricesignaldivergence AS "PriceSignalDivergence", crosstype AS "CrossType",
            marketstate AS "MarketState", trenddirection AS "TrendDirection", trendstrength AS "TrendStrength",
            sourcesequence AS "SourceSequence", sourceeventtimestamp AS "SourceEventTimestamp"
        FROM market_data.futures_traders_dynamic_index_signal
        WHERE contractid = $1 AND timeperiod = $2 AND configurationid = $3 AND valuedate = $4
        ORDER BY sourceeventtimestamp DESC nulls last, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesTradeSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod", sequenceid AS "SequenceId"
        FROM market_data.futures_trade_signal_lookup_by_scope WHERE scope = $1 ORDER BY sequenceid DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesTradeSignalById =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", timestamp AS "Timestamp", mean AS "Mean", stddev AS "StdDev",
            futuresprice AS "FuturesPrice", pricechangepercent AS "PriceChangePercent",
            fundriskpercent AS "FundRiskPercent", rsi AS "RSI", rsislope AS "RSISlope", trendtype AS "TrendType",
            trendstrength AS "TrendStrength", tradesignal AS "TradeSignal", tdi AS "TDI", tdistrength AS "TDIStrength",
            mdi AS "MDI", mditrend AS "MDITrend", mdiuptrendlimit AS "MDIUpTrendLimit",
            mdidowntrendlimit AS "MDIDownTrendLimit", uptrendingtrigger AS "UpTrendingTrigger",
            downtrendingtrigger AS "DownTrendingTrigger", entrytrigger AS "EntryTrigger", exittrigger AS "ExitTrigger",
            trenddelta AS "TrendDelta", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            fiftydma AS "FiftyDMA", twohundreddma AS "TwoHundredDMA", tradeexecutestate AS "TradeExecuteState"
        FROM market_data.futures_trade_signal
        WHERE contractid = $1 AND valuedate = $2 AND timeperiod = $3 ORDER BY sequenceid DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLastFuturesTradeSignalBySymbol =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", timestamp AS "Timestamp", mean AS "Mean", stddev AS "StdDev",
            futuresprice AS "FuturesPrice", pricechangepercent AS "PriceChangePercent",
            fundriskpercent AS "FundRiskPercent", rsi AS "RSI", rsislope AS "RSISlope", trendtype AS "TrendType",
            trendstrength AS "TrendStrength", tradesignal AS "TradeSignal", tdi AS "TDI", tdistrength AS "TDIStrength",
            mdi AS "MDI", mditrend AS "MDITrend", mdiuptrendlimit AS "MDIUpTrendLimit",
            mdidowntrendlimit AS "MDIDownTrendLimit", uptrendingtrigger AS "UpTrendingTrigger",
            downtrendingtrigger AS "DownTrendingTrigger", entrytrigger AS "EntryTrigger", exittrigger AS "ExitTrigger",
            trenddelta AS "TrendDelta", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            fiftydma AS "FiftyDMA", twohundreddma AS "TwoHundredDMA", tradeexecutestate AS "TradeExecuteState"
        FROM market_data.futures_trade_signal
        WHERE contractid = ANY($1) AND valuedate = $2 ORDER BY sequenceid DESC LIMIT 1;
        """;

    /// <summary>SQL for get last rate of return.</summary>
    internal const string GetLastRateOfReturn =
        """
        SELECT symbol AS "Symbol", valuedate AS "ValueDate", rateofreturn AS "RateOfReturn"
        FROM market_data.rate_of_return WHERE symbol = $1 ORDER BY valuedate DESC LIMIT 1;
        """;

    /// <summary>SQL for get last vix futures eod data.</summary>
    internal const string GetLastVixFuturesEodData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", openprice AS "OpenPrice", highprice AS "HighPrice",
            lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume"
        FROM market_data.vix_futures_eod_data
        WHERE contractid = $1 AND valuedate <= $2 ORDER BY valuedate DESC LIMIT 1;
        """;

    /// <summary>SQL for get last yield curve rate.</summary>
    internal const string GetLastYieldCurveRate =
        """
        SELECT valuedate AS "ValueDate", onemonth AS "OneMonth", twomonth AS "TwoMonth", threemonth AS "ThreeMonth",
            sixmonth AS "SixMonth", oneyear AS "OneYear", twoyear AS "TwoYear", threeyear AS "ThreeYear",
            fiveyear AS "FiveYear", sevenyear AS "SevenYear", tenyear AS "TenYear", twentyyear AS "TwentyYear",
            thirtyyear AS "ThirtyYear"
        FROM market_data.yield_curve_rate_by_date WHERE lookupid = 1 ORDER BY valuedate DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    internal const string GetLatestFuturesBollingerBandSignal =
        """
        SELECT serieskey AS "SeriesKey", timeperiod AS "TimePeriod", configurationid AS "ConfigurationId",
            marketdataasof AS "MarketDataAsOf", observationid AS "ObservationId", contractid AS "ContractId",
            valuedate AS "ValueDate", price AS "Price", ema10center AS "Ema10Center",
            standarddeviation10 AS "StandardDeviation10", upper10 AS "Upper10", lower10 AS "Lower10",
            width10 AS "Width10", position10 AS "Position10", ema20center AS "Ema20Center",
            standarddeviation20 AS "StandardDeviation20", upper20 AS "Upper20", lower20 AS "Lower20",
            width20 AS "Width20", position20 AS "Position20", width20baseline AS "Width20Baseline",
            width20ratio AS "Width20Ratio", iswarm AS "IsWarm", sourcesequence AS "SourceSequence",
            calculatedat AS "CalculatedAt", schemaversion AS "SchemaVersion", calculationversion AS "CalculationVersion",
            calculationmethod AS "CalculationMethod", isvalid AS "IsValid"
        FROM market_data.futures_bollinger_band_signal
        WHERE serieskey = $1 AND timeperiod = $2 AND configurationid = $3 AND yearmonth = $4 AND marketdataasof <= $5
        ORDER BY marketdataasof DESC, observationid ASC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    internal const string GetLatestFuturesEmaSignal =
        """
        SELECT serieskey AS "SeriesKey", timeperiod AS "TimePeriod", configurationid AS "ConfigurationId",
            marketdataasof AS "MarketDataAsOf", observationid AS "ObservationId", contractid AS "ContractId",
            valuedate AS "ValueDate", price AS "Price", ema10 AS "Ema10", previousema10 AS "PreviousEma10",
            ema10slope AS "Ema10Slope", ema20 AS "Ema20", previousema20 AS "PreviousEma20", ema20slope AS "Ema20Slope",
            ema50 AS "Ema50", previousema50 AS "PreviousEma50", ema50slope AS "Ema50Slope", ema200 AS "Ema200",
            previousema200 AS "PreviousEma200", ema200slope AS "Ema200Slope", iswarm AS "IsWarm",
            sourcesequence AS "SourceSequence", calculatedat AS "CalculatedAt", schemaversion AS "SchemaVersion",
            calculationversion AS "CalculationVersion", calculationmethod AS "CalculationMethod", isvalid AS "IsValid"
        FROM market_data.futures_ema_signal
        WHERE serieskey = $1 AND timeperiod = $2 AND configurationid = $3 AND yearmonth = $4 AND marketdataasof <= $5
        ORDER BY marketdataasof DESC, observationid ASC LIMIT 1;
        """;

    /// <summary>SQL for get latest futures option chain quote data.</summary>
    internal const string GetLatestFuturesOptionChainQuoteData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", tickid AS "TickId", ticktime AS "TickTime",
            optionprice AS "OptionPrice", bidprice AS "BidPrice", askprice AS "AskPrice", bidsize AS "BidSize",
            asksize AS "AskSize", impliedvolatility AS "ImpliedVolatility", underlyingprice AS "UnderlyingPrice",
            delta AS "Delta", gamma AS "Gamma", vega AS "Vega", theta AS "Theta", rho AS "Rho", volume AS "Volume",
            openinterest AS "OpenInterest", volumevaluedate AS "VolumeValueDate", openinterestvaluedate AS "OpenInterestValueDate"
        FROM market_data.futures_option_chain_quote_data
        WHERE underlyingcontractid=$1 AND expirydate=$2 AND tickid <= $4
          AND bidprice > 0 AND askprice >= bidprice
          AND valuedate=(SELECT MAX(valuedate) FROM market_data.futures_option_chain_quote_data
              WHERE underlyingcontractid=$1 AND expirydate=$2 AND valuedate <= $3
                AND tickid <= $4 AND bidprice > 0 AND askprice >= bidprice)
        ORDER BY contractid;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    internal const string GetLatestFuturesVwapSignal =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", configurationid AS "ConfigurationId",
            sessionstartutc AS "SessionStartUtc", sessionendutc AS "SessionEndUtc", asofutc AS "AsOfUtc",
            cumulativepricevolume AS "CumulativePriceVolume", cumulativevolume AS "CumulativeVolume",
            eligibletradecount AS "EligibleTradeCount", rejectedtradecount AS "RejectedTradeCount",
            lastprice AS "LastPrice", vwap AS "Vwap", priceminusvwap AS "PriceMinusVwap",
            pricetovwappercent AS "PriceToVwapPercent", lasttradesourcesequence AS "LastTradeSourceSequence",
            streamepochid AS "StreamEpochId", lasttradeordinal AS "LastTradeOrdinal", iswarm AS "IsWarm",
            isvalid AS "IsValid", invalidreason AS "InvalidReason", istickexact AS "IsTickExact",
            calculationmethod AS "CalculationMethod", schemaversion AS "SchemaVersion", calculationversion AS "CalculationVersion"
        FROM market_data.futures_vwap_signal
        WHERE contractid = $1 AND valuedate = $2 AND configurationid = $3
        ORDER BY asofutc DESC, lasttradeordinal DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetLatestFuturesVxTermStructureSignal =
        """
        SELECT valuedate AS "ValueDate", configurationid AS "ConfigurationId", frontcontractid AS "FrontContractId",
            frontexpiry AS "FrontExpiry", frontprice AS "FrontPrice", backcontractid AS "BackContractId",
            backexpiry AS "BackExpiry", backprice AS "BackPrice", frontbackspread AS "FrontBackSpread",
            frontbackratio AS "FrontBackRatio", termstructurepercent AS "TermStructurePercent",
            termstructurestate AS "TermStructureState", priorfrontbackratio AS "PriorFrontBackRatio",
            priortermstructurepercent AS "PriorTermStructurePercent", frontsourcetimestamp AS "FrontSourceTimestamp",
            backsourcetimestamp AS "BackSourceTimestamp", frontsourcesequence AS "FrontSourceSequence",
            backsourcesequence AS "BackSourceSequence", calculatedat AS "CalculatedAt", iswarm AS "IsWarm",
            isvalid AS "IsValid", schemaversion AS "SchemaVersion", calculationversion AS "CalculationVersion"
        FROM market_data.futures_vx_term_structure_signal
        WHERE valuedate = $1 AND configurationid = $2
        ORDER BY calculatedat DESC, frontsourcesequence DESC, backsourcesequence DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetMarketHolidays =
        """
        SELECT currencytype AS "CurrencyType", holidaydate AS "HolidayDate", description AS "Description"
        FROM market_data.market_holiday WHERE currencytype = $1 ORDER BY holidaydate ASC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetMarketHolidaysByDateRange =
        """
        SELECT currencytype AS "CurrencyType", holidaydate AS "HolidayDate", description AS "Description"
        FROM market_data.market_holiday
        WHERE currencytype = $1 AND holidaydate >= $2 AND holidaydate <= $3 ORDER BY holidaydate ASC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetMarketOutlookSnapshot =
        """
        SELECT snapshot AS "Snapshot", contractid AS "ContractId", valuedate AS "ValueDate", revision AS "Revision",
            updatedon AS "UpdatedOn", eoddata AS "EodData", futurestradesignal AS "FuturesTradeSignal",
            missinginputs AS "MissingInputs"
        FROM market_data.market_outlook_snapshot
        WHERE contractid = $1 AND valuedate <= $2 ORDER BY valuedate DESC LIMIT 1;
        """;

    /// <summary>Indexed canonical ITI observation query.</summary>
    internal const string GetNativeItiDayMode =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_signal
        WHERE contractid = $1 AND valuedate = $2 AND intrinsictimemode = $3 AND sequenceid > $4
        ORDER BY sequenceid DESC, timeperiod, intrinsictimetrend, intrinsictimegroupid;
        """;

    /// <summary>Indexed canonical ITI observation query.</summary>
    internal const string GetNativeItiLatestTrendMode =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_signal
        WHERE contractid = $1 AND valuedate <= $2 AND intrinsictimetrend = $3 AND intrinsictimemode = $4
        ORDER BY valuedate DESC, sequenceid DESC, timeperiod, intrinsictimegroupid LIMIT 1;
        """;

    /// <summary>Indexed canonical ITI observation query.</summary>
    internal const string GetNativeItiRange =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timeperiod AS "TimePeriod",
            sequenceid AS "SequenceId", intrinsictime AS "IntrinsicTime", intrinsictimegroupid AS "IntrinsicTimeGroupId",
            intrinsictimelength AS "IntrinsicTimeLength", intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend", intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice", trendextreme AS "TrendExtreme", trendreversal AS "TrendReversal",
            trenddelta AS "TrendDelta", targetdelta AS "TargetDelta", lambda AS "Lambda", tradingdays AS "TradingDays",
            threshold AS "Threshold", uptrendtrigger AS "UpTrendTrigger", downtrendtrigger AS "DownTrendTrigger",
            tradestate AS "TradeState", bandlevel AS "BandLevel", reversallevel AS "ReversalLevel"
        FROM market_data.futures_iti_signal
        WHERE contractid = ANY($1) AND valuedate >= $2 AND valuedate <= $3
        ORDER BY valuedate, sequenceid, contractid, timeperiod, intrinsictimemode, intrinsictimetrend, intrinsictimegroupid;
        """;

    /// <summary>SQL for get normal curve data.</summary>
    internal const string GetNormalCurveData =
        """
        SELECT stddevindex,percent FROM market_data.normal_curve_data ORDER BY stddevindex;
        """;

    /// <summary>GetOptionChainParameterVersion database statement.</summary>
    internal const string GetOptionChainParameterVersion =
        """
        SELECT parameter_hash
        FROM market_data.strategy_option_chain_policy_version WHERE set_id=$1 AND version=$2;
        """;

    /// <summary>SQL for get option chain statistics.</summary>
    internal const string GetOptionChainStatistics =
        """
        WITH scope AS (
            SELECT contractid, valuedate, volume, volumevaluedate, openinterest, openinterestvaluedate
            FROM market_data.futures_option_chain_quote_data
            WHERE underlyingcontractid = $1 AND expirydate = $2 AND valuedate <= $3
        ), volumes AS (
            SELECT DISTINCT ON (contractid) contractid, volume, volumevaluedate FROM scope
            WHERE volume IS NOT NULL AND volume >= 0 AND volumevaluedate IS NOT NULL AND volumevaluedate <= $3
            ORDER BY contractid, volumevaluedate DESC, valuedate DESC
        ), interest AS (
            SELECT DISTINCT ON (contractid) contractid, openinterest, openinterestvaluedate FROM scope
            WHERE openinterest IS NOT NULL AND openinterest >= 0 AND openinterestvaluedate IS NOT NULL
            AND openinterestvaluedate <= $3
            ORDER BY contractid, openinterestvaluedate DESC, valuedate DESC
        )
        SELECT COALESCE(v.contractid, i.contractid), v.volume, v.volumevaluedate, i.openinterest, i.openinterestvaluedate
        FROM volumes v full OUTER JOIN interest i ON v.contractid = i.contractid;
        """;

    /// <summary>GetOptionIvMetricHistory database statement.</summary>
    internal const string GetOptionIvMetricHistory =
        """
        WITH ranked AS (
            SELECT value_date,sampling_slot,payload,
                row_number() OVER (PARTITION BY value_date,sampling_slot ORDER BY revision DESC,publication_sequence DESC,
                snapshot_id) AS ordinal
            FROM market_data.option_iv_metric_history
            WHERE environment=$1 AND series_id=$2 AND methodology_version=$3 AND metric_policy_version=$4
            AND calendar_bucket=$5 AND value_date>=$6 AND value_date<=$7
                AND ($8::text IS NULL OR sampling_slot=$8)
                AND ($9::timestamptz IS NULL OR available_at_utc<=$9)
        )
        SELECT value_date,sampling_slot,payload FROM ranked
        WHERE ordinal=1 AND ($10::date IS NULL OR (value_date,sampling_slot COLLATE "C")>($10,$11::text COLLATE "C"))
        ORDER BY value_date,sampling_slot COLLATE "C" LIMIT $12;
        """;

    /// <summary>GetOptionIvObservationHistory database statement.</summary>
    internal const string GetOptionIvObservationHistory =
        """
        WITH ranked AS (
            SELECT value_date,sampling_slot,payload,
                row_number() OVER (PARTITION BY value_date,sampling_slot ORDER BY revision DESC,observation_id) AS ordinal
            FROM market_data.option_iv_observation_history
            WHERE environment=$1 AND series_id=$2 AND methodology_version=$3 AND calendar_bucket=$4 AND value_date>=$5
            AND value_date<=$6
                AND ($7::text IS NULL OR sampling_slot=$7)
                AND ($8::timestamptz IS NULL OR available_at_utc<=$8)
        )
        SELECT value_date,sampling_slot,payload FROM ranked
        WHERE ordinal=1 AND ($9::date IS NULL OR (value_date,sampling_slot COLLATE "C")>($9,$10::text COLLATE "C"))
        ORDER BY value_date,sampling_slot COLLATE "C" LIMIT $11;
        """;

    /// <summary>SQL for get option trade evidence.</summary>
    internal const string GetOptionTradeEvidence =
        """
        SELECT source_digest,payload
        FROM market_data.option_trade_evidence WHERE contract_id=$1 AND value_date=$2 AND source_id=$3;
        """;

    /// <summary>SQL for get option trade evidence batch.</summary>
    internal const string GetOptionTradeEvidenceBatch =
        """
        SELECT stored.contract_id,stored.value_date,stored.source_id,stored.source_digest,stored.payload
        FROM unnest($1::text[],$2::date[],$3::text[]) AS requested(contract_id,value_date,source_id)
        JOIN market_data.option_trade_evidence stored USING (contract_id,value_date,source_id);
        """;

    /// <summary>SQL for get qualified indicator seed bars.</summary>
    internal const string GetQualifiedIndicatorSeedBars =
        """
        WITH canonical AS (
            SELECT DISTINCT ON (marketdataasof) marketdataasof, observationid, intervalstart, intervalend,
                   openprice, highprice, lowprice, closeprice, volume, tradecount, pricevolumesum,
                   firstsourcesequence, lastsourcesequence, firstmarketevent, lastmarketevent,
                   calculatedat, schemaversion, calculationversion
            FROM market_data.futures_trade_session_bar
            WHERE serieskey=$1 AND timeperiod=$2 AND yearmonth=$3 AND marketdataasof=ANY($4)
                AND contractid=$5 AND valuedate=$6 AND iscomplete AND isvalid
                AND calculationversion NOT IN ('databento-historical-minute-seed-v1','databento-historical-trade-seed-v1',
                'databento-four-hour-trade-replay-v1')
            ORDER BY marketdataasof,calculatedat DESC,observationid
        )
        SELECT observationid,intervalstart,intervalend,openprice,highprice,lowprice,closeprice,
               volume,tradecount,pricevolumesum,firstsourcesequence,lastsourcesequence,
               firstmarketevent,lastmarketevent,calculatedat,schemaversion,calculationversion,NULL::bytea
        FROM canonical
        UNION ALL
        SELECT NULL,NULL,seed.interval_end,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,seed.payload
        FROM market_data.indicator_seed_coverage seed
        WHERE seed.contract_id=$5 AND seed.time_frame=$2 AND seed.value_date=$6
            AND seed.source_version=$7 AND seed.interval_end=ANY($4)
            AND NOT EXISTS (SELECT 1 FROM canonical WHERE marketdataasof=seed.interval_end);
        """;

    /// <summary>SQL for get raw eod.</summary>
    internal const string GetRawEod =
        """
        SELECT serieskey, contractid, valuedate, sessionstart, sessionend, openprice, highprice, lowprice, closeprice,
            volume, tradecount, pricevolumesum, observationid, firstsourcesequence, lastsourcesequence, firstmarketevent,
            lastmarketevent, schemaversion, iscomplete, isvalid
        FROM market_data.futures_eod_observation
        WHERE serieskey = $1 AND yearmonth = $2 AND valuedate = $3 ORDER BY valuedate,contractid;
        """;

    /// <summary>SQL for get raw eod range.</summary>
    internal const string GetRawEodRange =
        """
        SELECT serieskey, contractid, valuedate, sessionstart, sessionend, openprice, highprice, lowprice, closeprice,
            volume, tradecount, pricevolumesum, observationid, firstsourcesequence, lastsourcesequence, firstmarketevent,
            lastmarketevent, schemaversion, iscomplete, isvalid
        FROM market_data.futures_eod_observation
        WHERE serieskey = $1 AND yearmonth = $2 AND valuedate >= $3 AND valuedate <= $4
        ORDER BY valuedate,contractid;
        """;

    /// <summary>Reads a complete bar under its exact indexed series/frame/month/end identity.</summary>
    internal const string GetStoredTradeSessionBar =
        """
        SELECT observationid,intervalstart,intervalend,openprice,highprice,lowprice,closeprice,
               volume,tradecount,pricevolumesum,firstsourcesequence,lastsourcesequence,
               firstmarketevent,lastmarketevent,calculatedat,schemaversion,calculationversion
        FROM market_data.futures_trade_session_bar
        WHERE serieskey=$1 AND timeperiod=$2 AND yearmonth=$3 AND marketdataasof=$4
            AND contractid=$5 AND valuedate=$6 AND iscomplete AND isvalid
            AND calculationversion NOT IN ('databento-historical-minute-seed-v1','databento-historical-trade-seed-v1',
            'databento-four-hour-trade-replay-v1')
        ORDER BY calculatedat DESC,observationid LIMIT 1;
        """;

    /// <summary>Reads bounded exact interval keys, retaining the singular lookup's latest-observation ordering.</summary>
    internal const string GetStoredTradeSessionBars =
        """
        SELECT DISTINCT ON (marketdataasof) observationid,intervalstart,intervalend,openprice,highprice,lowprice,closeprice,
               volume,tradecount,pricevolumesum,firstsourcesequence,lastsourcesequence,
               firstmarketevent,lastmarketevent,calculatedat,schemaversion,calculationversion
        FROM market_data.futures_trade_session_bar
        WHERE serieskey=$1 AND timeperiod=$2 AND yearmonth=$3 AND marketdataasof=ANY($4)
            AND contractid=$5 AND valuedate=$6 AND iscomplete AND isvalid
            AND calculationversion NOT IN ('databento-historical-minute-seed-v1','databento-historical-trade-seed-v1',
            'databento-four-hour-trade-replay-v1')
        ORDER BY marketdataasof,calculatedat DESC,observationid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetTradeLiveFeed =
        """
        SELECT orderid, tradeid, tradelivefeedstate
        FROM market_data.trade_live_feed WHERE orderid = $1 AND tradeid = $2;
        """;

    /// <summary>SQL for get vix futures eod data.</summary>
    internal const string GetVixFuturesEodData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", openprice AS "OpenPrice", highprice AS "HighPrice",
            lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume"
        FROM market_data.vix_futures_eod_data WHERE contractid = $1 AND valuedate = $2 LIMIT 1;
        """;

    /// <summary>SQL for get vix futures eod data by value date.</summary>
    internal const string GetVixFuturesEodDataByValueDate =
        """
        SELECT contractid,valuedate,openprice,highprice,lowprice,closeprice,volume
        FROM market_data.vix_futures_eod_data WHERE valuedate=$1 ORDER BY contractid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string GetYesterdaysFuturesClosingPrice =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", closingprice AS "ClosingPrice",
            createdon AS "CreatedOn", createdby AS "CreatedBy"
        FROM market_data.futures_closing_price
        WHERE contractid = $1 AND valuedate < $2 ORDER BY valuedate DESC LIMIT 1;
        """;

    /// <summary>SQL for get yesterdays futures eod data.</summary>
    internal const string GetYesterdaysFuturesEodData =
        """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", symbol AS "Symbol", openprice AS "OpenPrice",
            highprice AS "HighPrice", lowprice AS "LowPrice", closeprice AS "ClosePrice", volume AS "Volume",
            dailypercentchange AS "DailyPercentChange", dailystddev AS "DailyStdDev",
            dailystddevamount AS "DailyStdDevAmount", upperband AS "UpperBand", mean AS "Mean", lowerband AS "LowerBand",
            marketdirection AS "MarketDirection", marketvolatility AS "MarketVolatility",
            pricedirection AS "PriceDirection", pricevolatility AS "PriceVolatility",
            marketdirectionindicator AS "MarketDirectionIndicator", windowsize AS "WindowSize", fiftydma AS "FiftyDMA",
            twohundreddma AS "TwoHundredDMA"
        FROM market_data.futures_eod_data
        WHERE contractid = $1 AND valuedate < $2 ORDER BY valuedate DESC LIMIT 1;
        """;

    /// <summary>GetYieldCurveIndexYears database statement.</summary>
    internal const string GetYieldCurveIndexYears =
        """
        SELECT rateyear FROM market_data.yield_curve_rate_year WHERE lookupid=1 ORDER BY rateyear;
        """;

    /// <summary>SQL for get yield curve rate.</summary>
    internal const string GetYieldCurveRate =
        """
        SELECT valuedate AS "ValueDate", onemonth AS "OneMonth", twomonth AS "TwoMonth", threemonth AS "ThreeMonth",
            sixmonth AS "SixMonth", oneyear AS "OneYear", twoyear AS "TwoYear", threeyear AS "ThreeYear",
            fiveyear AS "FiveYear", sevenyear AS "SevenYear", tenyear AS "TenYear", twentyyear AS "TwentyYear",
            thirtyyear AS "ThirtyYear"
        FROM market_data.yield_curve_rate_by_date WHERE lookupid = 1 AND valuedate = $1;
        """;

    /// <summary>SQL for get yield curve rate years.</summary>
    internal const string GetYieldCurveRateYears =
        """
        SELECT rateyear AS "RateYear"
        FROM market_data.yield_curve_rate_year WHERE lookupid = $1 ORDER BY rateyear DESC LIMIT 200;
        """;

    /// <summary>SQL for get yield curve rates.</summary>
    internal const string GetYieldCurveRates =
        """
        SELECT valuedate AS "ValueDate", onemonth AS "OneMonth", twomonth AS "TwoMonth", threemonth AS "ThreeMonth",
            sixmonth AS "SixMonth", oneyear AS "OneYear", twoyear AS "TwoYear", threeyear AS "ThreeYear",
            fiveyear AS "FiveYear", sevenyear AS "SevenYear", tenyear AS "TenYear", twentyyear AS "TwentyYear",
            thirtyyear AS "ThirtyYear"
        FROM market_data.yield_curve_rate_by_date
        WHERE lookupid = 1 AND valuedate >= $1 AND valuedate <= $2 ORDER BY valuedate DESC LIMIT 5000;
        """;

    /// <summary>Reads event stream id rows.</summary>
    internal const string HasMarketDataBaseline =
        """
        SELECT currentversion FROM event_stream_id WHERE eventstream = $1 AND currentversion > 0;
        """;

    /// <summary>SQL for insert composition preparation.</summary>
    internal const string InsertCompositionPreparation =
        """
        INSERT INTO market_data.composition_preparation(workflow_id,input_revision,payload)
        VALUES($1,$2,$3) ON CONFLICT (workflow_id,input_revision) DO NOTHING;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertEconomicCalendarCountryCode =
        """
        INSERT INTO market_data.economic_calendar_country_code (lookupid, countrycode)
        VALUES ($1, $2) ON CONFLICT (lookupid, countrycode) DO NOTHING;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertEconomicCalendarV2 =
        """
        INSERT INTO market_data.economic_calendar (countrycode, monthbucket, eventdate, eventname, actual, forecast,
            prior, impact, unit, change, changepercentage, createdon, createdby, commandid)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
        ON CONFLICT (countrycode, monthbucket, eventdate, eventname) DO UPDATE SET actual = EXCLUDED.actual,
            forecast = EXCLUDED.forecast, prior = EXCLUDED.prior, impact = EXCLUDED.impact, unit = EXCLUDED.unit,
            change = EXCLUDED.change, changepercentage = EXCLUDED.changepercentage, createdon = EXCLUDED.createdon,
            createdby = EXCLUDED.createdby, commandid = EXCLUDED.commandid;
        """;

    /// <summary>Native PostgreSQL idempotent observation write.</summary>
    internal const string InsertFuturesAdxSignal =
        """
        INSERT INTO market_data.futures_adx_signal ( contractid, valuedate, timeperiod, periodlength, timestamp,
            futuresprice, plusdi, minusdi, adxvalue, adx, adxstrength, configurationid, observationid, marketdataasof,
            sourcesequence, calculationversion, calculationmethod, schemaversion, isvalid, iswarm )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20 )
        ON CONFLICT (contractid, timeperiod, periodlength, valuedate,
            timestamp) DO UPDATE SET futuresprice = EXCLUDED.futuresprice, plusdi = EXCLUDED.plusdi,
            minusdi = EXCLUDED.minusdi, adxvalue = EXCLUDED.adxvalue, adx = EXCLUDED.adx,
            adxstrength = EXCLUDED.adxstrength, configurationid = EXCLUDED.configurationid,
            observationid = EXCLUDED.observationid, marketdataasof = EXCLUDED.marketdataasof,
            sourcesequence = EXCLUDED.sourcesequence, calculationversion = EXCLUDED.calculationversion,
            calculationmethod = EXCLUDED.calculationmethod, schemaversion = EXCLUDED.schemaversion,
            isvalid = EXCLUDED.isvalid, iswarm = EXCLUDED.iswarm;
        """;

    /// <summary>Native PostgreSQL idempotent observation write.</summary>
    internal const string InsertFuturesAtrSignal =
        """
        INSERT INTO market_data.futures_atr_signal ( contractid, valuedate, timeperiod, periodlength, timestamp,
            futuresprice, atrvalue, truerange, atr, atrstrength, configurationid, observationid, marketdataasof,
            sourcesequence, calculationversion, calculationmethod, schemaversion, isvalid, previousatrvalue, atrbaseline,
            atrratio, iswarm )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22 )
        ON CONFLICT (contractid, timeperiod, periodlength, valuedate,
            timestamp) DO UPDATE SET futuresprice = EXCLUDED.futuresprice, atrvalue = EXCLUDED.atrvalue,
            truerange = EXCLUDED.truerange, atr = EXCLUDED.atr, atrstrength = EXCLUDED.atrstrength,
            configurationid = EXCLUDED.configurationid, observationid = EXCLUDED.observationid,
            marketdataasof = EXCLUDED.marketdataasof, sourcesequence = EXCLUDED.sourcesequence,
            calculationversion = EXCLUDED.calculationversion, calculationmethod = EXCLUDED.calculationmethod,
            schemaversion = EXCLUDED.schemaversion, isvalid = EXCLUDED.isvalid,
            previousatrvalue = EXCLUDED.previousatrvalue, atrbaseline = EXCLUDED.atrbaseline,
            atrratio = EXCLUDED.atrratio, iswarm = EXCLUDED.iswarm;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesBarData =
        """
        INSERT INTO market_data.futures_bar_data ( contractid, symbol, valuedate, bardate, barratetype, barvalue,
            uptrendtrigger, downtrendtrigger )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8 )
        ON CONFLICT (contractid, symbol, valuedate, bardate) DO UPDATE SET barratetype = EXCLUDED.barratetype,
            barvalue = EXCLUDED.barvalue, uptrendtrigger = EXCLUDED.uptrendtrigger, downtrendtrigger = EXCLUDED.downtrendtrigger;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    internal const string InsertFuturesBollingerBandSignal =
        """
        INSERT INTO market_data.futures_bollinger_band_signal ( serieskey, timeperiod, configurationid, yearmonth,
            marketdataasof, observationid, contractid, valuedate, price, ema10center, standarddeviation10, upper10,
            lower10, width10, position10, ema20center, standarddeviation20, upper20, lower20, width20, position20,
            width20baseline, width20ratio, iswarm, sourcesequence, calculatedat, schemaversion, calculationversion,
            calculationmethod, isvalid )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25, $26, $27, $28, $29, $30 )
        ON CONFLICT (serieskey, timeperiod, configurationid, yearmonth, marketdataasof,
            observationid) DO UPDATE SET contractid = EXCLUDED.contractid, valuedate = EXCLUDED.valuedate,
            price = EXCLUDED.price, ema10center = EXCLUDED.ema10center,
            standarddeviation10 = EXCLUDED.standarddeviation10, upper10 = EXCLUDED.upper10, lower10 = EXCLUDED.lower10,
            width10 = EXCLUDED.width10, position10 = EXCLUDED.position10, ema20center = EXCLUDED.ema20center,
            standarddeviation20 = EXCLUDED.standarddeviation20, upper20 = EXCLUDED.upper20, lower20 = EXCLUDED.lower20,
            width20 = EXCLUDED.width20, position20 = EXCLUDED.position20, width20baseline = EXCLUDED.width20baseline,
            width20ratio = EXCLUDED.width20ratio, iswarm = EXCLUDED.iswarm, sourcesequence = EXCLUDED.sourcesequence,
            calculatedat = EXCLUDED.calculatedat, schemaversion = EXCLUDED.schemaversion,
            calculationversion = EXCLUDED.calculationversion, calculationmethod = EXCLUDED.calculationmethod,
            isvalid = EXCLUDED.isvalid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesClosingPrice =
        """
        INSERT INTO market_data.futures_closing_price ( contractid, valuedate, closingprice, createdon, createdby )
        VALUES ( $1, $2, $3, $4, $5 )
        ON CONFLICT (contractid, valuedate) DO UPDATE SET closingprice = EXCLUDED.closingprice,
            createdon = EXCLUDED.createdon, createdby = EXCLUDED.createdby;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    internal const string InsertFuturesEmaSignal =
        """
        INSERT INTO market_data.futures_ema_signal ( serieskey, timeperiod, configurationid, yearmonth, marketdataasof,
            observationid, contractid, valuedate, price, ema10, previousema10, ema10slope, ema20, previousema20,
            ema20slope, ema50, previousema50, ema50slope, ema200, previousema200, ema200slope, iswarm, sourcesequence,
            calculatedat, schemaversion, calculationversion, calculationmethod, isvalid )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25, $26, $27, $28 )
        ON CONFLICT (serieskey, timeperiod, configurationid, yearmonth, marketdataasof,
            observationid) DO UPDATE SET contractid = EXCLUDED.contractid, valuedate = EXCLUDED.valuedate,
            price = EXCLUDED.price, ema10 = EXCLUDED.ema10, previousema10 = EXCLUDED.previousema10,
            ema10slope = EXCLUDED.ema10slope, ema20 = EXCLUDED.ema20, previousema20 = EXCLUDED.previousema20,
            ema20slope = EXCLUDED.ema20slope, ema50 = EXCLUDED.ema50, previousema50 = EXCLUDED.previousema50,
            ema50slope = EXCLUDED.ema50slope, ema200 = EXCLUDED.ema200, previousema200 = EXCLUDED.previousema200,
            ema200slope = EXCLUDED.ema200slope, iswarm = EXCLUDED.iswarm, sourcesequence = EXCLUDED.sourcesequence,
            calculatedat = EXCLUDED.calculatedat, schemaversion = EXCLUDED.schemaversion,
            calculationversion = EXCLUDED.calculationversion, calculationmethod = EXCLUDED.calculationmethod,
            isvalid = EXCLUDED.isvalid;
        """;

    /// <summary>SQL for insert futures eod data.</summary>
    internal const string InsertFuturesEodData =
        """
        INSERT INTO market_data.futures_eod_data ( contractid, valuedate, symbol, openprice, highprice, lowprice,
            closeprice, volume, dailypercentchange, dailystddev, dailystddevamount, upperband, mean, lowerband,
            marketdirection, marketvolatility, pricedirection, pricevolatility, marketdirectionindicator, windowsize,
            fiftydma, twohundreddma )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22 )
        ON CONFLICT (contractid,valuedate,symbol) DO UPDATE SET openprice=EXCLUDED.openprice,highprice=EXCLUDED.highprice,
            lowprice=EXCLUDED.lowprice,closeprice=EXCLUDED.closeprice,volume=EXCLUDED.volume,
            dailypercentchange=EXCLUDED.dailypercentchange,dailystddev=EXCLUDED.dailystddev,
            dailystddevamount=EXCLUDED.dailystddevamount,upperband=EXCLUDED.upperband,mean=EXCLUDED.mean,
            lowerband=EXCLUDED.lowerband,marketdirection=EXCLUDED.marketdirection,
            marketvolatility=EXCLUDED.marketvolatility,pricedirection=EXCLUDED.pricedirection,
            pricevolatility=EXCLUDED.pricevolatility,marketdirectionindicator=EXCLUDED.marketdirectionindicator,
            windowsize=EXCLUDED.windowsize,fiftydma=EXCLUDED.fiftydma,twohundreddma=EXCLUDED.twohundreddma;
        """;

    /// <summary>SQL for insert futures eod data by month.</summary>
    internal const string InsertFuturesEodDataByMonth =
        """
        INSERT INTO market_data.futures_eod_data_by_month ( yearmonth, contractid, valuedate, symbol, openprice,
            highprice, lowprice, closeprice, volume, dailypercentchange, dailystddev, dailystddevamount, upperband, mean,
            lowerband, marketdirection, marketvolatility, pricedirection, pricevolatility, marketdirectionindicator,
            windowsize, fiftydma, twohundreddma )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23 )
        ON CONFLICT (yearmonth,valuedate,contractid,symbol) DO UPDATE SET openprice=EXCLUDED.openprice,
            highprice=EXCLUDED.highprice,lowprice=EXCLUDED.lowprice,closeprice=EXCLUDED.closeprice,volume=EXCLUDED.volume,
            dailypercentchange=EXCLUDED.dailypercentchange,dailystddev=EXCLUDED.dailystddev,
            dailystddevamount=EXCLUDED.dailystddevamount,upperband=EXCLUDED.upperband,mean=EXCLUDED.mean,
            lowerband=EXCLUDED.lowerband,marketdirection=EXCLUDED.marketdirection,
            marketvolatility=EXCLUDED.marketvolatility,pricedirection=EXCLUDED.pricedirection,
            pricevolatility=EXCLUDED.pricevolatility,marketdirectionindicator=EXCLUDED.marketdirectionindicator,
            windowsize=EXCLUDED.windowsize,fiftydma=EXCLUDED.fiftydma,twohundreddma=EXCLUDED.twohundreddma;
        """;

    /// <summary>SQL for insert futures eod data index.</summary>
    internal const string InsertFuturesEodDataIndex =
        """
        INSERT INTO market_data.futures_eod_data_index (valuedate, contractid)
        VALUES ($1, $2) ON CONFLICT (valuedate,contractid) DO NOTHING;
        """;

    /// <summary>SQL for insert futures intra day data.</summary>
    internal const string InsertFuturesIntraDayData =
        """
        INSERT INTO market_data.futures_intra_day_data ( contractid, valuedate, sequenceid, symbol, openprice, highprice,
            lowprice, closeprice, volume, dailypercentchange, dailystddev, dailystddevamount, upperband, mean, lowerband,
            marketdirection, marketvolatility, pricedirection, pricevolatility, marketdirectionindicator, windowsize )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21 )
        ON CONFLICT (contractid,valuedate,sequenceid) DO UPDATE SET symbol=EXCLUDED.symbol,openprice=EXCLUDED.openprice,
            highprice=EXCLUDED.highprice,lowprice=EXCLUDED.lowprice,closeprice=EXCLUDED.closeprice,volume=EXCLUDED.volume,
            dailypercentchange=EXCLUDED.dailypercentchange,dailystddev=EXCLUDED.dailystddev,
            dailystddevamount=EXCLUDED.dailystddevamount,upperband=EXCLUDED.upperband,mean=EXCLUDED.mean,
            lowerband=EXCLUDED.lowerband,marketdirection=EXCLUDED.marketdirection,
            marketvolatility=EXCLUDED.marketvolatility,pricedirection=EXCLUDED.pricedirection,
            pricevolatility=EXCLUDED.pricevolatility,marketdirectionindicator=EXCLUDED.marketdirectionindicator,
            windowsize=EXCLUDED.windowsize;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesItiSignal =
        """
        INSERT INTO market_data.futures_iti_signal ( contractid, valuedate, timeperiod, sequenceid, intrinsictime,
            intrinsictimegroupid, intrinsictimelength, intrinsicprice, intrinsictimetrend, intrinsictimemode, trendprice,
            trendextreme, trendreversal, trenddelta, targetdelta, lambda, tradingdays, threshold, uptrendtrigger,
            downtrendtrigger, tradestate, bandlevel, reversallevel )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23 )
        ON CONFLICT (contractid, valuedate, timeperiod, intrinsictimemode, intrinsictimetrend, intrinsictimegroupid,
            sequenceid) DO UPDATE SET intrinsictime = EXCLUDED.intrinsictime,
            intrinsictimelength = EXCLUDED.intrinsictimelength, intrinsicprice = EXCLUDED.intrinsicprice,
            trendprice = EXCLUDED.trendprice, trendextreme = EXCLUDED.trendextreme,
            trendreversal = EXCLUDED.trendreversal, trenddelta = EXCLUDED.trenddelta, targetdelta = EXCLUDED.targetdelta,
            lambda = EXCLUDED.lambda, tradingdays = EXCLUDED.tradingdays, threshold = EXCLUDED.threshold,
            uptrendtrigger = EXCLUDED.uptrendtrigger, downtrendtrigger = EXCLUDED.downtrendtrigger,
            tradestate = EXCLUDED.tradestate, bandlevel = EXCLUDED.bandlevel, reversallevel = EXCLUDED.reversallevel;
        """;

    /// <summary>Native PostgreSQL idempotent observation write.</summary>
    internal const string InsertFuturesMacdSignal =
        """
        INSERT INTO market_data.futures_macd_signal ( contractid, valuedate, timeperiod, signalemaperiod, fastemaperiod,
            slowemaperiod, timestamp, futuresprice, fastema, slowema, macdline, signalline, histogram, macd,
            macdstrength, configurationid, observationid, marketdataasof, sourcesequence, calculationversion,
            calculationmethod, schemaversion, isvalid, iswarm, observationcount )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25 )
        ON CONFLICT (contractid, timeperiod, signalemaperiod, fastemaperiod, slowemaperiod, valuedate,
            timestamp) DO UPDATE SET futuresprice = EXCLUDED.futuresprice, fastema = EXCLUDED.fastema,
            slowema = EXCLUDED.slowema, macdline = EXCLUDED.macdline, signalline = EXCLUDED.signalline,
            histogram = EXCLUDED.histogram, macd = EXCLUDED.macd, macdstrength = EXCLUDED.macdstrength,
            configurationid = EXCLUDED.configurationid, observationid = EXCLUDED.observationid,
            marketdataasof = EXCLUDED.marketdataasof, sourcesequence = EXCLUDED.sourcesequence,
            calculationversion = EXCLUDED.calculationversion, calculationmethod = EXCLUDED.calculationmethod,
            schemaversion = EXCLUDED.schemaversion, isvalid = EXCLUDED.isvalid, iswarm = EXCLUDED.iswarm,
            observationcount = EXCLUDED.observationcount;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesOptionTickPriceData =
        """
        INSERT INTO market_data.futures_option_tick_price_data ( contractid, valuedate, tickid, ticktime, optionprice,
            bidprice, askprice, bidsize, asksize, impliedvolatility, underlyingprice, delta, gamma, vega, theta, rho )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16 )
        ON CONFLICT (contractid, valuedate, tickid) DO UPDATE SET ticktime = EXCLUDED.ticktime,
            optionprice = EXCLUDED.optionprice, bidprice = EXCLUDED.bidprice, askprice = EXCLUDED.askprice,
            bidsize = EXCLUDED.bidsize, asksize = EXCLUDED.asksize, impliedvolatility = EXCLUDED.impliedvolatility,
            underlyingprice = EXCLUDED.underlyingprice, delta = EXCLUDED.delta, gamma = EXCLUDED.gamma,
            vega = EXCLUDED.vega, theta = EXCLUDED.theta, rho = EXCLUDED.rho;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesRsiSignal =
        """
        INSERT INTO market_data.futures_rsi_signal ( contractid, valuedate, timeperiod, periodlength, timestamp, price,
            pricechange, pricegain, priceloss, averagepricegain, averagepriceloss, rs, rsi, rsiaverage, rsislope,
            sourcesequence, sourceeventtimestamp, configurationid, observationid, marketdataasof, calculationversion,
            calculationmethod, schemaversion, isvalid, previousrsi, regimeslope, iswarm )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25, $26, $27 )
        ON CONFLICT (contractid, timeperiod, periodlength, valuedate, timestamp) DO UPDATE SET price = EXCLUDED.price,
            pricechange = EXCLUDED.pricechange, pricegain = EXCLUDED.pricegain, priceloss = EXCLUDED.priceloss,
            averagepricegain = EXCLUDED.averagepricegain, averagepriceloss = EXCLUDED.averagepriceloss, rs = EXCLUDED.rs,
            rsi = EXCLUDED.rsi, rsiaverage = EXCLUDED.rsiaverage, rsislope = EXCLUDED.rsislope,
            sourcesequence = EXCLUDED.sourcesequence, sourceeventtimestamp = EXCLUDED.sourceeventtimestamp,
            configurationid = EXCLUDED.configurationid, observationid = EXCLUDED.observationid,
            marketdataasof = EXCLUDED.marketdataasof, calculationversion = EXCLUDED.calculationversion,
            calculationmethod = EXCLUDED.calculationmethod, schemaversion = EXCLUDED.schemaversion,
            isvalid = EXCLUDED.isvalid, previousrsi = EXCLUDED.previousrsi, regimeslope = EXCLUDED.regimeslope,
            iswarm = EXCLUDED.iswarm;
        """;

    /// <summary>SQL for insert futures tdi signal.</summary>
    internal const string InsertFuturesTdiSignal =
        """
        INSERT INTO market_data.futures_traders_dynamic_index_signal ( contractid, timeperiod, configurationid,
            valuedate, timestamp, schemaversion, rsiperiod, pricelineperiod, signallineperiod, marketbaseperiod,
            volatilitybandperiod, volatilitybanddeviation, price, rsi, priceline, signalline, marketbaseline,
            uppervolatilityband, lowervolatilityband, bandwidth, pricesignaldivergence, crosstype, marketstate,
            trenddirection, trendstrength, sourcesequence, sourceeventtimestamp )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25, $26, $27 )
        ON CONFLICT (contractid,timeperiod,configurationid,valuedate,
            timestamp) DO UPDATE SET schemaversion=EXCLUDED.schemaversion,rsiperiod=EXCLUDED.rsiperiod,
            pricelineperiod=EXCLUDED.pricelineperiod,signallineperiod=EXCLUDED.signallineperiod,
            marketbaseperiod=EXCLUDED.marketbaseperiod,volatilitybandperiod=EXCLUDED.volatilitybandperiod,
            volatilitybanddeviation=EXCLUDED.volatilitybanddeviation,price=EXCLUDED.price,rsi=EXCLUDED.rsi,
            priceline=EXCLUDED.priceline,signalline=EXCLUDED.signalline,marketbaseline=EXCLUDED.marketbaseline,
            uppervolatilityband=EXCLUDED.uppervolatilityband,lowervolatilityband=EXCLUDED.lowervolatilityband,
            bandwidth=EXCLUDED.bandwidth,pricesignaldivergence=EXCLUDED.pricesignaldivergence,
            crosstype=EXCLUDED.crosstype,marketstate=EXCLUDED.marketstate,trenddirection=EXCLUDED.trenddirection,
            trendstrength=EXCLUDED.trendstrength,sourcesequence=EXCLUDED.sourcesequence,
            sourceeventtimestamp=EXCLUDED.sourceeventtimestamp;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesTradeSignal =
        """
        INSERT INTO market_data.futures_trade_signal ( contractid, valuedate, timeperiod, sequenceid, timestamp, mean,
            stddev, futuresprice, pricechangepercent, fundriskpercent, rsi, rsislope, trendtype, trendstrength,
            tradesignal, tdi, tdistrength, mdi, mditrend, mdiuptrendlimit, mdidowntrendlimit, uptrendingtrigger,
            downtrendingtrigger, entrytrigger, exittrigger, trenddelta, trendextreme, trendreversal, fiftydma,
            twohundreddma, tradeexecutestate )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25, $26, $27, $28, $29, $30, $31 )
        ON CONFLICT (contractid, valuedate, timeperiod, timestamp, sequenceid) DO UPDATE SET mean = EXCLUDED.mean,
            stddev = EXCLUDED.stddev, futuresprice = EXCLUDED.futuresprice,
            pricechangepercent = EXCLUDED.pricechangepercent, fundriskpercent = EXCLUDED.fundriskpercent,
            rsi = EXCLUDED.rsi, rsislope = EXCLUDED.rsislope, trendtype = EXCLUDED.trendtype,
            trendstrength = EXCLUDED.trendstrength, tradesignal = EXCLUDED.tradesignal, tdi = EXCLUDED.tdi,
            tdistrength = EXCLUDED.tdistrength, mdi = EXCLUDED.mdi, mditrend = EXCLUDED.mditrend,
            mdiuptrendlimit = EXCLUDED.mdiuptrendlimit, mdidowntrendlimit = EXCLUDED.mdidowntrendlimit,
            uptrendingtrigger = EXCLUDED.uptrendingtrigger, downtrendingtrigger = EXCLUDED.downtrendingtrigger,
            entrytrigger = EXCLUDED.entrytrigger, exittrigger = EXCLUDED.exittrigger, trenddelta = EXCLUDED.trenddelta,
            trendextreme = EXCLUDED.trendextreme, trendreversal = EXCLUDED.trendreversal, fiftydma = EXCLUDED.fiftydma,
            twohundreddma = EXCLUDED.twohundreddma, tradeexecutestate = EXCLUDED.tradeexecutestate;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesTradeSignalIndex =
        """
        INSERT INTO market_data.futures_trade_signal_lookup_by_scope ( scope, entryid, sequenceid, contractid, valuedate,
            timeperiod )
        VALUES ( $1, $2, $3, $4, $5, $6 )
        ON CONFLICT (scope, entryid) DO UPDATE SET sequenceid = EXCLUDED.sequenceid, contractid = EXCLUDED.contractid,
            valuedate = EXCLUDED.valuedate, timeperiod = EXCLUDED.timeperiod
        WHERE market_data.futures_trade_signal_lookup_by_scope.sequenceid <= EXCLUDED.sequenceid;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesTradeSignalQuarantine =
        """
        INSERT INTO market_data.futures_trade_signal_quarantine ( fingerprint, sourcepayload, reason, quarantinedon )
        VALUES ( $1, $2, $3, $4 )
        ON CONFLICT (fingerprint) DO UPDATE SET sourcepayload = EXCLUDED.sourcepayload, reason = EXCLUDED.reason,
            quarantinedon = EXCLUDED.quarantinedon;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    internal const string InsertFuturesVwapSignal =
        """
        INSERT INTO market_data.futures_vwap_signal ( contractid, valuedate, configurationid, asofutc, lasttradeordinal,
            sessionstartutc, sessionendutc, cumulativepricevolume, cumulativevolume, eligibletradecount,
            rejectedtradecount, lastprice, vwap, priceminusvwap, pricetovwappercent, lasttradesourcesequence,
            streamepochid, iswarm, isvalid, invalidreason, istickexact, calculationmethod, schemaversion, calculationversion )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24 )
        ON CONFLICT (contractid, valuedate, configurationid, asofutc,
            lasttradeordinal) DO UPDATE SET sessionstartutc = EXCLUDED.sessionstartutc,
            sessionendutc = EXCLUDED.sessionendutc, cumulativepricevolume = EXCLUDED.cumulativepricevolume,
            cumulativevolume = EXCLUDED.cumulativevolume, eligibletradecount = EXCLUDED.eligibletradecount,
            rejectedtradecount = EXCLUDED.rejectedtradecount, lastprice = EXCLUDED.lastprice, vwap = EXCLUDED.vwap,
            priceminusvwap = EXCLUDED.priceminusvwap, pricetovwappercent = EXCLUDED.pricetovwappercent,
            lasttradesourcesequence = EXCLUDED.lasttradesourcesequence, streamepochid = EXCLUDED.streamepochid,
            iswarm = EXCLUDED.iswarm, isvalid = EXCLUDED.isvalid, invalidreason = EXCLUDED.invalidreason,
            istickexact = EXCLUDED.istickexact, calculationmethod = EXCLUDED.calculationmethod,
            schemaversion = EXCLUDED.schemaversion, calculationversion = EXCLUDED.calculationversion;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertFuturesVxTermStructureSignal =
        """
        INSERT INTO market_data.futures_vx_term_structure_signal ( valuedate, configurationid, calculatedat,
            frontsourcesequence, backsourcesequence, frontcontractid, frontexpiry, frontprice, backcontractid,
            backexpiry, backprice, frontbackspread, frontbackratio, termstructurepercent, termstructurestate,
            priorfrontbackratio, priortermstructurepercent, frontsourcetimestamp, backsourcetimestamp, iswarm, isvalid,
            schemaversion, calculationversion )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23 )
        ON CONFLICT (valuedate, configurationid, calculatedat, frontsourcesequence,
            backsourcesequence) DO UPDATE SET frontcontractid = EXCLUDED.frontcontractid,
            frontexpiry = EXCLUDED.frontexpiry, frontprice = EXCLUDED.frontprice,
            backcontractid = EXCLUDED.backcontractid, backexpiry = EXCLUDED.backexpiry, backprice = EXCLUDED.backprice,
            frontbackspread = EXCLUDED.frontbackspread, frontbackratio = EXCLUDED.frontbackratio,
            termstructurepercent = EXCLUDED.termstructurepercent, termstructurestate = EXCLUDED.termstructurestate,
            priorfrontbackratio = EXCLUDED.priorfrontbackratio,
            priortermstructurepercent = EXCLUDED.priortermstructurepercent,
            frontsourcetimestamp = EXCLUDED.frontsourcetimestamp, backsourcetimestamp = EXCLUDED.backsourcetimestamp,
            iswarm = EXCLUDED.iswarm, isvalid = EXCLUDED.isvalid, schemaversion = EXCLUDED.schemaversion,
            calculationversion = EXCLUDED.calculationversion;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string InsertLatest =
        """
        INSERT INTO market_data.option_iv_latest(environment,series_id,methodology_version,metric_policy_version,
            snapshot_id,snapshot_digest,publication_sequence,available_at_utc)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8)
        ON CONFLICT (environment,series_id,methodology_version,metric_policy_version) DO UPDATE SET snapshot_id=EXCLUDED.snapshot_id,snapshot_digest=EXCLUDED.snapshot_digest,publication_sequence=EXCLUDED.publication_sequence,available_at_utc=EXCLUDED.available_at_utc
        WHERE market_data.option_iv_latest.publication_sequence < EXCLUDED.publication_sequence;
        """;

    /// <summary>InsertMarketDataImportOwnership database statement.</summary>
    internal const string InsertMarketDataImportOwnership =
        """
        INSERT INTO market_data.market_data_import_ownership(dataset,logicalkey,commandid,maywrite,createdon)
        VALUES($1,$2,$3,$4,$5);
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertMarketHoliday =
        """
        INSERT INTO market_data.market_holiday (currencytype, holidaydate, description)
        VALUES ($1, $2, $3)
        ON CONFLICT (currencytype, holidaydate) DO UPDATE SET description = EXCLUDED.description;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string InsertMetricHistory =
        """
        INSERT INTO market_data.option_iv_metric_history(environment,series_id,methodology_version,metric_policy_version,
            calendar_bucket,value_date,sampling_slot,revision,snapshot_id,available_at_utc,publication_sequence,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
        ON CONFLICT (environment,series_id,methodology_version,metric_policy_version,calendar_bucket,value_date,
            sampling_slot,revision,snapshot_id) DO UPDATE SET payload=EXCLUDED.payload
        WHERE market_data.option_iv_metric_history.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string InsertObservationById =
        """
        INSERT INTO market_data.option_iv_observation_by_id(environment,observation_id,payload)
        VALUES($1,$2,$3)
        ON CONFLICT (environment,observation_id) DO UPDATE SET payload=EXCLUDED.payload
        WHERE market_data.option_iv_observation_by_id.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string InsertObservationHistory =
        """
        INSERT INTO market_data.option_iv_observation_history(environment,series_id,methodology_version,calendar_bucket,
            value_date,sampling_slot,revision,observation_id,available_at_utc,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
        ON CONFLICT (environment,series_id,methodology_version,calendar_bucket,value_date,sampling_slot,revision,
            observation_id) DO UPDATE SET payload=EXCLUDED.payload
        WHERE market_data.option_iv_observation_history.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>InsertOptionChainParameterVersion database statement.</summary>
    internal const string InsertOptionChainParameterVersion =
        """
        INSERT INTO market_data.strategy_option_chain_policy_version(set_id,version,parameter_hash)
        VALUES($1,$2,$3) ON CONFLICT(set_id,version) DO NOTHING;
        """;

    /// <summary>SQL for insert option trade evidence.</summary>
    internal const string InsertOptionTradeEvidence =
        """
        WITH accepted AS (
            INSERT INTO market_data.option_trade_evidence(contract_id,value_date,source_id,source_digest,payload)
            VALUES($1,$2,$3,$4,$5) ON CONFLICT (contract_id,value_date,source_id) DO NOTHING RETURNING 1
        ) SELECT EXISTS(SELECT 1 FROM accepted);
        """;

    /// <summary>SQL for insert option trade evidence batch.</summary>
    internal const string InsertOptionTradeEvidenceBatch =
        """
        INSERT INTO market_data.option_trade_evidence(contract_id,value_date,source_id,source_digest,payload)
        SELECT contract_id,value_date,source_id,source_digest,payload
        FROM unnest($1::text[],$2::date[],$3::text[],$4::text[],$5::bytea[])
            AS requested(contract_id,value_date,source_id,source_digest,payload)
        ORDER BY contract_id,value_date,source_id
        ON CONFLICT (contract_id,value_date,source_id) DO NOTHING
        RETURNING contract_id,value_date,source_id;
        """;

    /// <summary>SQL for insert rate of return.</summary>
    internal const string InsertRateOfReturn =
        """
        INSERT INTO market_data.rate_of_return (symbol, valuedate, rateofreturn)
        VALUES ($1, $2, $3) ON CONFLICT (symbol,valuedate) DO UPDATE SET rateofreturn=EXCLUDED.rateofreturn;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string InsertSnapshot =
        """
        INSERT INTO market_data.option_iv_snapshot_by_id(environment,snapshot_id,snapshot_digest,publication_sequence,payload)
        VALUES($1,$2,$3,$4,$5)
        ON CONFLICT (environment,snapshot_id) DO UPDATE SET payload=EXCLUDED.payload
        WHERE market_data.option_iv_snapshot_by_id.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string InsertTradeLiveFeed =
        """
        INSERT INTO market_data.trade_live_feed ( orderid, tradeid, tradelivefeedstate )
        VALUES ( $1, $2, $3 )
        ON CONFLICT (orderid, tradeid) DO UPDATE SET orderid = EXCLUDED.orderid, tradeid = EXCLUDED.tradeid,
            tradelivefeedstate = EXCLUDED.tradelivefeedstate;
        """;

    /// <summary>SQL for insert vix futures eod data.</summary>
    internal const string InsertVixFuturesEodData =
        """
        INSERT INTO market_data.vix_futures_eod_data (contractid, valuedate, openprice, highprice, lowprice, closeprice, volume)
        VALUES ($1, $2, $3, $4, $5, $6, $7)
        ON CONFLICT(contractid,valuedate) DO UPDATE SET openprice=EXCLUDED.openprice,highprice=EXCLUDED.highprice,
            lowprice=EXCLUDED.lowprice,closeprice=EXCLUDED.closeprice,volume=EXCLUDED.volume;
        """;

    /// <summary>SQL for insert yield curve rate.</summary>
    internal const string InsertYieldCurveRate =
        """
        INSERT INTO market_data.yield_curve_rates ( id, valuedate, onemonth, twomonth, threemonth, sixmonth, oneyear,
            twoyear, threeyear, fiveyear, sevenyear, tenyear, twentyyear, thirtyyear )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14 )
        ON CONFLICT(id,valuedate) DO UPDATE SET onemonth=EXCLUDED.onemonth,twomonth=EXCLUDED.twomonth,
            threemonth=EXCLUDED.threemonth,sixmonth=EXCLUDED.sixmonth,oneyear=EXCLUDED.oneyear,twoyear=EXCLUDED.twoyear,
            threeyear=EXCLUDED.threeyear,fiveyear=EXCLUDED.fiveyear,sevenyear=EXCLUDED.sevenyear,tenyear=EXCLUDED.tenyear,
            twentyyear=EXCLUDED.twentyyear,thirtyyear=EXCLUDED.thirtyyear;
        """;

    /// <summary>SQL for insert yield curve rate by date.</summary>
    internal const string InsertYieldCurveRateByDate =
        """
        INSERT INTO market_data.yield_curve_rate_by_date ( lookupid, valuedate, onemonth, twomonth, threemonth, sixmonth,
            oneyear, twoyear, threeyear, fiveyear, sevenyear, tenyear, twentyyear, thirtyyear )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14 )
        ON CONFLICT(lookupid,valuedate) DO UPDATE SET onemonth=EXCLUDED.onemonth,twomonth=EXCLUDED.twomonth,
            threemonth=EXCLUDED.threemonth,sixmonth=EXCLUDED.sixmonth,oneyear=EXCLUDED.oneyear,twoyear=EXCLUDED.twoyear,
            threeyear=EXCLUDED.threeyear,fiveyear=EXCLUDED.fiveyear,sevenyear=EXCLUDED.sevenyear,tenyear=EXCLUDED.tenyear,
            twentyyear=EXCLUDED.twentyyear,thirtyyear=EXCLUDED.thirtyyear;
        """;

    /// <summary>SQL for insert yield curve rate year.</summary>
    internal const string InsertYieldCurveRateYear =
        """
        INSERT INTO market_data.yield_curve_rate_year (lookupid, rateyear)
        VALUES ($1, $2) ON CONFLICT(lookupid,rateyear) DO NOTHING;
        """;

    /// <summary>Locks event stream id rows.</summary>
    internal const string LockMarketDataBaseline =
        """
        LOCK TABLE event_stream_id, event_log, market_data.economic_calendar,
            market_data.economic_calendar_country_code, market_data.yield_curve_rates,
            market_data.yield_curve_rate_by_date, market_data.yield_curve_rate_year
            IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>LockTradeSignalIndexes database statement.</summary>
    internal const string LockTradeSignalIndexes =
        """
        LOCK TABLE market_data.futures_trade_signal,market_data.futures_trade_signal_lookup_by_scope,
            market_data.futures_trade_signal_quarantine IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>LockYieldCurveIndexes database statement.</summary>
    internal const string LockYieldCurveIndexes =
        """
        LOCK TABLE market_data.yield_curve_rates,market_data.yield_curve_rate_by_date,
            market_data.yield_curve_rate_year IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>Reads strategy option chain policy current rows.</summary>
    internal const string MarketDataDbContext_ReadPublishedAsync_1 =
        """
        SELECT DISTINCT ON(set_id) set_id,version,published,parameter_hash
        FROM market_data.strategy_option_chain_policy_current
        WHERE environment=$1 ORDER BY set_id,version DESC LIMIT 129;
        """;

    /// <summary>Reads composition preparation rows.</summary>
    internal const string MarketDataDbContext_Statement_1 =
        """
        SELECT payload FROM composition_preparation WHERE workflow_id=:workflow AND input_revision=:revision;
        """;

    /// <summary>Inserts composition preparation rows.</summary>
    internal const string MarketDataDbContext_Statement_2 =
        """
        INSERT INTO composition_preparation(workflow_id,input_revision,payload)
        VALUES(:workflow,:revision,:payload) if NOT EXISTS;
        """;

    /// <summary>Inserts event stream id rows.</summary>
    internal const string MarketDataImportStreamFence_LockAsync_1 =
        """
        INSERT INTO event_stream_id (eventstream) VALUES ($1)
        ON CONFLICT (eventstream) DO NOTHING;
        """;

    /// <summary>Reads and locks event stream id rows.</summary>
    internal const string MarketDataImportStreamFence_LockAsync_2 =
        """
        SELECT eventstreamid, currentversion FROM event_stream_id
        WHERE eventstream = $1 FOR UPDATE;
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string MarketDataImportStreamFence_LockAsync_3 =
        """
        SELECT EXISTS (
            SELECT 1 FROM event_log source
            JOIN event_projector_state projection ON projection.eventid = source.eventversion
            WHERE source.eventstreamid = $1 AND projection.projectorname = $2
              AND (projection.stage IS DISTINCT FROM 'Completed'
                OR projection.outcome IS NULL
                OR projection.outcome NOT IN ('Completed', 'AlreadyCompleted', 'Superseded'))
        );
        """;

    /// <summary>Reads economic calendar rows.</summary>
    internal const string ReadBaselineEconomicCalendar =
        """
        SELECT eventdate, countrycode, eventname, actual, forecast, prior, impact, unit,
               change, changepercentage, createdon, createdby
        FROM market_data.economic_calendar
        ORDER BY countrycode, eventdate, eventname;
        """;

    /// <summary>Reads event stream id rows.</summary>
    internal const string ReadMarketDataBaselineHeads =
        """
        SELECT eventstream, currentversion FROM event_stream_id
        WHERE starts_with(eventstream, $1) AND currentversion > 0
        ORDER BY eventstream;
        """;

    /// <summary>ReadMarketDataImportOwnership database statement.</summary>
    internal const string ReadMarketDataImportOwnership =
        """
        SELECT commandid,maywrite
        FROM market_data.market_data_import_ownership WHERE dataset=$1 AND logicalkey=$2;
        """;

    /// <summary>ReadTradeSignalRepairRows database statement.</summary>
    internal const string ReadTradeSignalRepairRows =
        """
        SELECT row_to_json(t)::text
        FROM market_data.futures_trade_signal t ORDER BY sequenceid,contractid,valuedate,timeperiod;
        """;

    /// <summary>Reads a bounded page for backfilling the historical-load progress projection.</summary>
    internal const string ReadUnprojectedHistoricalDataLoads =
        """
        SELECT c.data_load_attempt_id, c.request_sha256, c.status, c.stage,
               c.provider_job_id, c.provider_file_id, c.batch_ordinal,
               c.source_position, c.error_message, c.updated_at_utc,
               NULLIF(m.manifest_json, ''), NULLIF(m.audit_json, '')
        FROM historical_data_load_checkpoint c
        LEFT JOIN historical_data_load_manifest m ON m.data_load_attempt_id = c.data_load_attempt_id
        WHERE NOT EXISTS (
            SELECT 1 FROM historical_analytics_attempt_progress p WHERE p.attempt_id = c.data_load_attempt_id)
        ORDER BY c.data_load_attempt_id
        LIMIT 128;
        """;

    /// <summary>RebuildYieldCurveDateIndex database statement.</summary>
    internal const string RebuildYieldCurveDateIndex =
        """
        INSERT INTO market_data.yield_curve_rate_by_date(lookupid,valuedate,onemonth,twomonth,threemonth,sixmonth,oneyear,
            twoyear,threeyear,fiveyear,sevenyear,tenyear,twentyyear,thirtyyear) SELECT 1,valuedate,onemonth,twomonth,
            threemonth,sixmonth,oneyear,twoyear,threeyear,fiveyear,sevenyear,tenyear,twentyyear,thirtyyear
        FROM market_data.yield_curve_rates WHERE id=1;
        """;

    /// <summary>RebuildYieldCurveYearIndex database statement.</summary>
    internal const string RebuildYieldCurveYearIndex =
        """
        INSERT INTO market_data.yield_curve_rate_year(lookupid,rateyear) SELECT DISTINCT 1,extract(year FROM valuedate)::integer
        FROM market_data.yield_curve_rates WHERE id=1;
        """;

    /// <summary>SQL for save historical data load.</summary>
    internal const string SaveHistoricalDataLoad =
        """
        WITH checkpoint AS (
            INSERT INTO historical_data_load_checkpoint (
                data_load_attempt_id, request_sha256, status, stage, provider_job_id,
                provider_file_id, batch_ordinal, source_position, error_message, updated_at_utc)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            ON CONFLICT (data_load_attempt_id) DO UPDATE SET
                request_sha256 = EXCLUDED.request_sha256,
                status = EXCLUDED.status,
                stage = EXCLUDED.stage,
                provider_job_id = EXCLUDED.provider_job_id,
                provider_file_id = EXCLUDED.provider_file_id,
                batch_ordinal = EXCLUDED.batch_ordinal,
                source_position = EXCLUDED.source_position,
                error_message = EXCLUDED.error_message,
                updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING data_load_attempt_id
        ), manifest AS (
            INSERT INTO historical_data_load_manifest (
                data_load_attempt_id, manifest_json, audit_json, created_at_utc)
            SELECT $1, $11, $12, $10
            FROM checkpoint
            WHERE $11 <> '' OR $12 <> ''
            ON CONFLICT (data_load_attempt_id) DO NOTHING
            RETURNING data_load_attempt_id
        )
        INSERT INTO historical_analytics_attempt_progress (attempt_id, payload)
        SELECT data_load_attempt_id, $13 FROM checkpoint
        ON CONFLICT (attempt_id) DO UPDATE SET payload = EXCLUDED.payload
        WHERE ($11 = '' AND $12 = '') OR EXISTS (SELECT 1 FROM manifest);
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string SelectLatest =
        """
        SELECT snapshot_id,snapshot_digest,publication_sequence,available_at_utc
        FROM market_data.option_iv_latest
        WHERE environment=$1 AND series_id=$2 AND methodology_version=$3 AND metric_policy_version=$4;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string SelectObservationById =
        """
        SELECT payload FROM market_data.option_iv_observation_by_id WHERE environment=$1 AND observation_id=$2;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    internal const string SelectSnapshot =
        """
        SELECT snapshot_digest,publication_sequence,payload
        FROM market_data.option_iv_snapshot_by_id WHERE environment=$1 AND snapshot_id=$2;
        """;

    /// <summary>SQL for store qualified indicator seed bars.</summary>
    internal const string StoreQualifiedIndicatorSeedBars =
        """
        INSERT INTO market_data.indicator_seed_coverage(contract_id,time_frame,value_date,source_version,interval_end,payload)
        SELECT $1,$2,$3,$4,interval_end,payload
        FROM unnest($5::timestamptz[],$6::bytea[]) AS qualified(interval_end,payload)
        ORDER BY interval_end
        ON CONFLICT (contract_id,time_frame,value_date,source_version,interval_end) DO NOTHING;
        """;

    /// <summary>SQL for try insert observation.</summary>
    internal const string TryInsertObservation =
        """
        WITH accepted AS (INSERT INTO market_data.futures_trade_session_bar ( serieskey, timeperiod, yearmonth,
            marketdataasof, observationid, contractid, valuedate, intervalstart, intervalend, openprice, highprice,
            lowprice, closeprice, volume, tradecount, pricevolumesum, firstsourcesequence, lastsourcesequence,
            firstmarketevent, lastmarketevent, calculatedat, schemaversion, calculationversion, iscomplete,
            isvalid) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20,
            $21, $22, $23, $24, $25) ON CONFLICT (serieskey, timeperiod, yearmonth, marketdataasof,
            observationid) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM accepted);
        """;

    /// <summary>SQL for try insert raw eod.</summary>
    internal const string TryInsertRawEod =
        """
        WITH accepted AS (INSERT INTO market_data.futures_eod_observation ( serieskey, yearmonth, valuedate, contractid,
            sessionstart, sessionend, openprice, highprice, lowprice, closeprice, volume, tradecount, pricevolumesum,
            observationid, firstsourcesequence, lastsourcesequence, firstmarketevent, lastmarketevent, schemaversion,
            iscomplete, isvalid) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18,
            $19, $20, $21) ON CONFLICT (serieskey, yearmonth, valuedate,
            contractid) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM accepted);
        """;

    /// <summary>Corrects session statistics while preserving close, standard deviation and intraday history.</summary>
    internal const string UpdateFuturesEodSessionStatistics =
        """
        UPDATE market_data.futures_eod_data
        SET openprice=$1, highprice=$2, lowprice=$3, volume=$4, dailypercentchange=$5, pricedirection=$6
        WHERE contractid=$7 AND valuedate=$8 AND symbol=$9;
        """;

    /// <summary>SQL for update option chain greeks.</summary>
    internal const string UpdateOptionChainGreeks =
        """
        UPDATE market_data.futures_option_chain_quote_data
        SET impliedvolatility = $1, underlyingprice = $2, delta = $3, gamma = $4, vega = $5, theta = $6, rho = $7
        WHERE underlyingcontractid = $8 AND expirydate = $9 AND valuedate = $10 AND contractid = $11 AND tickid = $12;
        """;

    /// <summary>SQL for update vix futures eod data.</summary>
    internal const string UpdateVixFuturesEodData =
        """
        UPDATE market_data.vix_futures_eod_data
        SET openprice = $1, highprice = $2, lowprice = $3, closeprice = $4, volume = $5
        WHERE contractid = $6 AND valuedate = $7;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string UpsertFuturesItiTimeFrameState =
        """
        INSERT INTO market_data.futures_iti_timeframe_state ( contractid, timeperiod, calendarbucketstart,
            timeframestartvaluedate, valuedate, sequenceid, intrinsictime, intrinsictimegroupid, intrinsictimelength,
            intrinsicprice, intrinsictimetrend, intrinsictimemode, trendprice, trendextreme, trendreversal, trenddelta,
            targetdelta, lambda, tradingdays, threshold, uptrendtrigger, downtrendtrigger, tradestate, bandanchorprice,
            bandpercentage, bandsize, bandlevel, reversallevel )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25, $26, $27, $28 )
        ON CONFLICT (contractid, timeperiod, calendarbucketstart) DO UPDATE SET timeframestartvaluedate = EXCLUDED.timeframestartvaluedate, valuedate = EXCLUDED.valuedate, sequenceid = EXCLUDED.sequenceid, intrinsictime = EXCLUDED.intrinsictime, intrinsictimegroupid = EXCLUDED.intrinsictimegroupid, intrinsictimelength = EXCLUDED.intrinsictimelength, intrinsicprice = EXCLUDED.intrinsicprice, intrinsictimetrend = EXCLUDED.intrinsictimetrend, intrinsictimemode = EXCLUDED.intrinsictimemode, trendprice = EXCLUDED.trendprice, trendextreme = EXCLUDED.trendextreme, trendreversal = EXCLUDED.trendreversal, trenddelta = EXCLUDED.trenddelta, targetdelta = EXCLUDED.targetdelta, lambda = EXCLUDED.lambda, tradingdays = EXCLUDED.tradingdays, threshold = EXCLUDED.threshold, uptrendtrigger = EXCLUDED.uptrendtrigger, downtrendtrigger = EXCLUDED.downtrendtrigger, tradestate = EXCLUDED.tradestate, bandanchorprice = EXCLUDED.bandanchorprice, bandpercentage = EXCLUDED.bandpercentage, bandsize = EXCLUDED.bandsize, bandlevel = EXCLUDED.bandlevel, reversallevel = EXCLUDED.reversallevel
        WHERE market_data.futures_iti_timeframe_state.sequenceid <= EXCLUDED.sequenceid;
        """;

    /// <summary>SQL for upsert futures option chain quote data.</summary>
    internal const string UpsertFuturesOptionChainQuoteData =
        """
        INSERT INTO market_data.futures_option_chain_quote_data ( underlyingcontractid, expirydate, valuedate,
            contractid, tickid, ticktime, optionprice, bidprice, askprice, bidsize, asksize, impliedvolatility,
            underlyingprice, delta, gamma, vega, theta, rho )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18 )
        ON CONFLICT (underlyingcontractid,expirydate,valuedate,contractid) DO UPDATE SET tickid=EXCLUDED.tickid,
            ticktime=EXCLUDED.ticktime,optionprice=EXCLUDED.optionprice,bidprice=EXCLUDED.bidprice,
            askprice=EXCLUDED.askprice,bidsize=EXCLUDED.bidsize,asksize=EXCLUDED.asksize,
            impliedvolatility=EXCLUDED.impliedvolatility,underlyingprice=EXCLUDED.underlyingprice,delta=EXCLUDED.delta,
            gamma=EXCLUDED.gamma,vega=EXCLUDED.vega,theta=EXCLUDED.theta,rho=EXCLUDED.rho;
        """;

    /// <summary>SQL for upsert historical analytics attempt projection.</summary>
    internal const string UpsertHistoricalAnalyticsAttemptProjection =
        """
        INSERT INTO historical_analytics_attempt_projection (attempt_id, event_id, payload)
        VALUES ($1, $2, $3)
        ON CONFLICT (attempt_id) DO UPDATE SET event_id = EXCLUDED.event_id, payload = EXCLUDED.payload
        WHERE historical_analytics_attempt_projection.event_id < EXCLUDED.event_id;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    internal const string UpsertMarketOutlookSnapshot =
        """
        INSERT INTO market_data.market_outlook_snapshot (contractid, valuedate, revision, updatedon, eoddata,
            futurestradesignal, missinginputs, snapshot)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
        ON CONFLICT (contractid, valuedate) DO UPDATE SET revision = EXCLUDED.revision, updatedon = EXCLUDED.updatedon,
            eoddata = EXCLUDED.eoddata, futurestradesignal = EXCLUDED.futurestradesignal,
            missinginputs = EXCLUDED.missinginputs, snapshot = EXCLUDED.snapshot;
        """;

    /// <summary>UpsertOptionChainParameterCurrent database statement.</summary>
    internal const string UpsertOptionChainParameterCurrent =
        """
        INSERT INTO market_data.strategy_option_chain_policy_current(environment,set_id,version,revision,published,parameter_hash)
        VALUES($1,$2,$3,$4,$5,$6)
        ON CONFLICT(environment,set_id,version) DO UPDATE SET revision=EXCLUDED.revision,published=EXCLUDED.published,
            parameter_hash=EXCLUDED.parameter_hash
        WHERE strategy_option_chain_policy_current.revision < EXCLUDED.revision;
        """;

    /// <summary>YieldCurveExists database statement.</summary>
    internal const string YieldCurveExists =
        """
        SELECT EXISTS(SELECT 1 FROM market_data.yield_curve_rates WHERE id=1 AND valuedate=$1);
        """;

    /// <summary>Inserts indicator seed coverage rows.</summary>
    internal const string ql_IndicatorSeedCoverage_Statement_1 =
        """
        INSERT INTO market_data.indicator_seed_coverage(contract_id,time_frame,value_date,source_version,interval_end,payload)
        SELECT $1,$2,$3,$4,interval_end,payload
        FROM unnest($5::timestamptz[],$6::bytea[]) AS qualified(interval_end,payload)
        ORDER BY interval_end
        ON CONFLICT (contract_id,time_frame,value_date,source_version,interval_end) DO NOTHING;
        """;
}
