namespace TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;

internal static class OptionPricerSchemaCql
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
}
