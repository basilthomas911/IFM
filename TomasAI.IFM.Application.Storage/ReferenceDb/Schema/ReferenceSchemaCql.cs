namespace TomasAI.IFM.Application.Storage.ReferenceDb.Schema;

internal static class ReferenceSchemaCql
{
    public const string CreateEconomicCalendarTable = """
    CREATE TABLE IF NOT EXISTS economic_calendar (
    eventDate timestamp,
    countryCode text,
    eventName text,
    actual text,
    forecast text,
    prior text,
    createdOn timestamp,
    createdBy text,
    PRIMARY KEY (eventDate, countryCode, eventName)
    )
    WITH CLUSTERING ORDER BY (countryCode ASC, eventName ASC);
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

    public const string CreateSeedIdTable = """
    CREATE TABLE IF NOT EXISTS seed_id (
    SeedType text PRIMARY KEY,
    NextSeedId counter
    );
    """;
}
