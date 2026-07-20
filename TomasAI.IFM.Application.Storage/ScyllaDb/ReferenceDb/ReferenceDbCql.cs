namespace TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;

internal class ReferenceDbCql
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

    public const string DeleteEconomicCalendar = """
    delete from economic_calendar
    where EventDate = :eventDate
    and CountryCode = :countryCode
    and EventName = :eventName;
    """;

    public const string DeleteLookupType = """
    delete from lookup_type
    where LookupTypeName = :lookupTypeName
    and OrderId = :orderId;
    """;

    public const string DeleteMDIForwardLossRatio = """
    DELETE FROM mdi_forward_loss_ratio
    WHERE trendDirection = :trendDirection
    AND tradeType = :tradeType;
    """;

    public const string DeleteScheduledJob = """
    begin batch;
    delete from scheduled_job where JobId = :jobId;
    delete from scheduled_job_days where JobId = :jobId;
    apply batch;
    """;

    public const string GetEconomicCalendarById = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar
    where EventDate = :eventDate
    and CountryCode = :countryCode
    and EventName = :eventName
    """;

    public const string GetEconomicCalendarCountryCodes = """
    select countryCode as "CountryCode"
    from economic_calendar
    """;

    public const string GetEconomicCalendars = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar
    WHERE eventDate >= :startDate
    AND eventDate <= :endDate
    and CountryCode in (:countryCode)
    allow filtering;
    """;

    public const string GetEconomicCalendarsAll = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar
    allow filtering;
    """;

    public const string GetLookupType = """
    SELECT lookupTypeName AS "LookupTypeName", shortCode AS "ShortCode", orderId AS "OrderId", description AS "Description", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM lookup_type
    WHERE lookupTypeName = :lookupTypeName;
    """;

    public const string GetLookupTypeById = """
    SELECT lookupTypeName AS "LookupTypeName", shortCode AS "ShortCode", orderId AS "OrderId", description AS "Description", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM lookup_type
    WHERE lookupTypeName = :lookupTypeName
    AND orderId = :orderId;
    """;

    public const string GetLookupTypeNames = """
    select distinct lookupTypeName as "LookupTypeName"
    from lookup_type;
    """;

    public const string GetLookupTypes = """
    SELECT lookupTypeName AS "LookupTypeName", shortCode AS "ShortCode", orderId AS "OrderId", description AS "Description", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM lookup_type;
    """;

    public const string GetLookupTypeShortCodes = """
    select
    ShortCode as "ShortCode",
    OrderId as "OrderId"
    from lookup_type
    where LookupTypeName = :lookupTypeName
    order by OrderId;
    """;

    public const string GetMDIForwardLossRatios = """
    SELECT trendDirection AS "TrendDirection", tradeType AS "TradeType", mdi AS "MDI", forwardLossRatio AS "ForwardLossRatio", createdBy AS "CreatedBy", createdOn AS "CreatedOn", updatedBy AS "UpdatedBy", updatedOn AS "UpdatedOn"
    FROM mdi_forward_loss_ratio
    WHERE trendDirection = :trendDirection
    AND tradeType = :tradeType;
    """;

    public const string GetNextSeedId = """
    select NextSeedId as "Value" 
    from seed_id 
    where SeedType = :seedType;
    """;

    public const string GetScheduledJob = """
    SELECT jobId AS "JobId", jobName AS "JobName", jobSchedule AS "JobSchedule", jobScheduleDate AS "JobScheduleDate", jobScheduleInterval AS "JobScheduleInterval", taskName AS "TaskName", taskEnabled AS "TaskEnabled", createdOn AS "CreatedOn", createdBy AS "CreatedBy", updatedOn AS "UpdatedOn", updatedBy AS "UpdatedBy"
    FROM scheduled_job
    WHERE jobId = :jobId;
    """;

    public const string GetScheduledJobDays = """
    SELECT jobId AS "JobId", monday AS "Monday", tuesday AS "Tuesday", wednesday AS "Wednesday", thursday AS "Thursday", friday AS "Friday", saturday AS "Saturday", sunday AS "Sunday"
    FROM scheduled_job_days
    WHERE jobId = :jobId;
    """;

    public const string GetScheduledJobId = """
    SELECT JobId 
    from scheduled_job 
    where JobName = :jobId
    allow filtering;
    """;

    public const string GetScheduledJobs = """
    SELECT jobId AS "JobId", jobName AS "JobName", jobSchedule AS "JobSchedule", jobScheduleDate AS "JobScheduleDate", jobScheduleInterval AS "JobScheduleInterval", taskName AS "TaskName", taskEnabled AS "TaskEnabled", createdOn AS "CreatedOn", createdBy AS "CreatedBy", updatedOn AS "UpdatedOn", updatedBy AS "UpdatedBy"
    FROM scheduled_job;
    """;

    public const string InsertEconomicCalendar = """
    INSERT INTO economic_calendar (eventDate, countryCode, eventName, actual, forecast, prior, createdOn, createdBy)
    VALUES (:eventDate, :countryCode, :eventName, :actual, :forecast, :prior, :createdOn, :createdBy);
    """;

    public const string InsertLookupType = """
    INSERT INTO lookup_type (lookupTypeName, shortCode, orderId, description, createdOn, createdBy)
    VALUES (:lookupTypeName, :shortCode, :orderId, :description, :createdOn, :createdBy);
    """;

    public const string InsertMDIForwardLossRatio = """
    INSERT INTO mdi_forward_loss_ratio (mdi, trendDirection, tradeType, forwardLossRatio, createdBy, createdOn, updatedBy, updatedOn)
    VALUES (:mdi, :trendDirection, :tradeType, :forwardLossRatio, :createdBy, :createdOn, :updatedBy, :updatedOn);
    """;

    public const string InsertScheduledJob = """
    INSERT INTO scheduled_job (jobId, jobName, jobSchedule, jobScheduleDate, jobScheduleInterval, taskName, taskEnabled, createdOn, createdBy, updatedOn, updatedBy)
    VALUES (:jobId, :jobName, :jobSchedule, :jobScheduleDate, :jobScheduleInterval, :taskName, :taskEnabled, :createdOn, :createdBy, :updatedOn, :updatedBy);
    """;

    public const string InsertScheduledJobDays = """
    INSERT INTO scheduled_job_days (jobId, monday, tuesday, wednesday, thursday, friday, saturday, sunday)
    VALUES (:jobId, :monday, :tuesday, :wednesday, :thursday, :friday, :saturday, :sunday);
    """;

    public const string UpdateNextSeedId = """
    UPDATE seed_id
    SET NextSeedId = NextSeedId + 1
    WHERE SeedType = :seedType;
    """;
}
