namespace TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

/// <summary>Native derived-market-data tables. Raw Databento trades and quotes belong to TradeTickQuoteDb.</summary>
public static class MarketDataSchemaSql
{
    /// <summary>Creates the immutable option-chain policy versions without altering existing rows.</summary>
    public const string OptionChainVersionCreate = """
        CREATE SCHEMA IF NOT EXISTS market_data;
        CREATE TABLE IF NOT EXISTS market_data.strategy_option_chain_parameter_version (set_id uuid,version int,payload text,PRIMARY KEY (set_id,version));
        """;

    /// <summary>Creates the currently published option-chain policies without altering existing rows.</summary>
    public const string OptionChainCurrentCreate = """
        CREATE SCHEMA IF NOT EXISTS market_data;
        CREATE TABLE IF NOT EXISTS market_data.strategy_option_chain_parameter_current (environment text,set_id uuid,version int,revision bigint,published boolean,payload text,PRIMARY KEY (environment,set_id,version));
        """;

    /// <summary>Creates both option-chain policy tables for staged offline migration.</summary>
    public const string OptionChainPoliciesCreate = OptionChainVersionCreate + "\n" + OptionChainCurrentCreate + "\n";

    public const string Create = OptionChainPoliciesCreate + """
        CREATE TABLE IF NOT EXISTS market_data.ifm_recovery_probe (
            probe_id uuid PRIMARY KEY,
            observed_at_utc timestamptz
        );
        
        CREATE TABLE IF NOT EXISTS market_data.option_iv_observation_history (
         environment text,series_id text,methodology_version text,calendar_bucket int,value_date date,
         sampling_slot text,revision int,observation_id text,available_at_utc timestamptz,payload bytea,
         PRIMARY KEY (environment,series_id,methodology_version,calendar_bucket,value_date,sampling_slot,revision,observation_id));
        CREATE INDEX IF NOT EXISTS option_iv_observation_history_ordered ON market_data.option_iv_observation_history (environment, series_id, methodology_version, calendar_bucket, value_date ASC,sampling_slot ASC,revision DESC,observation_id ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.option_iv_observation_by_id (
         environment text,observation_id text,payload bytea,PRIMARY KEY (environment,observation_id));
        
        CREATE TABLE IF NOT EXISTS market_data.option_iv_metric_history (
         environment text,series_id text,methodology_version text,metric_policy_version text,calendar_bucket int,
         value_date date,sampling_slot text,revision int,snapshot_id text,available_at_utc timestamptz,
         publication_sequence bigint,payload bytea,
         PRIMARY KEY (environment,series_id,methodology_version,metric_policy_version,calendar_bucket,
         value_date,sampling_slot,revision,snapshot_id));
        CREATE INDEX IF NOT EXISTS option_iv_metric_history_ordered ON market_data.option_iv_metric_history (environment, series_id, methodology_version, metric_policy_version, calendar_bucket, value_date ASC,sampling_slot ASC,revision DESC,snapshot_id ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.option_iv_snapshot_by_id (
         environment text,snapshot_id text,snapshot_digest text,publication_sequence bigint,payload bytea,
         PRIMARY KEY (environment,snapshot_id));
        
        CREATE TABLE IF NOT EXISTS market_data.option_iv_latest (
         environment text,series_id text,methodology_version text,metric_policy_version text,
         snapshot_id text,snapshot_digest text,publication_sequence bigint,available_at_utc timestamptz,
         PRIMARY KEY (environment,series_id,methodology_version,metric_policy_version));
        
        CREATE TABLE IF NOT EXISTS market_data.option_trade_evidence (
            contract_id text,
            value_date date,
            source_id text,
            source_digest text,
            payload bytea,
            PRIMARY KEY (contract_id, value_date, source_id));
        
        CREATE TABLE IF NOT EXISTS market_data.composition_preparation (
            workflow_id uuid,
            input_revision bigint,
            payload bytea,
            PRIMARY KEY (workflow_id, input_revision));
        
        CREATE TABLE IF NOT EXISTS market_data.futures_eod_observation (
                seriesKey text, yearMonth int, valueDate date, contractId text,
                sessionStart timestamptz, sessionEnd timestamptz,
                openPrice numeric, highPrice numeric, lowPrice numeric, closePrice numeric,
                volume numeric, tradeCount bigint, priceVolumeSum numeric,
                observationId uuid, firstSourceSequence bigint, lastSourceSequence bigint,
                firstMarketEvent timestamptz, lastMarketEvent timestamptz,
                schemaVersion int, isComplete boolean, isValid boolean,
                PRIMARY KEY (seriesKey, yearMonth, valueDate, contractId)
            );
        CREATE INDEX IF NOT EXISTS futures_eod_observation_ordered ON market_data.futures_eod_observation (seriesKey, yearMonth, valueDate DESC, contractId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_trade_session_bar (
                seriesKey text, timePeriod text, yearMonth int, marketDataAsOf timestamptz,
                observationId uuid, contractId text, valueDate date,
                intervalStart timestamptz, intervalEnd timestamptz,
                openPrice numeric, highPrice numeric, lowPrice numeric, closePrice numeric,
                volume numeric, tradeCount bigint, priceVolumeSum numeric,
                firstSourceSequence bigint, lastSourceSequence bigint,
                firstMarketEvent timestamptz, lastMarketEvent timestamptz, calculatedAt timestamptz,
                schemaVersion int, calculationVersion text, isComplete boolean, isValid boolean,
                PRIMARY KEY (seriesKey, timePeriod, yearMonth, marketDataAsOf, observationId)
            );
        CREATE INDEX IF NOT EXISTS futures_trade_session_bar_ordered ON market_data.futures_trade_session_bar (seriesKey, timePeriod, yearMonth, marketDataAsOf DESC, observationId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_ema_signal (
                seriesKey text, timePeriod text, configurationId text, yearMonth int,
                marketDataAsOf timestamptz, observationId uuid, contractId text, valueDate date,
                price numeric, ema10 numeric, previousEma10 numeric, ema10Slope numeric,
                ema20 numeric, previousEma20 numeric, ema20Slope numeric,
                ema50 numeric, previousEma50 numeric, ema50Slope numeric,
                ema200 numeric, previousEma200 numeric, ema200Slope numeric, isWarm boolean,
                sourceSequence bigint, calculatedAt timestamptz, schemaVersion int,
                calculationVersion text, calculationMethod text, isValid boolean,
                PRIMARY KEY (seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId)
            );
        CREATE INDEX IF NOT EXISTS futures_ema_signal_ordered ON market_data.futures_ema_signal (seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf DESC, observationId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_bollinger_band_signal (
                seriesKey text, timePeriod text, configurationId text, yearMonth int,
                marketDataAsOf timestamptz, observationId uuid, contractId text, valueDate date,
                price numeric, ema10Center numeric, standardDeviation10 numeric,
                upper10 numeric, lower10 numeric, width10 numeric, position10 numeric,
                ema20Center numeric, standardDeviation20 numeric,
                upper20 numeric, lower20 numeric, width20 numeric, position20 numeric,
                width20Baseline numeric, width20Ratio numeric, isWarm boolean,
                sourceSequence bigint, calculatedAt timestamptz, schemaVersion int,
                calculationVersion text, calculationMethod text, isValid boolean,
                PRIMARY KEY (seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId)
            );
        CREATE INDEX IF NOT EXISTS futures_bollinger_band_signal_ordered ON market_data.futures_bollinger_band_signal (seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf DESC, observationId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_vx_term_structure_signal (
                valueDate date, configurationId text, calculatedAt timestamptz,
                frontSourceSequence bigint, backSourceSequence bigint,
                frontContractId text, frontExpiry date, frontPrice numeric,
                backContractId text, backExpiry date, backPrice numeric,
                frontBackSpread numeric, frontBackRatio numeric, termStructurePercent numeric,
                termStructureState text, priorFrontBackRatio numeric,
                priorTermStructurePercent numeric, frontSourceTimestamp timestamptz,
                backSourceTimestamp timestamptz, isWarm boolean, isValid boolean,
                schemaVersion int, calculationVersion text,
                PRIMARY KEY (valueDate, configurationId, calculatedAt,
                    frontSourceSequence, backSourceSequence)
            );
        CREATE INDEX IF NOT EXISTS futures_vx_term_structure_signal_ordered ON market_data.futures_vx_term_structure_signal (valueDate, configurationId, calculatedAt DESC,
                frontSourceSequence DESC, backSourceSequence DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_vwap_signal (
                contractId text, valueDate date, configurationId text,
                asOfUtc timestamptz, lastTradeOrdinal bigint,
                sessionStartUtc timestamptz, sessionEndUtc timestamptz,
                cumulativePriceVolume numeric, cumulativeVolume bigint,
                eligibleTradeCount bigint, rejectedTradeCount bigint,
                lastPrice numeric, vwap numeric, priceMinusVwap numeric,
                priceToVwapPercent numeric, lastTradeSourceSequence bigint,
                streamEpochId uuid, isWarm boolean, isValid boolean,
                invalidReason text, isTickExact boolean, calculationMethod text,
                schemaVersion int, calculationVersion text,
                PRIMARY KEY (contractId, valueDate, configurationId, asOfUtc, lastTradeOrdinal)
            );
        CREATE INDEX IF NOT EXISTS futures_vwap_signal_ordered ON market_data.futures_vwap_signal (contractId, valueDate, configurationId, asOfUtc DESC, lastTradeOrdinal DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.market_data_download_log (
            dataset text,
            provider text,
            scope text,
            value_date date,
            requested_at_utc timestamptz,
            import_command_id uuid,
            log_command_id uuid,
            source_terminal_event_id uuid,
            schema_version smallint,
            status text,
            started_at_utc timestamptz,
            finished_at_utc timestamptz,
            elapsed_milliseconds bigint,
            downloaded_record_count bigint,
            persisted_record_count bigint,
            error_code text,
            error_message text,
            payload_sha256 text,
            projected_at_utc timestamptz,
            PRIMARY KEY (dataset, provider, scope, value_date, requested_at_utc, import_command_id)
        );
        CREATE INDEX IF NOT EXISTS market_data_download_log_ordered ON market_data.market_data_download_log (dataset, provider, scope, value_date, requested_at_utc DESC, import_command_id DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.market_data_import_ownership (
            dataset text,
            logicalKey text,
            commandId uuid,
            mayWrite boolean,
            createdOn timestamptz,
            PRIMARY KEY (dataset, logicalKey)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.market_outlook_snapshot (
                contractId text,
                valueDate date,
                revision bigint,
                updatedOn timestamptz,
                eodData bytea,
                futuresTradeSignal bytea,
                missingInputs text,
                snapshot bytea,
                PRIMARY KEY (contractId, valueDate)
            );
        CREATE INDEX IF NOT EXISTS market_outlook_snapshot_ordered ON market_data.market_outlook_snapshot (contractId, valueDate DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.market_outlook_working_state (
                contractId text,
                valueDate date,
                revision bigint,
                updatedOn timestamptz,
                status text,
                workingState bytea,
                PRIMARY KEY (contractId, valueDate)
            );
        CREATE INDEX IF NOT EXISTS market_outlook_working_state_ordered ON market_data.market_outlook_working_state (contractId, valueDate DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.economic_calendar (
            countryCode text,
            monthBucket int,
            eventDate timestamptz,
            eventName text,
            actual text,
            forecast text,
            prior text,
            impact text,
            unit text,
            change text,
            changePercentage text,
            createdOn timestamptz,
            createdBy text,
            commandId uuid,
            PRIMARY KEY (countryCode, monthBucket, eventDate, eventName)
            );
        CREATE INDEX IF NOT EXISTS economic_calendar_ordered ON market_data.economic_calendar (countryCode, monthBucket, eventDate DESC, eventName ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.economic_calendar_country_code (
            lookupId int,
            countryCode text,
            PRIMARY KEY (lookupId, countryCode)
            );
        CREATE INDEX IF NOT EXISTS economic_calendar_country_code_ordered ON market_data.economic_calendar_country_code (lookupId, countryCode ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.trade_live_feed (
            orderId int,
            tradeId int,
            tradeLiveFeedState text,
            PRIMARY KEY (orderId, tradeId)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_iti_signal (
            contractId text,
            valueDate date,
            timePeriod text,
            sequenceId bigint,
            intrinsicTime timestamptz,
            intrinsicTimeGroupId int,
            intrinsicTimeLength double precision,
            intrinsicPrice double precision,
            intrinsicTimeTrend text,
            intrinsicTimeMode text,
            trendPrice double precision,
            trendExtreme double precision,
            trendReversal double precision,
            trendDelta double precision,
            targetDelta double precision,
            lambda double precision,
            tradingDays int,
            threshold double precision,
            upTrendTrigger double precision,
            downTrendTrigger double precision,
            tradeState text,
            bandLevel double precision,
            reversalLevel double precision,
            PRIMARY KEY (contractId, valueDate, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId, sequenceId)
            );
        CREATE INDEX IF NOT EXISTS futures_iti_signal_ordered ON market_data.futures_iti_signal (contractId, valueDate desc, timePeriod desc, intrinsicTimeMode desc, intrinsicTimeTrend desc,intrinsicTimeGroupId desc, sequenceId desc);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_iti_signal_by_contract_day (
            contractId text,
            valueDate date,
            timePeriod text,
            sequenceId bigint,
            intrinsicTime timestamptz,
            intrinsicTimeGroupId int,
            intrinsicTimeLength double precision,
            intrinsicPrice double precision,
            intrinsicTimeTrend text,
            intrinsicTimeMode text,
            trendPrice double precision,
            trendExtreme double precision,
            trendReversal double precision,
            trendDelta double precision,
            targetDelta double precision,
            lambda double precision,
            tradingDays int,
            threshold double precision,
            upTrendTrigger double precision,
            downTrendTrigger double precision,
            tradeState text,
            bandLevel double precision,
            reversalLevel double precision,
            PRIMARY KEY (contractId, valueDate, intrinsicTimeMode, sequenceId, timePeriod, intrinsicTimeTrend, intrinsicTimeGroupId)
            );
        CREATE INDEX IF NOT EXISTS futures_iti_signal_by_contract_day_ordered ON market_data.futures_iti_signal_by_contract_day (contractId, valueDate, intrinsicTimeMode ASC, sequenceId DESC, timePeriod ASC, intrinsicTimeTrend ASC, intrinsicTimeGroupId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_iti_signal_by_contract_month (
            contractId text,
            yearMonth int,
            valueDate date,
            timePeriod text,
            sequenceId bigint,
            intrinsicTime timestamptz,
            intrinsicTimeGroupId int,
            intrinsicTimeLength double precision,
            intrinsicPrice double precision,
            intrinsicTimeTrend text,
            intrinsicTimeMode text,
            trendPrice double precision,
            trendExtreme double precision,
            trendReversal double precision,
            trendDelta double precision,
            targetDelta double precision,
            lambda double precision,
            tradingDays int,
            threshold double precision,
            upTrendTrigger double precision,
            downTrendTrigger double precision,
            tradeState text,
            bandLevel double precision,
            reversalLevel double precision,
            PRIMARY KEY (contractId, yearMonth, valueDate, sequenceId, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId)
            );
        CREATE INDEX IF NOT EXISTS futures_iti_signal_by_contract_month_ordered ON market_data.futures_iti_signal_by_contract_month (contractId, yearMonth, valueDate DESC, sequenceId DESC, timePeriod ASC, intrinsicTimeMode ASC, intrinsicTimeTrend ASC, intrinsicTimeGroupId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_iti_signal_by_trend_mode_month (
            contractId text,
            yearMonth int,
            valueDate date,
            timePeriod text,
            sequenceId bigint,
            intrinsicTime timestamptz,
            intrinsicTimeGroupId int,
            intrinsicTimeLength double precision,
            intrinsicPrice double precision,
            intrinsicTimeTrend text,
            intrinsicTimeMode text,
            trendPrice double precision,
            trendExtreme double precision,
            trendReversal double precision,
            trendDelta double precision,
            targetDelta double precision,
            lambda double precision,
            tradingDays int,
            threshold double precision,
            upTrendTrigger double precision,
            downTrendTrigger double precision,
            tradeState text,
            bandLevel double precision,
            reversalLevel double precision,
            PRIMARY KEY (contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, valueDate, sequenceId, timePeriod, intrinsicTimeGroupId)
            );
        CREATE INDEX IF NOT EXISTS futures_iti_signal_by_trend_mode_month_ordered ON market_data.futures_iti_signal_by_trend_mode_month (contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, valueDate DESC, sequenceId DESC, timePeriod ASC, intrinsicTimeGroupId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_iti_timeframe_state (
              contractId text,
              timePeriod text,
              calendarBucketStart date,
              timeFrameStartValueDate date,
              valueDate date,
              sequenceId bigint,
              intrinsicTime timestamptz,
              intrinsicTimeGroupId int,
              intrinsicTimeLength double precision,
              intrinsicPrice double precision,
              intrinsicTimeTrend text,
              intrinsicTimeMode text,
              trendPrice double precision,
              trendExtreme double precision,
              trendReversal double precision,
              trendDelta double precision,
              targetDelta double precision,
              lambda double precision,
              tradingDays int,
              threshold double precision,
              upTrendTrigger double precision,
              downTrendTrigger double precision,
              tradeState text,
              bandAnchorPrice double precision,
              bandPercentage double precision,
              bandSize double precision,
              bandLevel double precision,
              reversalLevel double precision,
              PRIMARY KEY (contractId, timePeriod, calendarBucketStart)
              );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_option_chain_quote_data (
                underlyingContractId text,
                expiryDate date,
                valueDate date,
                contractId text,
                tickId bigint,
                tickTime time,
                optionPrice double precision,
                bidPrice double precision,
                askPrice double precision,
                bidSize int,
                askSize int,
                impliedVolatility double precision,
                underlyingPrice double precision,
                delta double precision,
                gamma double precision,
                vega double precision,
                theta double precision,
                rho double precision,
                PRIMARY KEY (underlyingContractId, expiryDate, valueDate, contractId)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_option_tick_price_data (
            contractId text,
            valueDate date,
            tickId bigint,
            tickTime time,
            optionPrice double precision,
            bidPrice double precision,
            askPrice double precision,
            bidSize int,
            askSize int,
            impliedVolatility double precision,
            underlyingPrice double precision,
            delta double precision,
            gamma double precision,
            vega double precision,
            theta double precision,
            rho double precision,
            PRIMARY KEY (contractId, valueDate, tickId)
            );
        CREATE INDEX IF NOT EXISTS futures_option_tick_price_data_ordered ON market_data.futures_option_tick_price_data (contractId, valueDate ASC, tickId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_bar_data (
            contractId text,
            symbol text,
            valueDate date,
            barDate timestamptz,
            barRateType text,
            barValue numeric,
            upTrendTrigger double precision,
            downTrendTrigger double precision,
            PRIMARY KEY (contractId, symbol, valueDate, barDate)
            );
        CREATE INDEX IF NOT EXISTS futures_bar_data_ordered ON market_data.futures_bar_data (contractId, symbol ASC, valueDate DESC,barDate ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_closing_price (
            contractId text,
            valueDate date,
            closingPrice numeric,
            createdOn timestamptz,
            createdBy text,
            PRIMARY KEY (contractId, valueDate)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_eod_data (
            contractId text,
            valueDate date,
            symbol text,
            openPrice numeric,
            highPrice numeric,
            lowPrice numeric,
            closePrice numeric,
            volume bigint,
            dailyPercentChange double precision,
            dailyStdDev double precision,
            dailyStdDevAmount double precision,
            upperBand double precision,
            mean double precision,
            lowerBand double precision,
            marketDirection text,
            marketVolatility text,
            priceDirection text,
            priceVolatility text,
            marketDirectionIndicator double precision,
            windowSize int,
            fiftyDMA numeric,
            twoHundredDMA numeric,
            PRIMARY KEY (contractId, valueDate, symbol)
            );
        CREATE INDEX IF NOT EXISTS futures_eod_data_ordered ON market_data.futures_eod_data (contractId, valueDate DESC, symbol ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_eod_data_by_month (
            yearMonth int,
            contractId text,
            valueDate date,
            symbol text,
            openPrice numeric,
            highPrice numeric,
            lowPrice numeric,
            closePrice numeric,
            volume bigint,
            dailyPercentChange double precision,
            dailyStdDev double precision,
            dailyStdDevAmount double precision,
            upperBand double precision,
            mean double precision,
            lowerBand double precision,
            marketDirection text,
            marketVolatility text,
            priceDirection text,
            priceVolatility text,
            marketDirectionIndicator double precision,
            windowSize int,
            fiftyDMA numeric,
            twoHundredDMA numeric,
            PRIMARY KEY (yearMonth, valueDate, contractId, symbol)
            );
        CREATE INDEX IF NOT EXISTS futures_eod_data_by_month_ordered ON market_data.futures_eod_data_by_month (yearMonth, valueDate DESC, contractId ASC, symbol ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_intra_day_data (
            contractId text,
            valueDate date,
            sequenceId bigint,
            symbol text,
            openPrice numeric,
            highPrice numeric,
            lowPrice numeric,
            closePrice numeric,
            volume bigint,
            dailyPercentChange double precision,
            dailyStdDev double precision,
            dailyStdDevAmount double precision,
            upperBand double precision,
            mean double precision,
            lowerBand double precision,
            marketDirection text,
            marketVolatility text,
            priceDirection text,
            priceVolatility text,
            marketDirectionIndicator double precision,
            windowSize int,
            PRIMARY KEY (contractId, valueDate, sequenceId)
            );
        CREATE INDEX IF NOT EXISTS futures_intra_day_data_ordered ON market_data.futures_intra_day_data (contractId, valueDate DESC, sequenceId DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.vix_futures_eod_data (
            contractId text,
            valueDate date,
            openPrice numeric,
            highPrice numeric,
            lowPrice numeric,
            closePrice numeric,
            volume bigint,
            PRIMARY KEY (contractId, valueDate)
            );
        CREATE INDEX IF NOT EXISTS vix_futures_eod_data_ordered ON market_data.vix_futures_eod_data (contractId, valueDate DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.vix_futures_contract_index (
            bucket int,
            contractId text,
            PRIMARY KEY (bucket, contractId)
            );
        CREATE INDEX IF NOT EXISTS vix_futures_contract_index_ordered ON market_data.vix_futures_contract_index (bucket, contractId ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.market_data_projection_month (
            projectionName text,
            yearMonth int,
            PRIMARY KEY (projectionName, yearMonth)
            );
        CREATE INDEX IF NOT EXISTS market_data_projection_month_ordered ON market_data.market_data_projection_month (projectionName, yearMonth DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.market_data_projection_state (
            projectionName text PRIMARY KEY,
            generation uuid,
            isReady boolean,
            blocked boolean,
            activeOperations uuid[],
            sourceRowCount bigint,
            projectedRowCount bigint,
            sourceFingerprint text,
            projectedFingerprint text,
            completedOn timestamptz
            );
        
        CREATE TABLE IF NOT EXISTS market_data.market_data_projection_mutation (
            projectionName text,
            mutationId uuid,
            startedOn timestamptz,
            PRIMARY KEY (projectionName, mutationId)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.market_data_projection_scope_state (
            projectionName text,
            scopeKey text,
            generation uuid,
            isReady boolean,
            blocked boolean,
            activeOperations uuid[],
            completedOn timestamptz,
            PRIMARY KEY (projectionName, scopeKey)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.market_data_projection_scope_mutation (
            projectionName text,
            scopeKey text,
            mutationId uuid,
            startedOn timestamptz,
            PRIMARY KEY (projectionName, scopeKey, mutationId)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_trade_signal (
            contractId text,
            valueDate date,
            timePeriod text,
            timestamp time,
            sequenceId bigint,
            mean double precision,
            stdDev double precision,
            futuresPrice double precision,
            priceChangePercent double precision,
            fundRiskPercent double precision,
            rsi double precision,
            rsiSlope double precision,
            trendType text,
            trendStrength text,
            tradeSignal text,
            tdi text,
            tdiStrength text,
            mdi double precision,
            mdiTrend text,
            mdiUpTrendLimit double precision,
            mdiDownTrendLimit double precision,
            upTrendingTrigger double precision,
            downTrendingTrigger double precision,
            entryTrigger double precision,
            exitTrigger double precision,
            trendDelta double precision,
            trendExtreme double precision,
            trendReversal double precision,
            fiftyDMA numeric,
            twoHundredDMA numeric,
            tradeExecuteState text,
            PRIMARY KEY (contractId, valueDate, timePeriod, timestamp, sequenceId)
            );
        CREATE INDEX IF NOT EXISTS futures_trade_signal_ordered ON market_data.futures_trade_signal (contractId, timestamp, valueDate DESC, timePeriod DESC, timestamp DESC, sequenceId DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_trade_signal_lookup_by_scope (
            scope text,
            entryId text,
            sequenceId bigint,
            contractId text,
            valueDate date,
            timePeriod text,
            PRIMARY KEY (scope, entryId)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_trade_signal_quarantine (
            fingerprint text,
            sourcePayload text,
            reason text,
            quarantinedOn timestamptz,
            PRIMARY KEY (fingerprint)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_eod_data_index (
            valueDate date,
            contractId text,
            PRIMARY KEY (valueDate, contractId)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.futures_iti_signal_index (
            valueDate date,
            contractId text,
            PRIMARY KEY (valueDate, contractId)
            );
        
        CREATE TABLE IF NOT EXISTS market_data.yield_curve_rates (
            id int,
            valueDate date,
            oneMonth double precision,
            twoMonth double precision,
            threeMonth double precision,
            sixMonth double precision,
            oneYear double precision,
            twoYear double precision,
            threeYear double precision,
            fiveYear double precision,
            sevenYear double precision,
            tenYear double precision,
            twentyYear double precision,
            thirtyYear double precision,
            PRIMARY KEY (id, valueDate)
            );
        CREATE INDEX IF NOT EXISTS yield_curve_rates_ordered ON market_data.yield_curve_rates (id, valueDate DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.yield_curve_rate_by_date (
            lookupId int,
            valueDate date,
            oneMonth double precision,
            twoMonth double precision,
            threeMonth double precision,
            sixMonth double precision,
            oneYear double precision,
            twoYear double precision,
            threeYear double precision,
            fiveYear double precision,
            sevenYear double precision,
            tenYear double precision,
            twentyYear double precision,
            thirtyYear double precision,
            PRIMARY KEY (lookupId, valueDate)
            );
        CREATE INDEX IF NOT EXISTS yield_curve_rate_by_date_ordered ON market_data.yield_curve_rate_by_date (lookupId, valueDate DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.yield_curve_rate_year (
            lookupId int,
            rateYear int,
            PRIMARY KEY (lookupId, rateYear)
            );
        CREATE INDEX IF NOT EXISTS yield_curve_rate_year_ordered ON market_data.yield_curve_rate_year (lookupId, rateYear DESC);
        
        CREATE INDEX IF NOT EXISTS vix_futures_eod_value_date ON market_data.vix_futures_eod_data(valueDate,contractId);

        CREATE INDEX IF NOT EXISTS futures_eod_value_date ON market_data.futures_eod_data(valueDate DESC,contractId,symbol);

        CREATE TABLE IF NOT EXISTS market_data.rate_of_return (
            symbol TEXT,
            valueDate DATE,
            rateOfReturn double precision,
            PRIMARY KEY (symbol, valueDate)
            );
        CREATE INDEX IF NOT EXISTS rate_of_return_ordered ON market_data.rate_of_return (symbol, valueDate DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_rsi_signal (
                    contractId TEXT,
                    valueDate DATE,
                    timestamp TIME,
                    averagePriceGain numeric,
                    averagePriceLoss numeric,
                    periodLength INT,
                    price numeric,
                    priceChange numeric,
                    priceGain numeric,
                    priceLoss numeric,
                    rs double precision,
                    rsi double precision,
                    rsiAverage double precision,
                    rsiSlope double precision,
                    sourceSequence BIGINT,
                    sourceEventTimestamp timestamptz,
                    configurationId TEXT,
                    observationId UUID,
                    marketDataAsOf timestamptz,
                    calculationVersion TEXT,
                    calculationMethod TEXT,
                    schemaVersion INT,
                    isValid BOOLEAN,
                    previousRsi double precision,
                    regimeSlope double precision,
                    isWarm BOOLEAN,
                    signalType TEXT,
                    timePeriod TEXT,
                    windowSize INT,
                    PRIMARY KEY (contractId, timePeriod, periodLength, valueDate, timestamp)
                );
        CREATE INDEX IF NOT EXISTS futures_rsi_signal_ordered ON market_data.futures_rsi_signal (contractId, timePeriod, periodLength, timestamp, valueDate DESC, timestamp DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_tdi_signal (
                    contractId TEXT,
                    valueDate DATE,
                    timestamp TIME,
                    downTrendCount INT,
                    tdi TEXT,
                    tdiStrength TEXT,
                    timePeriod TEXT,
                    upTrendCount INT,
                    PRIMARY KEY (contractId, valueDate, timestamp)
                );
        CREATE INDEX IF NOT EXISTS futures_tdi_signal_ordered ON market_data.futures_tdi_signal (contractId, timestamp, valueDate DESC, timestamp DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_traders_dynamic_index_signal (
                    contractId TEXT,
                    timePeriod TEXT,
                    configurationId TEXT,
                    valueDate DATE,
                    timestamp TIME,
                    schemaVersion INT,
                    rsiPeriod INT,
                    priceLinePeriod INT,
                    signalLinePeriod INT,
                    marketBasePeriod INT,
                    volatilityBandPeriod INT,
                    volatilityBandDeviation double precision,
                    price numeric,
                    rsi double precision,
                    priceLine double precision,
                    signalLine double precision,
                    marketBaseLine double precision,
                    upperVolatilityBand double precision,
                    lowerVolatilityBand double precision,
                    bandWidth double precision,
                    priceSignalDivergence double precision,
                    crossType TEXT,
                    marketState TEXT,
                    trendDirection TEXT,
                    trendStrength TEXT,
                    sourceSequence BIGINT,
                    sourceEventTimestamp timestamptz,
                    PRIMARY KEY (contractId, timePeriod, configurationId, valueDate, timestamp)
                );
        CREATE INDEX IF NOT EXISTS futures_traders_dynamic_index_signal_ordered ON market_data.futures_traders_dynamic_index_signal (contractId, timePeriod, configurationId, timestamp, valueDate DESC, timestamp DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_macd_signal (
                    contractId TEXT,
                    valueDate DATE,
                    timePeriod TEXT,
                    signalEmaPeriod INT,
                    fastEmaPeriod INT,
                    slowEmaPeriod INT,
                    timestamp TIME,
                    futuresPrice numeric,
                    fastEma double precision,
                    slowEma double precision,
                    macdLine double precision,
                    signalLine double precision,
                    histogram double precision,
                    macd TEXT,
                    macdStrength TEXT,
                    configurationId TEXT,
                    observationId UUID,
                    marketDataAsOf timestamptz,
                    sourceSequence BIGINT,
                    calculationVersion TEXT,
                    calculationMethod TEXT,
                    schemaVersion INT,
                    isValid BOOLEAN,
                    isWarm BOOLEAN,
                    observationCount INT,
                    PRIMARY KEY (contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate, timestamp)
                );
        CREATE INDEX IF NOT EXISTS futures_macd_signal_ordered ON market_data.futures_macd_signal (contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, timestamp, valueDate DESC, timestamp DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_adx_signal (
                    contractId TEXT,
                    valueDate DATE,
                    timePeriod TEXT,
                    periodLength INT,
                    timestamp TIME,
                    futuresPrice numeric,
                    plusDI double precision,
                    minusDI double precision,
                    adxValue double precision,
                    adx TEXT,
                    adxStrength TEXT,
                    configurationId TEXT,
                    observationId UUID,
                    marketDataAsOf timestamptz,
                    sourceSequence BIGINT,
                    calculationVersion TEXT,
                    calculationMethod TEXT,
                    schemaVersion INT,
                    isValid BOOLEAN,
                    PRIMARY KEY (contractId, timePeriod, periodLength, valueDate, timestamp)
                );
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS isWarm BOOLEAN;
        CREATE INDEX IF NOT EXISTS futures_adx_signal_ordered ON market_data.futures_adx_signal (contractId, timePeriod, periodLength, timestamp, valueDate DESC, timestamp DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.futures_atr_signal (
                    contractId TEXT,
                    valueDate DATE,
                    timePeriod TEXT,
                    periodLength INT,
                    timestamp TIME,
                    futuresPrice numeric,
                    atrValue double precision,
                    trueRange double precision,
                    atr TEXT,
                    atrStrength TEXT,
                    configurationId TEXT,
                    observationId UUID,
                    marketDataAsOf timestamptz,
                    sourceSequence BIGINT,
                    calculationVersion TEXT,
                    calculationMethod TEXT,
                    schemaVersion INT,
                    isValid BOOLEAN,
                    previousAtrValue double precision,
                    atrBaseline double precision,
                    atrRatio double precision,
                    isWarm BOOLEAN,
                    PRIMARY KEY (contractId, timePeriod, periodLength, valueDate, timestamp)
                );
        CREATE INDEX IF NOT EXISTS futures_atr_signal_ordered ON market_data.futures_atr_signal (contractId, timePeriod, periodLength, timestamp, valueDate DESC, timestamp DESC);
        
        CREATE TABLE IF NOT EXISTS market_data.market_holiday (
                    currencyType TEXT,
                    holidayDate DATE,
                    description TEXT,
                    PRIMARY KEY (currencyType, holidayDate)
                );
        CREATE INDEX IF NOT EXISTS market_holiday_ordered ON market_data.market_holiday (currencyType, holidayDate ASC);
        
        CREATE TABLE IF NOT EXISTS market_data.normal_curve_data (
                    stdDevIndex double precision PRIMARY KEY,
                    percent double precision
                );
        
        ALTER TABLE market_data.market_outlook_snapshot ADD COLUMN IF NOT EXISTS snapshot bytea;
        
        ALTER TABLE market_data.futures_option_chain_quote_data ADD COLUMN IF NOT EXISTS volume bigint;
        
        ALTER TABLE market_data.futures_option_chain_quote_data ADD COLUMN IF NOT EXISTS openInterest bigint;
        
        ALTER TABLE market_data.futures_option_chain_quote_data ADD COLUMN IF NOT EXISTS volumeValueDate date;
        
        ALTER TABLE market_data.futures_option_chain_quote_data ADD COLUMN IF NOT EXISTS openInterestValueDate date;
        
        ALTER TABLE market_data.futures_eod_data ADD COLUMN IF NOT EXISTS fiftyDMA numeric;
        
        ALTER TABLE market_data.futures_eod_data ADD COLUMN IF NOT EXISTS twoHundredDMA numeric;
        
        ALTER TABLE market_data.futures_eod_data_by_month ADD COLUMN IF NOT EXISTS fiftyDMA numeric;
        
        ALTER TABLE market_data.futures_eod_data_by_month ADD COLUMN IF NOT EXISTS twoHundredDMA numeric;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS sourceSequence BIGINT;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS sourceEventTimestamp timestamptz;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS configurationId TEXT;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS observationId UUID;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS marketDataAsOf timestamptz;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS calculationVersion TEXT;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS calculationMethod TEXT;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS schemaVersion INT;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS isValid BOOLEAN;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS previousRsi double precision;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS regimeSlope double precision;
        
        ALTER TABLE market_data.futures_rsi_signal ADD COLUMN IF NOT EXISTS isWarm BOOLEAN;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS configurationId TEXT;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS observationId UUID;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS marketDataAsOf timestamptz;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS sourceSequence BIGINT;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS calculationVersion TEXT;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS calculationMethod TEXT;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS schemaVersion INT;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS isValid BOOLEAN;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS isWarm BOOLEAN;
        
        ALTER TABLE market_data.futures_macd_signal ADD COLUMN IF NOT EXISTS observationCount INT;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS configurationId TEXT;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS observationId UUID;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS marketDataAsOf timestamptz;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS sourceSequence BIGINT;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS calculationVersion TEXT;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS calculationMethod TEXT;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS schemaVersion INT;
        
        ALTER TABLE market_data.futures_adx_signal ADD COLUMN IF NOT EXISTS isValid BOOLEAN;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS configurationId TEXT;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS observationId UUID;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS marketDataAsOf timestamptz;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS sourceSequence BIGINT;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS calculationVersion TEXT;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS calculationMethod TEXT;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS schemaVersion INT;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS isValid BOOLEAN;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS previousAtrValue double precision;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS atrBaseline double precision;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS atrRatio double precision;
        
        ALTER TABLE market_data.futures_atr_signal ADD COLUMN IF NOT EXISTS isWarm BOOLEAN;
        
        CREATE INDEX IF NOT EXISTS futures_rsi_signal_signaltype ON market_data.futures_rsi_signal (signalType);
        """;
}
