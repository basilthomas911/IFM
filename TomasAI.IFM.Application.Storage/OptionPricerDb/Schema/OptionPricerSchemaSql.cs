namespace TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;

/// <summary>Complete PostgreSQL schema catalog for option-pricer tables, indexes and additive changes.</summary>
internal static class OptionPricerSchemaSql
{
    public const string Create = """
        CREATE SCHEMA IF NOT EXISTS option_pricer;
        CREATE TABLE IF NOT EXISTS option_pricer.option_pricer_device (
            DeviceId int,
            DeviceName text,
            SpreadPaths int,
            VolatilityPaths int,
            MaxBatchSize int,
            OptionType text,
            Enabled boolean,
            PRIMARY KEY (DeviceId, DeviceName)
             );
        CREATE TABLE IF NOT EXISTS option_pricer.spread_distribution_job (
            orderId int,
            tradeId int,
            tradeType text,
            tradeStatus text,
            valueDate date,
            daysToExpiry int,
            jobSubmitted timestamptz,
            jobStatus text,
            jobCompleted timestamptz,
            jobFailed timestamptz,
            inProgress boolean,
            lossProbabilityFactor double precision,
            PRIMARY KEY (orderId, tradeId, valueDate)
             );
        CREATE TABLE IF NOT EXISTS option_pricer.spread_distribution (
            id bigint,
            tradeId int,
            valueDate date,
            tradeType text,
            tradeStatus text,
            daysToExpiry int,
            forwardPrice double precision,
            lossProbability double precision,
            lossThreshold numeric,
            lossThresholdCount int,
            shortVolatility double precision,
            longVolatility double precision,
            forwardLossRatio double precision,
            createdOn timestamptz,
            PRIMARY KEY (tradeId, valueDate, tradeType, tradeStatus, daysToExpiry, id)
             );
        ALTER TABLE option_pricer.spread_distribution ADD COLUMN IF NOT EXISTS sourceEventId bigint;
        CREATE INDEX IF NOT EXISTS ix_spread_distribution_accepted_latest ON option_pricer.spread_distribution (tradeId,valueDate,tradeType,tradeStatus,daysToExpiry,sourceEventId DESC,createdOn DESC,id DESC);
        CREATE INDEX IF NOT EXISTS ix_spread_distribution_job_progress ON option_pricer.spread_distribution_job (orderId, tradeId) WHERE inProgress;
        CREATE INDEX IF NOT EXISTS ix_spread_distribution_latest ON option_pricer.spread_distribution (tradeId, valueDate, tradeType, tradeStatus, daysToExpiry, id DESC);
        """;
}
