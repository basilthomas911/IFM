namespace TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;

internal class OptionPricerDbCql
{
    public const string CreateOptionPricerDeviceTable = """
            CREATE TABLE IF NOT EXISTS option_pricer_device (
            DeviceId int,
            DeviceName text,
            SpreadPaths int,
            VolatilityPaths int,
            MaxBatchSize int,
            OptionType text,
            Enabled boolean,
            PRIMARY KEY (DeviceId, DeviceName)
        ) WITH CLUSTERING ORDER BY (DeviceName ASC);
        """;

    public const string CreateSpreadDistributionJobTable = """
            CREATE TABLE IF NOT EXISTS spread_distribution_job (
            orderId int,
            tradeId int,
            tradeType text,
            tradeStatus text,
            valueDate timestamp,
            daysToExpiry int,
            jobSubmitted timestamp,
            jobStatus text,
            jobCompleted timestamp,
            jobFailed timestamp,
            inProgress boolean,
            lossProbabilityFactor double,
            PRIMARY KEY ((orderId, tradeId), valueDate)
        ) WITH CLUSTERING ORDER BY (valueDate desc);
        """;

    public const string CreateSpreadDistributionTable = """
            CREATE TABLE IF NOT EXISTS spread_distribution (
            id bigint,
            tradeId int,
            valueDate date,
            tradeType text,
            tradeStatus text,
            daysToExpiry int,
            forwardPrice double,
            lossProbability double,
            lossThreshold decimal,
            lossThresholdCount int,
            shortVolatility double,
            longVolatility double,
            forwardLossRatio double,
            createdOn timestamp,
            PRIMARY KEY (tradeId, valueDate, tradeType, tradeStatus, daysToExpiry, id)
        ) with clustering order by (valueDate desc, tradeType asc, tradeStatus asc,daysToExpiry desc, id desc)
        """;

    public const string DeleteOptionPricerDevice = """
        DELETE FROM option_pricer_device 
        WHERE deviceId = :deviceId AND deviceName = :deviceName;
        """;

    public const string DeleteSpreadDistribution = """
        DELETE from spread_distribution 
        WHERE tradeId = :tradeId 
        AND valueDate = :valueDate
        """;

    public const string DeleteSpreadDistributionJobs = """
        delete from spread_distribution_job
        where orderId = :orderId
        and tradeId = :tradeId
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
        FROM 
        option_pricer_device;
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
        FROM spread_distribution
        WHERE tradeId = :tradeId
        AND valueDate = :valueDate
        AND tradeType = :tradeType
        AND tradeStatus = :tradeStatus
        AND daysToExpiry = :daysToExpiry Limit 1;
        """;

    public const string GetSpreadDistributionIJobIds = """
        select orderId, tradeId
        from spread_distribution_job
        group by orderId, tradeId
        """;

    public const string GetSpreadDistributionJobs = """
        SELECT 
        orderId AS "OrderId",
        tradeId AS "TradeId",
        tradeType AS "TradeType",
        tradeStatus AS "TradeStatus",
        valueDate AS "ValueDate",
        daysToExpiry AS "DaysToExpiry",
        optionStyle AS "OptionStyle",
        optionType AS "OptionType",
        jobSubmitted AS "JobSubmitted",
        jobStatus AS "JobStatus",
        jobCompleted AS "JobCompleted",
        jobFailed AS "JobFailed",
        inProgress AS "InProgress",
        lossProbabilityFactor AS "LossProbabilityFactor"
        FROM spread_distribution_job
        WHERE orderId = :orderId AND tradeId = :tradeId;
        """;

    public const string InsertIOptionPricerDevice = """
        INSERT INTO option_pricer_device (deviceId, deviceName, spreadPaths, volatilityPaths, maxBatchSize, optionType, enabled)
        VALUES (:deviceId, :deviceName, :spreadPaths, :volatilityPaths, :maxBatchSize, :optionType, :enabled);
        """;

    public const string InsertSpreadDistribution = """
        INSERT INTO spread_distribution (
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
        :id,
        :tradeId,
        :valueDate,
        :tradeType,
        :tradeStatus,
        :daysToExpiry,
        :forwardPrice,
        :lossProbability,
        :lossThreshold,
        :lossThresholdCount,
        :shortVolatility,
        :longVolatility,
        :forwardLossRatio,
        :createdOn
        ) IF NOT EXISTS;
        """;

    public const string InsertSpreadDistributionJob = """
        INSERT INTO spread_distribution_job (
        orderId, tradeId, tradeType, tradeStatus, valueDate, daysToExpiry, 
        optionStyle, optionType, jobSubmitted, jobStatus, jobCompleted, jobFailed, 
        inProgress, lossProbabilityFactor
        ) VALUES (
        :orderId, :tradeId, :tradeType, :tradeStatus, :valueDate, :daysToExpiry, 
        :optionStyle, :optionType, :jobSubmitted, :jobStatus, :jobCompleted, :jobFailed, 
        :inProgress, :lossProbabilityFactor
        ) IF NOT EXISTS;
        """;

    public const string UpdateSreadDistributionJobStatus = """
        update spread_distribution_job
        set JobCompleted = :jobCompleted,
        JobFailed = :jobFaild,
        JobStatus = :jobStatus,
        InProgress = :inProgress
        where orderId = :orderId
        and tradeId = :tradeId 
        """;
}
