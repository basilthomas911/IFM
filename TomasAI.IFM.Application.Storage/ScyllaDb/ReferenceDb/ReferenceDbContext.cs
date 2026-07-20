using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;

/// <summary>
/// reference database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
public class ReferenceDbContext(
    IDbConnectionSettings connectionSettings, 
    IDbContextFactory dbFactory, 
    ILogger<DbProvider> logger) 
    : ObjectDataRepository<ReferenceDbContext>(connectionSettings["ReferenceDbConnection"], logger), IReferenceDbContext
{
    readonly static SemaphoreSlim _semaphoreSlim = new(1, 1);
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    public const string ReferenceDbConnection = "ReferenceDbConnection";

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override ReferenceDbContext Database => this;

    static long MapToNextSeedId(IObjectDataRecord e)
       => e.GetLong(0);

    static LookupTypeReadModel MapToLookupType(IObjectDataRecord e)
        => new(
            lookupTypeName: e.GetString(0),
            shortCode: e.GetString(1),
            orderId: e.GetInt(2),
            description: e.GetString(3),
            createdOn: e.GetDateTime(4),
            createdBy: e.GetString(5)
        );

    static LookupTypeNameReadModel MapToLookupTypeName(IObjectDataRecord e)
        => new(
            lookupTypeName: e.GetString(0)
        );

    static EconomicCalendarReadModel MapToEconomicCalendar(IObjectDataRecord e)
        => new(
            eventDate: e.GetDateTime(0),
            countryCode: e.GetString(1),
            eventName: e.GetString(2),
            actual: e.GetString(3),
            forecast: e.GetString(4),
            prior: e.GetString(5),
            createdOn: e.GetDateTime(6),
            createdBy: e.GetString(7)
        );

    static EconomicCalendarCountryCodeReadModel MapToEconomicCalendarCountryCode(IObjectDataRecord e)
        => new(
            countryCode: e.GetString(0)
        );

    static ScheduledJobReadModel MapToScheduledJob<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            jobId: e.GetInt(0),
            jobName: e.GetString(1),
            jobSchedule: e.GetEnum<JobScheduleType>(2),
            jobScheduleDate: e.GetDateTime(3),
            jobScheduleInterval: e.GetDouble(4),
            taskName: e.GetString(5),
            taskEnabled: e.GetBool(6),
            createdOn: e.GetDateTime(7),
            createdBy: e.GetString(8),
            updatedOn: e.GetDateTime(9),
            updatedBy: e.GetString(10)
        );

    static ScheduledJobDaysOfWeekReadModel MapToScheduledJobDaysOfWeek<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            jobId: e.GetInt(0),
            monday: e.GetBool(1),
            tuesday: e.GetBool(2),
            wednesday: e.GetBool(3),
            thursday: e.GetBool(4),
            friday: e.GetBool(5),
            saturday: e.GetBool(6),
            sunday: e.GetBool(7)
        );

    static MDIForwardLossRatioReadModel MapToMDIForwardLossRatio<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            trendDirection: e.GetEnum<IntrinsicTimeTrendType>(0),
            tradeType: e.GetEnum<TradeType>(1),
            mdi: e.GetInt(2),
            forwardLossRatio: e.GetDouble(3),
            createdBy: e.GetString(4),
            createdOn: e.GetDateTime(5),
            updatedBy: e.GetString(6),
            updatedOn: e.GetDateTime(7)
        );

    static LookupTypeShortCodeReadModel MapToLookupTypeShortCode<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            shortCode: e.GetString(0),
            orderId: e.GetInt(1)
        );

    static int MapToJobId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    /// <summary>
    /// return db reader/writer properties
    /// </summary>
    public IReferenceDbReadContext DbReader => this;
    public IReferenceDbWriteContext DbWriter => this;

    /// <summary>
    /// delete lookup type by name
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <returns></returns>
    public async Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId)
    {
        await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.DeleteLookupType)
               .SetParameters(new DeleteLookupType(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
               .ExecuteCommandAsync();

        var lookupTypes = await GetLookupTypeAsync(lookupTypeId.LookupTypeName);
        if (lookupTypes?.Count  > 0)
        {
            var orderId = 0;
            foreach (var e in lookupTypes)
                await UpdateLookupTypeAsync(e.Id, e with { OrderId = orderId++, CreatedOn = DateTime.Now });
        }
    }

    /// <summary>
    /// delete scheduled job
    /// </summary>
    /// <param name="scheduledJobId"></param>
    /// <returns></returns>
    public async Task DeleteScheduledJobAsync(int scheduledJobId)
         => await _dbFactory.ReferenceDb
                .Use(ReferenceDbCql.DeleteScheduledJob)
                .SetParameters(new DeleteScheduledJob(scheduledJobId))
                .ExecuteCommandAsync();

     /// <summary>
    /// delete economic calendar
    /// </summary>
    /// <param name="id">economic calendar id</param>
    /// <returns></returns>
    public async Task DeleteEconomicCalendarAsync(EconomicCalendarId id)
        => await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.DeleteEconomicCalendar)
               .SetParameters(new DeleteEconomicCalendar(id.EventDate, id.CountryCode, id.EventName))
               .ExecuteCommandAsync();

    /// <summary>
    /// return next seed id by seed type
    /// </summary>
    /// <param name="seedType"></param>
    /// <returns></returns>
    public async Task<int> GetNextSeedIdAsync(string seedType)
    {
        var nextSeedId = 0;
        await _semaphoreSlim.WaitAsync();
        try
        {
            await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.UpdateNextSeedId)
               .SetParameters(new UpdateNextSeedId(seedType))
               .ExecuteCommandAsync();

             var value = await _dbFactory.ReferenceDb
                    .Use(ReferenceDbCql.GetNextSeedId)
                    .SetParameters(new GetNextSeedId(seedType))
                    .ExecuteScalarAsync(MapToNextSeedId!);
            nextSeedId = Convert.ToInt32(value);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
        return nextSeedId;
    }

    /// <summary>
    /// return current seed id for selected seed type
    /// </summary>
    /// <param name="seedType"></param>
    /// <returns></returns>
    public async Task<int> GetCurrentSeedIdAsync(string seedType)
       => Convert.ToInt32(await _dbFactory.ReferenceDb
                .Use(ReferenceDbCql.GetNextSeedId)
                .SetParameters(new GetNextSeedId(seedType))
                .ExecuteScalarAsync(MapToNextSeedId!) );

    /// <summary>
    /// return list of economic calendar events for selected event date
    /// </summary>
    /// <param name="eventDate"></param>
    /// <returns></returns>
    public async Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id)
        => await _dbFactory.ReferencePool.GetAsync( async db =>
            await db.Use(ReferenceDbCql.GetEconomicCalendarById)
                .SetParameters(new GetEconomicCalendarById(id.EventDate, id.CountryCode, id.EventName))
                .ExecuteSingleAsync(MapToEconomicCalendar!));
    /*
   => await _dbFactory.ReferenceDb
          .Use(ReferenceDbCql.GetEconomicCalendarById)
          .SetParameters(new
          {
              eventDate = id.EventDate,
              countryCode = id.CountryCode,
              eventName = id.EventName
          })
          .ExecuteSingleAsync<EconomicCalendarReadModel>(MapToEconomicCalendar!);
    */

    /// <summary>
    /// return list of economic calendar events for selected event date
    /// </summary>
    /// <param name="eventDate"></param>
    /// <returns></returns>
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode)
        => [.. (await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.GetEconomicCalendars)
               .SetParameters(new GetEconomicCalendars(eventDate.Date, eventDate.Date.AddDays(1), countryCode))
               .ExecuteQueryAsync(MapToEconomicCalendar!))
                .OrderByDescending(e => e.EventDate)];
            
    /// <summary>
    /// return list of all economic calendar events 
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync()
        => [.. (await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.GetEconomicCalendarsAll)
               .ExecuteQueryAsync(MapToEconomicCalendar!)).OrderByDescending(e => e.EventDate)];

    /// <summary>
    /// return list of economic calendar events for selected date range
    /// </summary>
    /// <param name="eventDate"></param>
    /// <returns></returns>
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode)
        => await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.GetEconomicCalendars)
               .SetParameters(new GetEconomicCalendars(startDate, endDate, countryCode))
               .ExecuteQueryAsync(MapToEconomicCalendar!);

    /// <summary>
    /// return list of economic calendar country codes
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync()
    {
        var countryCodes = await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.GetEconomicCalendarCountryCodes)
               .ExecuteQueryAsync(MapToEconomicCalendarCountryCode);
        if (countryCodes is null || countryCodes.Count == 0)
            return [];
        return countryCodes.DistinctBy(e => e.CountryCode).ToImmutableList();
    }

    /// <summary>
    /// return lookup type from lookup type id
    /// </summary>
    /// <param name="lookupTypeId"
    /// <returns></returns>
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId)
       => await _dbFactory.ReferenceDb
                .Use(ReferenceDbCql.GetLookupTypeById)
                .SetParameters(new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
                .ExecuteSingleAsync(MapToLookupType!);

    /// <summary>
    /// return lookup types from lookup type name
    /// </summary>
    /// <param name="lookupTypeName"></param>
    /// <returns></returns>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName)
       => await _dbFactory.ReferenceDb
                .Use(ReferenceDbCql.GetLookupType)
                .SetParameters(new GetLookupType(lookupTypeName))
                .ExecuteQueryAsync(MapToLookupType!);

    /// <summary>
    /// return all lookup types 
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync()
       => await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.GetLookupTypes)
               .ExecuteQueryAsync(MapToLookupType!);

    /// <summary>
    /// return all lookup type names
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<string>> GetLookupTypeNamesAsync()
       => [.. (await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.GetLookupTypeNames)
               .ExecuteQueryAsync(MapToLookupTypeName!)).Select(e => e.LookupTypeName)];

    /// <summary>
    /// return all lookup type short codes by lookup type
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName)
       => await _dbFactory.ReferenceDb
                .Use(ReferenceDbCql.GetLookupTypeShortCodes)
                .SetParameters(new GetLookupType(lookupTypeName))
                .ExecuteQueryAsync(MapToLookupTypeShortCode!);

    /// <summary>
    /// return scheduled job id
    /// </summary>
    /// <param name="scheduledJobName"></param>
    /// <returns></returns>
    public async Task<int> GetScheduledJobIdAsync(string scheduledJobName)
       => await _dbFactory.ReferenceDb
            .Use(ReferenceDbCql.GetScheduledJobId)
            .SetParameters(new GetScheduledJobId(scheduledJobName))
            .ExecuteScalarAsync( MapToJobId!);

    /// <summary>
    /// return list of scheduled jobs
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync()
    {
        var db =  _dbFactory.ReferenceDb;
        var scheduledJobs = await db.Use(ReferenceDbCql.GetScheduledJobs)
               .ExecuteQueryAsync(MapToScheduledJob!);
        foreach (var e in scheduledJobs)
        {
            var jobDaysOfWeek = await GetScheduledJobDaysAsync(e.JobId);
            if (jobDaysOfWeek is not null)
                e.DaysOfWeek = jobDaysOfWeek;
        }
        return scheduledJobs;

         Task<ScheduledJobDaysOfWeekReadModel?> GetScheduledJobDaysAsync(int jobId)
            =>  db.Use(ReferenceDbCql.GetScheduledJobDays)
                .SetParameters(new GetScheduledJobDays(jobId))
                .ExecuteSingleAsync(MapToScheduledJobDaysOfWeek!);
    }

    /// <summary>
    /// return mdi forward loss ratio data
    /// </summary>
    /// <param name="trendDirection"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        => await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.GetMDIForwardLossRatios)
               .SetParameters(new GetMDIForwardLossRatios(trendDirection.ToStringFast(), tradeType.ToStringFast()))
               .ExecuteQueryAsync(MapToMDIForwardLossRatio!);

    /// <summary>
    /// insert lookup types
    /// </summary>
    /// <param name="lookupTypes"></param>
    /// <returns></returns>
    public async Task InsertLookupTypeAsync(LookupTypeReadModel e)
        => await _dbFactory.ReferenceDb
               .Use(ReferenceDbCql.InsertLookupType)
               .SetParameters(new InsertLookupType(e.LookupTypeName, e.ShortCode, e.OrderId, e.Description, e.CreatedOn, e.CreatedBy))
               .ExecuteCommandAsync();

     /// <summary>
    /// insert economic calendar
    /// </summary>
    /// <param name="e"></param>
    public async Task InsertEconomicCalendarAsync(EconomicCalendarReadModel e)
        =>  await _dbFactory.ReferencePool.ExecuteAsync(async db => 
                await db.Use(ReferenceDbCql.InsertEconomicCalendar)
                   .SetParameters(new InsertEconomicCalendar(e.EventDate, e.CountryCode, e.EventName, e.Actual, e.Forecast, e.Prior, e.CreatedOn, e.CreatedBy))
                   .ExecuteCommandAsync()
        );
        /*
       => await _dbFactory.ReferenceDb
              .Use(ReferenceDbCql.InsertEconomicCalendar)
              .SetParameters(new
              {
                  eventDate = e.EventDate,
                  countryCode = e.CountryCode,
                  eventName = e.EventName,
                  actual = e.Actual,
                  forecast = e.Forecast,
                  prior = e.Prior,
                  createdOn = e.CreatedOn,
                  createdBy = e.CreatedBy
              })
              .ExecuteCommandAsync().ConfigureAwait(false);
        */

    /// <summary>
    /// insert collection of economic calendars into storage
    /// </summary>
    /// <param name="economicCalendars"></param>
    /// <returns></returns>
    public async Task InsertEconomicCalendarsAsync(ICollection<EconomicCalendarReadModel> economicCalendars)
        => await _dbFactory.ReferenceDb.Use(ReferenceDbCql.InsertEconomicCalendar)
            .SetParameters(economicCalendars.Select(o => new InsertEconomicCalendar(o.EventDate, o.CountryCode, o.EventName, o.Actual, o.Forecast, o.Prior, o.CreatedOn, o.CreatedBy)))
            .ExecuteCommandAsync();

    /// <summary>
    /// insert scheduled job
    /// </summary>
    /// <param name="e">scheduled job</param>
    /// <returns></returns>
    public async Task InsertScheduledJobAsync(ScheduledJobReadModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;
        var jobId = await ((IReferenceDbReadContext)db).GetNextSeedIdAsync("ScheduledJobId");
        queuedCommands.Add(
        db.Use(ReferenceDbCql.InsertScheduledJob)
              .SetParameters(new InsertScheduledJob(jobId, e.JobName, e.JobSchedule.ToStringFast(), e.JobScheduleDate, e.JobScheduleInterval, e.TaskName, e.TaskEnabled, e.CreatedOn, e.CreatedBy))
              .QueueCommand());

        if (e.DaysOfWeek is not null)
        {
            queuedCommands.Add(
            db.Use(ReferenceDbCql.InsertScheduledJobDays)
                  .SetParameters(new InsertScheduledJobDays(jobId, e.DaysOfWeek.Monday, e.DaysOfWeek.Tuesday, e.DaysOfWeek.Wednesday, e.DaysOfWeek.Thursday, e.DaysOfWeek.Friday, e.DaysOfWeek.Saturday, e.DaysOfWeek.Sunday))
                .QueueCommand());
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert scheduled job
    /// </summary>
    /// <param name="e">scheduled job</param>
    /// <returns></returns>
    public async Task UpdateScheduledJobAsync(ScheduledJobReadModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        queuedCommands.Add(
        db.Use(ReferenceDbCql.DeleteScheduledJob)
                .SetParameters(new DeleteScheduledJob(e.JobId))
                .QueueCommand());

        queuedCommands.Add(
        db.Use(ReferenceDbCql.InsertScheduledJob)
               .SetParameters(new InsertScheduledJob(e.JobId, e.JobName, e.JobSchedule.ToStringFast(), e.JobScheduleDate, e.JobScheduleInterval, e.TaskName, e.TaskEnabled, e.CreatedOn, e.CreatedBy))
               .QueueCommand());

        if (e.DaysOfWeek != null)
        {
            queuedCommands.Add(
            db.Use(ReferenceDbCql.InsertScheduledJobDays)
                  .SetParameters(new InsertScheduledJobDays(e.JobId, e.DaysOfWeek.Monday, e.DaysOfWeek.Tuesday, e.DaysOfWeek.Wednesday, e.DaysOfWeek.Thursday, e.DaysOfWeek.Friday, e.DaysOfWeek.Saturday, e.DaysOfWeek.Sunday))
                   .QueueCommand());
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }
    
    /// <summary>
    /// update economic calendar
    /// </summary>
    /// <param name="id"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        queuedCommands.Add(
        db.Use(ReferenceDbCql.DeleteEconomicCalendar)
               .SetParameters(new DeleteEconomicCalendar(id.EventDate, id.CountryCode, id.EventName))
               .QueueCommand());

        queuedCommands.Add(
        db.Use(ReferenceDbCql.InsertEconomicCalendar)
               .SetParameters(new InsertEconomicCalendar(e.EventDate, e.CountryCode, e.EventName, e.Actual, e.Forecast, e.Prior, e.CreatedOn, e.CreatedBy))
               .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// update lookup type
    /// </summary>
    /// <param name="id"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        queuedCommands.Add(
        db.Use(ReferenceDbCql.DeleteLookupType)
               .SetParameters(new DeleteLookupType(id.LookupTypeName, id.OrderId))
               .QueueCommand());

        queuedCommands.Add(
        db.Use(ReferenceDbCql.InsertLookupType)
               .SetParameters(new InsertLookupType(e.LookupTypeName, e.ShortCode, e.OrderId, e.Description, e.CreatedOn, e.CreatedBy))
               .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

     /// <summary>
    /// insert mdi forward loss ratio
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel e)
        => await _dbFactory.ReferenceDb
              .Use(ReferenceDbCql.InsertMDIForwardLossRatio)
              .SetParameters(new InsertMDIForwardLossRatio(e.MDI, e.TrendDirection.ToStringFast(), e.TradeType.ToStringFast(), e.ForwardLossRatio, e.CreatedBy, e.CreatedOn, e.UpdatedBy, e.UpdatedOn))
              .ExecuteCommandAsync().ConfigureAwait(false);

    /// <summary>
    /// insert mdi forward loss ratios
    /// </summary>
    /// <param name="mdiForwardLossRatios"></param>
    /// <returns></returns>
    public async Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
        => await _dbFactory.ReferenceDb
            .Use(ReferenceDbCql.InsertMDIForwardLossRatio)
            .SetParameters(mdiForwardLossRatios.Select(o => new InsertMDIForwardLossRatio(o.MDI, o.TrendDirection.ToStringFast(), o.TradeType.ToStringFast(), o.ForwardLossRatio, o.CreatedBy, o.CreatedOn, o.UpdatedBy, o.UpdatedOn)))
            .ExecuteCommandAsync();

    /// <summary>
    /// delete mdi forward loss ratio
    /// </summary>
    /// <param name="trendDirection"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        =>  await _dbFactory.ReferenceDb
                .Use(ReferenceDbCql.DeleteMDIForwardLossRatio)
                .SetParameters(new DeleteMDIForwardLossRatio(trendDirection.ToStringFast(), tradeType.ToStringFast()))
                .ExecuteCommandAsync();

    /// <summary>
    /// update mdi forward loss ratio
    /// </summary>
    /// <param name="mdiForwardLossRatio"></param>
    /// <returns></returns>
    public async Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        // Delete the existing record
        queuedCommands.Add(
            db.Use(ReferenceDbCql.DeleteMDIForwardLossRatio)
                .SetParameters(new DeleteMDIForwardLossRatio(mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast()))
                .QueueCommand());

        // Insert the updated record
        queuedCommands.Add(
            db.Use(ReferenceDbCql.InsertMDIForwardLossRatio)
                .SetParameters(new InsertMDIForwardLossRatio(mdiForwardLossRatio.MDI, mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast(), mdiForwardLossRatio.ForwardLossRatio, mdiForwardLossRatio.CreatedBy, mdiForwardLossRatio.CreatedOn, mdiForwardLossRatio.UpdatedBy, mdiForwardLossRatio.UpdatedOn))
                .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

 }

