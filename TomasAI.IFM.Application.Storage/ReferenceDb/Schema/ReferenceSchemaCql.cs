namespace TomasAI.IFM.Application.Storage.ReferenceDb.Schema;

internal static class ReferenceSchemaCql
{
    public const string CreateTradeStrategyFamilyTable = """
    CREATE TABLE IF NOT EXISTS trade_strategy_family_v3 (
    catalog text, tradeStrategyFamilyId int, definitionVersion bigint,
    systemKey text, family text, strategy text, timeFrame text, symbol text, currency text,
    description text, state text, createdOnUtc timestamp, createdBy text,
    PRIMARY KEY ((catalog), systemKey, definitionVersion)
    ) WITH CLUSTERING ORDER BY (systemKey ASC, definitionVersion DESC);
    """;
    public const string CreateLegacyTradeStrategyFamilyTable = """
    CREATE TABLE IF NOT EXISTS trade_strategy_family_v2 (
    catalog text, tradeStrategyFamilyId int, definitionVersion bigint,
    systemKey text, name text, state text, createdOnUtc timestamp, createdBy text,
    PRIMARY KEY ((catalog), systemKey, definitionVersion)
    ) WITH CLUSTERING ORDER BY (systemKey ASC, definitionVersion DESC);
    """;
    public const string CreateReferenceProjectionStateV3Table = """
    CREATE TABLE IF NOT EXISTS reference_projection_state_v3 (
    projectionName text PRIMARY KEY,
    generation uuid,
    completed boolean,
    completedOn timestamp
    );
    """;

    public const string CreateReferenceProjectionMutationV3Table = """
    CREATE TABLE IF NOT EXISTS reference_projection_mutation_v3 (
    projectionName text,
    mutationId uuid,
    startedOn timestamp,
    PRIMARY KEY ((projectionName), mutationId)
    );
    """;

    public const string CreateReferenceProjectionOwnershipV3Table = """
    CREATE TABLE IF NOT EXISTS reference_projection_ownership_v3 (
    projectionName text PRIMARY KEY,
    ownerMutationId uuid,
    conflicted boolean,
    claimedOn timestamp
    );
    """;

    public const string CreateLookupTypeTable = """
    CREATE table if not Exists lookup_type (
    LookupTypeName text,
    ShortCode text,
    OrderId int,
    Description text,
    CreatedOn timestamp,
    CreatedBy text,
    PRIMARY KEY ((LookupTypeName), ShortCode, OrderId)
    );
    """;

    public const string CreateMDIForwardLossRatioTable = """
    CREATE TABLE IF NOT EXISTS mdi_forward_loss_ratio (
    trendDirection text,
    tradeType text,
    mdi int,
    forwardLossRatio double,
    createdBy text,
    createdOn timestamp,
    updatedBy text,
    updatedOn timestamp,
    PRIMARY KEY ((trendDirection, tradeType), mdi)
    );
    """;

    public const string CreateScheduledJobDaysTable = """
    CREATE TABLE IF NOT EXISTS scheduled_job_days (
    jobId int PRIMARY KEY,
    monday boolean,
    tuesday boolean,
    wednesday boolean,
    thursday boolean,
    friday boolean,
    saturday boolean,
    sunday boolean
    );
    """;

    public const string CreateScheduledJobTable = """
    CREATE TABLE IF NOT EXISTS scheduled_job (
    JobId int,
    JobName text,
    JobSchedule text,
    JobScheduleDate timestamp,
    JobScheduleInterval double,
    TaskName text,
    TaskEnabled boolean,
    CreatedOn timestamp,
    CreatedBy text,
    UpdatedOn timestamp,
    UpdatedBy text,
    PRIMARY KEY (JobId, JobName)
    );
    """;

    public const string CreateScheduledJobByNameV3Table = """
    CREATE TABLE IF NOT EXISTS scheduled_job_by_name_v3 (
    jobName text PRIMARY KEY,
    jobId int,
    reservationToken uuid
    );
    """;

    public const string CreateScheduledJobWriteOwnershipV3Table = """
    CREATE TABLE IF NOT EXISTS scheduled_job_write_ownership_v3 (
    scopeType text,
    scopeKey text,
    operationId uuid,
    startedOn timestamp,
    PRIMARY KEY ((scopeType, scopeKey))
    );
    """;

}
