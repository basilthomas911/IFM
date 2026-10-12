namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>Native PostgreSQL statements; positional binds are explicit and no runtime CQL translation is used.</summary>
internal static class OptionPricerDbSql
{
    public const string DeleteOptionPricerDevice = """
        DELETE FROM option_pricer.option_pricer_device 
                WHERE deviceId = $1 AND deviceName = $2;
        """;

    public const string DeleteSpreadDistribution = """
        DELETE from option_pricer.spread_distribution 
                WHERE tradeId = $1 
                AND valueDate = $2;
        """;

    public const string DeleteSpreadDistributionJobs = """
        delete from option_pricer.spread_distribution_job
                where orderId = $1
                and tradeId = $2;
        """;

    public const string DeleteSpreadDistributionJob = """
        DELETE FROM option_pricer.spread_distribution_job
                WHERE orderId = $1
                AND tradeId = $2
                AND valueDate = $3;
        """;

    public const string GetOptionPricerDevices = """
        SELECT 
                deviceId AS "DeviceId",
                deviceName AS "DeviceName",
                spreadPaths AS "SpreadPaths",
                volatilityPaths AS "VolatilityPaths",
                maxBatchSize AS "MaxBatchSize",
                optionType AS "OptionType",
                enabled AS "Enabled"
                FROM option_pricer.option_pricer_device;
        """;

    public const string GetSpreadDistribution = """
        SELECT 
                id AS "Id",
                tradeId AS "TradeId",
                valueDate AS "ValueDate",
                tradeType AS "TradeType",
                tradeStatus AS "TradeStatus",
                daysToExpiry AS "DaysToExpiry",
                forwardPrice AS "ForwardPrice",
                lossProbability AS "LossProbability",
                lossThreshold AS "LossThreshold",
                lossThresholdCount AS "LossThresholdCount",
                shortVolatility AS "ShortVolatility",
                longVolatility AS "LongVolatility",
                forwardLossRatio AS "ForwardLossRatio",
                createdOn AS "CreatedOn"
                FROM option_pricer.spread_distribution
                WHERE tradeId = $1
                AND valueDate = $2
                AND tradeType = $3
                AND tradeStatus = $4
                AND daysToExpiry = $5 ORDER BY COALESCE(sourceEventId,0) DESC,createdOn DESC,id DESC LIMIT 1;
        """;

    public const string GetAllSpreadDistributionJobs = """
        SELECT
                orderId AS "OrderId",
                tradeId AS "TradeId",
                tradeType AS "TradeType",
                tradeStatus AS "TradeStatus",
                valueDate AS "ValueDate",
                daysToExpiry AS "DaysToExpiry",
                jobSubmitted AS "JobSubmitted",
                jobStatus AS "JobStatus",
                jobCompleted AS "JobCompleted",
                jobFailed AS "JobFailed",
                inProgress AS "InProgress",
                lossProbabilityFactor AS "LossProbabilityFactor"
                FROM option_pricer.spread_distribution_job;
        """;

    public const string GetSpreadDistributionJobs = """
        SELECT 
                orderId AS "OrderId",
                tradeId AS "TradeId",
                tradeType AS "TradeType",
                tradeStatus AS "TradeStatus",
                valueDate AS "ValueDate",
                daysToExpiry AS "DaysToExpiry",
                jobSubmitted AS "JobSubmitted",
                jobStatus AS "JobStatus",
                jobCompleted AS "JobCompleted",
                jobFailed AS "JobFailed",
                inProgress AS "InProgress",
                lossProbabilityFactor AS "LossProbabilityFactor"
                FROM option_pricer.spread_distribution_job
                WHERE orderId = $1 AND tradeId = $2;
        """;

    public const string InsertOptionPricerDevice = """
        INSERT INTO option_pricer.option_pricer_device (deviceId, deviceName, spreadPaths, volatilityPaths, maxBatchSize, optionType, enabled)
                VALUES ($1, $2, $3, $4, $5, $6, $7) ON CONFLICT (deviceId, deviceName) DO UPDATE SET spreadPaths=EXCLUDED.spreadPaths, volatilityPaths=EXCLUDED.volatilityPaths, maxBatchSize=EXCLUDED.maxBatchSize, optionType=EXCLUDED.optionType, enabled=EXCLUDED.enabled;
        """;

    public const string InsertAcceptedSpreadDistribution = """
        INSERT INTO option_pricer.spread_distribution(id,tradeId,valueDate,tradeType,tradeStatus,daysToExpiry,forwardPrice,
            lossProbability,lossThreshold,lossThresholdCount,shortVolatility,longVolatility,forwardLossRatio,createdOn,sourceEventId)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
        ON CONFLICT(tradeId,valueDate,tradeType,tradeStatus,daysToExpiry,id) DO UPDATE SET
            forwardPrice=EXCLUDED.forwardPrice,lossProbability=EXCLUDED.lossProbability,lossThreshold=EXCLUDED.lossThreshold,
            lossThresholdCount=EXCLUDED.lossThresholdCount,shortVolatility=EXCLUDED.shortVolatility,longVolatility=EXCLUDED.longVolatility,
            forwardLossRatio=EXCLUDED.forwardLossRatio,createdOn=EXCLUDED.createdOn,sourceEventId=EXCLUDED.sourceEventId;
        """;

    public const string InsertSpreadDistribution = """
        INSERT INTO option_pricer.spread_distribution (
                id,
                tradeId,
                valueDate,
                tradeType,
                tradeStatus,
                daysToExpiry,
                forwardPrice,
                lossProbability,
                lossThreshold,
                lossThresholdCount,
                shortVolatility,
                longVolatility,
                forwardLossRatio,
                createdOn
                ) VALUES (
                $1,
                $2,
                $3,
                $4,
                $5,
                $6,
                $7,
                $8,
                $9,
                $10,
                $11,
                $12,
                $13,
                $14
                ) ON CONFLICT (tradeId, valueDate, tradeType, tradeStatus, daysToExpiry, id) DO NOTHING;
        """;

    public const string InsertSpreadDistributionJob = """
        INSERT INTO option_pricer.spread_distribution_job (
                orderId, tradeId, tradeType, tradeStatus, valueDate, daysToExpiry, 
                jobSubmitted, jobStatus, jobCompleted, jobFailed,
                inProgress, lossProbabilityFactor
                ) VALUES (
                $1, $2, $3, $4, $5, $6, 
                $7, $8, $9, $10,
                $11, $12
                ) ON CONFLICT (orderId, tradeId, valueDate) DO NOTHING;
        """;

    public const string UpdateSpreadDistributionJobStatus = """
        update option_pricer.spread_distribution_job
                set JobCompleted = $1,
                JobFailed = $2,
                JobStatus = $3,
                InProgress = $4
                where orderId = $5
                and tradeId = $6
                and valueDate = $7;
        """;
}
