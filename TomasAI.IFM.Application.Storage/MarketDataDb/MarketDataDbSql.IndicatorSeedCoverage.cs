namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public static partial class MarketDataDbSql
{
    internal const string GetQualifiedIndicatorSeedBars = $$"""
        WITH canonical AS (
            SELECT DISTINCT ON (marketDataAsOf) marketDataAsOf, observationId, intervalStart, intervalEnd,
                   openPrice, highPrice, lowPrice, closePrice, volume, tradeCount, priceVolumeSum,
                   firstSourceSequence, lastSourceSequence, firstMarketEvent, lastMarketEvent,
                   calculatedAt, schemaVersion, calculationVersion
            FROM market_data.futures_trade_session_bar
            WHERE seriesKey=$1 AND timePeriod=$2 AND yearMonth=$3 AND marketDataAsOf=ANY($4)
                AND contractId=$5 AND valueDate=$6 AND isComplete AND isValid
                AND calculationVersion NOT IN {{UnversionedProviderSeedCalculations}}
            ORDER BY marketDataAsOf,calculatedAt DESC,observationId
        )
        SELECT observationId,intervalStart,intervalEnd,openPrice,highPrice,lowPrice,closePrice,
               volume,tradeCount,priceVolumeSum,firstSourceSequence,lastSourceSequence,
               firstMarketEvent,lastMarketEvent,calculatedAt,schemaVersion,calculationVersion,NULL::bytea
        FROM canonical
        UNION ALL
        SELECT NULL,NULL,seed.interval_end,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,seed.payload
        FROM market_data.indicator_seed_coverage seed
        WHERE seed.contract_id=$5 AND seed.time_frame=$2 AND seed.value_date=$6
            AND seed.source_version=$7 AND seed.interval_end=ANY($4)
            AND NOT EXISTS (SELECT 1 FROM canonical WHERE marketDataAsOf=seed.interval_end);
        """;

    internal const string StoreQualifiedIndicatorSeedBars = """
        INSERT INTO market_data.indicator_seed_coverage(contract_id,time_frame,value_date,source_version,interval_end,payload)
        SELECT $1,$2,$3,$4,interval_end,payload
        FROM unnest($5::timestamptz[],$6::bytea[]) AS qualified(interval_end,payload)
        ORDER BY interval_end
        ON CONFLICT (contract_id,time_frame,value_date,source_version,interval_end) DO NOTHING;
        """;
}
