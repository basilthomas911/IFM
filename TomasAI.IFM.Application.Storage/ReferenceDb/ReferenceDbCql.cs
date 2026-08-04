using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Application.Storage.ReferenceDb;

internal class ReferenceDbCql
{
    public const string DeleteEconomicCalendar = """
    delete from economic_calendar
    where EventDate = :eventDate
    and CountryCode = :countryCode
    and EventName = :eventName;
    """;

    public const string DeleteEconomicCalendarByCountryMonthV2 = """
    DELETE FROM economic_calendar_by_country_month_v2
    WHERE countryCode = :countryCode
    AND monthBucket = :monthBucket
    AND eventDate = :eventDate
    AND eventName = :eventName;
    """;

    public const string DeleteEconomicCalendarCountryMonthV2 = """
    DELETE FROM economic_calendar_by_country_month_v2
    WHERE countryCode = :countryCode
    AND monthBucket = :monthBucket;
    """;

    public const string DeleteReferenceProjectionStateV3 = """
    DELETE FROM reference_projection_state_v3
    WHERE projectionName = :projectionName;
    """;

    public const string DeleteReferenceProjectionMutationV3 = """
    DELETE FROM reference_projection_mutation_v3
    WHERE projectionName = :projectionName AND mutationId = :mutationId;
    """;

    public const string DeleteReferenceProjectionMutationsV3 = """
    DELETE FROM reference_projection_mutation_v3
    WHERE projectionName = :projectionName;
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
    DELETE FROM scheduled_job
    WHERE jobId = :jobId;
    """;

    public const string DeleteScheduledJobDays = """
    DELETE FROM scheduled_job_days
    WHERE jobId = :jobId;
    """;

    public const string DeleteScheduledJobByNameV3ForOfflineRepair = """
    DELETE FROM scheduled_job_by_name_v3
    WHERE jobName = :jobName;
    """;

    public const string ReleaseScheduledJobNameV3 = """
    DELETE FROM scheduled_job_by_name_v3
    WHERE jobName = :jobName
    IF jobId = :jobId
    AND reservationToken = :reservationToken;
    """;

    public const string ReleaseScheduledJobWriteOwnershipV3 = """
    DELETE FROM scheduled_job_write_ownership_v3
    WHERE scopeType = :scopeType
    AND scopeKey = :scopeKey
    IF operationId = :operationId;
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
    FROM economic_calendar_by_country_month_v2
    WHERE countryCode = :countryCode
    AND monthBucket = :monthBucket
    AND eventDate >= :startDate
    AND eventDate <= :endDate;
    """;

    public const string GetEconomicCalendarsAll = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar;
    """;

    public const string GetEconomicCalendarKeysAll = """
    SELECT countryCode AS "CountryCode", eventDate AS "EventDate", eventName AS "EventName",
        actual AS "Actual", forecast AS "Forecast", prior AS "Prior",
        createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar;
    """;

    public const string GetEconomicCalendarProjectionKeysV2All = """
    SELECT countryCode AS "CountryCode", monthBucket AS "MonthBucket", eventDate AS "EventDate", eventName AS "EventName",
        actual AS "Actual", forecast AS "Forecast", prior AS "Prior",
        createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar_by_country_month_v2;
    """;

    public const string GetReferenceProjectionStateV3 = """
    SELECT generation AS "Generation", completed AS "Completed"
    FROM reference_projection_state_v3
    WHERE projectionName = :projectionName;
    """;

    public const string GetReferenceProjectionStateNamesV3All = """
    SELECT projectionName AS "ProjectionName"
    FROM reference_projection_state_v3;
    """;

    public const string GetReferenceProjectionMutationsV3 = """
    SELECT mutationId AS "MutationId"
    FROM reference_projection_mutation_v3
    WHERE projectionName = :projectionName;
    """;

    public const string GetReferenceProjectionMutationsV3All = """
    SELECT projectionName AS "ProjectionName", mutationId AS "MutationId", startedOn AS "StartedOn"
    FROM reference_projection_mutation_v3;
    """;

    public const string InvalidateReferenceProjectionStateV3 = """
    UPDATE reference_projection_state_v3
    SET generation = :generation, completed = false, completedOn = null
    WHERE projectionName = :projectionName;
    """;

    public const string CompleteReferenceProjectionStateV3 = """
    UPDATE reference_projection_state_v3
    SET completed = true, completedOn = :completedOn
    WHERE projectionName = :projectionName
    IF generation = :generation;
    """;

    public const string InsertReferenceProjectionMutationV3 = """
    INSERT INTO reference_projection_mutation_v3 (projectionName, mutationId, startedOn)
    VALUES (:projectionName, :mutationId, :startedOn);
    """;

    public const string ClaimReferenceProjectionOwnershipV3 = """
    INSERT INTO reference_projection_ownership_v3 (
        projectionName, ownerMutationId, conflicted, claimedOn)
    VALUES (:projectionName, :mutationId, false, :claimedOn)
    IF NOT EXISTS;
    """;

    public const string FlagReferenceProjectionOwnershipConflictV3 = """
    UPDATE reference_projection_ownership_v3
    SET conflicted = true
    WHERE projectionName = :projectionName
    IF EXISTS;
    """;

    public const string ReleaseReferenceProjectionOwnershipIfSafeV3 = """
    DELETE FROM reference_projection_ownership_v3
    WHERE projectionName = :projectionName
    IF ownerMutationId = :mutationId AND conflicted = false;
    """;

    public const string ReleaseReferenceProjectionOwnershipV3 = """
    DELETE FROM reference_projection_ownership_v3
    WHERE projectionName = :projectionName
    IF ownerMutationId = :mutationId;
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

    public const string GetNextSeedIdV2 = """
    SELECT NextSeedId AS "Value"
    FROM seed_id_v2
    WHERE SeedType = :seedType;
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
    FROM scheduled_job_by_name_v3
    WHERE JobName = :jobName;
    """;

    public const string GetScheduledJobReservationV3 = """
    SELECT jobId, reservationToken
    FROM scheduled_job_by_name_v3
    WHERE jobName = :jobName;
    """;

    public const string GetScheduledJobs = """
    SELECT jobId AS "JobId", jobName AS "JobName", jobSchedule AS "JobSchedule", jobScheduleDate AS "JobScheduleDate", jobScheduleInterval AS "JobScheduleInterval", taskName AS "TaskName", taskEnabled AS "TaskEnabled", createdOn AS "CreatedOn", createdBy AS "CreatedBy", updatedOn AS "UpdatedOn", updatedBy AS "UpdatedBy"
    FROM scheduled_job;
    """;

    public const string GetScheduledJobsByNameV3All = """
    SELECT jobName AS "JobName", jobId AS "JobId", reservationToken AS "ReservationToken"
    FROM scheduled_job_by_name_v3;
    """;

    public const string GetScheduledJobWriteOwnershipV3 = """
    SELECT scopeType, scopeKey, operationId, startedOn
    FROM scheduled_job_write_ownership_v3
    WHERE scopeType = :scopeType
    AND scopeKey = :scopeKey;
    """;

    public const string GetScheduledJobWriteOwnershipsV3All = """
    SELECT scopeType, scopeKey, operationId, startedOn
    FROM scheduled_job_write_ownership_v3;
    """;

    public const string InsertEconomicCalendar = """
    INSERT INTO economic_calendar (eventDate, countryCode, eventName, actual, forecast, prior, createdOn, createdBy)
    VALUES (:eventDate, :countryCode, :eventName, :actual, :forecast, :prior, :createdOn, :createdBy);
    """;

    public const string InsertEconomicCalendarByCountryMonthV2 = """
    INSERT INTO economic_calendar_by_country_month_v2 (countryCode, monthBucket, eventDate, eventName, actual, forecast, prior, createdOn, createdBy)
    VALUES (:countryCode, :monthBucket, :eventDate, :eventName, :actual, :forecast, :prior, :createdOn, :createdBy);
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

    public const string InsertScheduledJobByNameV3 = """
    INSERT INTO scheduled_job_by_name_v3 (jobName, jobId, reservationToken)
    VALUES (:jobName, :jobId, :reservationToken)
    IF NOT EXISTS;
    """;

    public const string ClaimScheduledJobWriteOwnershipV3 = """
    INSERT INTO scheduled_job_write_ownership_v3 (
        scopeType, scopeKey, operationId, startedOn)
    VALUES (:scopeType, :scopeKey, :operationId, :startedOn)
    IF NOT EXISTS;
    """;

    public const string RotateScheduledJobNameV3Reservation = """
    UPDATE scheduled_job_by_name_v3
    SET reservationToken = :reservationToken
    WHERE jobName = :jobName
    IF jobId = :jobId
    AND reservationToken = :expectedReservationToken;
    """;

    public const string InsertSeedIdV2IfNotExists = """
    INSERT INTO seed_id_v2 (SeedType, NextSeedId)
    VALUES (:seedType, :nextSeedId)
    IF NOT EXISTS;
    """;

    public const string InsertScheduledJobDays = """
    INSERT INTO scheduled_job_days (jobId, monday, tuesday, wednesday, thursday, friday, saturday, sunday)
    VALUES (:jobId, :monday, :tuesday, :wednesday, :thursday, :friday, :saturday, :sunday);
    """;

    public const string UpdateNextSeedIdV2 = """
    UPDATE seed_id_v2
    SET NextSeedId = :nextSeedId
    WHERE SeedType = :seedType
    IF NextSeedId = :expectedSeedId;
    """;
}
