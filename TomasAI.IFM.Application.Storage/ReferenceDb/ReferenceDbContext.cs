using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.TaskScheduler;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.ReferenceDb
{
    /// <summary>
    /// reference database
    /// </summary>
    public class ReferenceDbContext : ObjectDataRepository<ReferenceDbContext>, IReferenceDbContext, IReferenceDbReadContext, IReferenceDbWriteContext
    {
        public const string ReferenceDbConnection = "ReferenceDbConnection";
        readonly IDbContextFactory _dbFactory;

        /// <summary>
        /// reference database constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        /// <param name="dbFactory"></param>
        public ReferenceDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<ReferenceDbContext> logger)
            :base(connectionSettings["ReferenceDbConnection"], logger)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// initialize reference view model mappings
        /// </summary>
        /// <param name="model"></param>
        public override void OnCreateModel(DbModel<ReferenceDbContext> model)
        {
            LookupType = model.Map(e => e.LookupType)
                .Parameters(e =>
                    e.Set(o => o.LookupTypeName)
                     .Set(o => o.ShortCode)
                     .Set(o => o.OrderId)
                     .Set(o => o.Description)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                 );

            LookupTypeShortCode = model.Map(e => e.LookupTypeShortCode)
                .Parameters(e =>
                    e.Set(o => o.ShortCode)
                     .Set(o => o.OrderId)
                 );

            ScheduledJob = model.Map(e => e.ScheduledJob)
                .Parameters(e =>
                    e.Set(o => o.JobId)
                     .Set(o => o.JobName)
                     .Set(o => o.JobSchedule, o => o.AsEnum<JobScheduleType>())
                     .Set(o => o.JobScheduleDate)
                     .Set(o => o.JobScheduleInterval)
                     .Set(o => o.TaskName)
                     .Set(o => o.TaskEnabled)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.UpdatedOn)
                     .Set(o => o.UpdatedBy)
                 );

            ScheduledJobDaysOfWeek = model.Map(e => e.ScheduledJobDaysOfWeek)
                .Parameters(e =>
                    e.Set(o => o.JobId)
                     .Set(o => o.Monday)
                     .Set(o => o.Tuesday)
                     .Set(o => o.Wednesday)
                     .Set(o => o.Thursday)
                     .Set(o => o.Friday)
                     .Set(o => o.Saturday)
                     .Set(o => o.Sunday)
                 );

            StrikePriceVolatility = model.Map(e => e.StrikePriceVolatility)
                .Parameters(e =>
                    e.Set(o => o.Symbol)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.MarketTrend, o => o.AsEnum<MarketDirectionType>())
                     .Set(o => o.MarketVolatility, o => o.AsEnum<MarketVolatilityType>())
                     .Set(o => o.MarketVolatilityTrend, o => o.AsEnum<PriceDirectionType>())
                     .Set(o => o.Delta)
                     .Set(o => o.StrikePriceOffset)
                );

            EconomicCalendar = model.Map(e => e.EconomicCalendar)
                .Parameters(e =>
                    e.Set(o => o.EventDate)
                     .Set(o => o.CountryCode)
                     .Set(o => o.EventName)
                     .Set(o => o.Actual)
                     .Set(o => o.Forecast)
                     .Set(o => o.Prior)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                 );

            CountryCodes = model.Map(e => e.CountryCodes)
                .Parameters(e =>
                    e.Set(o => o.CountryCode)
                 );

            MdiForwardLossRatio = model.Map(e => e.MdiForwardLossRatio)
               .Parameters(e =>
                   e.Set(o => o.MDI)
                     .Set(o => o.TrendDirection, o => o.AsEnum<IntrinsicTimeTrendType>())
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.ForwardLossRatio)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.UpdatedBy)
                     .Set(o => o.UpdatedOn)
                );

        }

        public DbMap<LookupTypeReadModel> LookupType { get; private set; }
        public DbMap<ScheduledJobReadModel> ScheduledJob { get; private set; }
        public DbMap<ScheduledJobDaysOfWeekReadModel> ScheduledJobDaysOfWeek { get; private set; }
        public DbMap<StrikePriceVolatilityReadModel> StrikePriceVolatility { get; private set; }
        public DbMap<EconomicCalendarReadModel> EconomicCalendar { get; private set; }
        public DbMap<EconomicCalendarCountryCodeReadModel> CountryCodes { get; private set; }
        public DbMap<LookupTypeShortCodeReadModel> LookupTypeShortCode { get; private set; }
        public DbMap<MDIForwardLossRatioReadModel> MdiForwardLossRatio { get; private set; }

        /// <summary>
        /// return db reader/writer properties
        /// </summary>
        public IReferenceDbReadContext DbReader => this;
        public IReferenceDbWriteContext DbWriter => this;

        public enum StoredProcedure
        {
            spBackupDatabase,
            spDeleteLookupType,
            spDeleteScheduledJob,
            spDeleteStrikePriceVolatility,
            spDeleteEconomicCalendar,
            spGetNextSeedId,
            spGetCurrentSeedId,
            spGetEconomicCalendar,
            spGetEconomicCalendarById,
            spGetEconomicCalendarCountryCodes,
            spGetEconomicCalendars,
            spGetEconomicCalendarsAll,
            spGetLookupType,
            spGetLookupTypeById,
            spGetLookupTypes,
            spGetLookupTypeNames,
            spGetLookupTypeShortCodes,
            spGetMDIForwardLossRatios,
            spGetScheduledJobId,
            spGetScheduledJobs,
            spGetScheduledJobDays,
            spGetStrikePriceVolatility,
            spInsertLookupType,
            spInsertScheduledJob,
            spInsertScheduledJobDays,
            spInsertStrikePriceVolatility,
            spInsertEconomicCalendar
        }

        /// <summary>
        /// delete lookup type by name
        /// </summary>
        /// <param name="lookupTypeId"></param>
        /// <returns></returns>
        public async Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId)
        {
            await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spDeleteLookupType)
                   .SetParameters(new { 
                       lookupTypeName = lookupTypeId.LookupTypeName, 
                       orderId = lookupTypeId.OrderId })
                   .ExecuteCommandAsync();

            var lookupTypes = await GetLookupTypeAsync(lookupTypeId.LookupTypeName);
            if ((lookupTypes?.Count ?? 0) > 0)
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
                    .Use(StoredProcedure.spDeleteScheduledJob)
                    .SetParameters(new { jobId = scheduledJobId })
                    .ExecuteCommandAsync();

        /// <summary>
        /// delete scheduled job
        /// </summary>
        /// <param name="scheduledJobId"></param>
        /// <returns></returns>
        public async Task DeleteStrikePriceVolatilityAsync(StrikePriceVolatilityId id)
             => await _dbFactory.ReferenceDb
                    .Use(StoredProcedure.spDeleteStrikePriceVolatility)
                    .SetParameters(new { 
                        symbol = id.Symbol,
                        tradeType = $"{id.TradeType}",
                        marketTrend = $"{id.MarketTrend}",
                        marketVolatility = $"{id.MarketVolatility}" })
                    .ExecuteCommandAsync();

        /// <summary>
        /// delete economic calendar
        /// </summary>
        /// <param name="id">economic calendar id</param>
        /// <returns></returns>
        public async Task DeleteEconomicCalendarAsync(EconomicCalendarId id)
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spDeleteEconomicCalendar)
                   .SetParameters(new {
                       eventDate = id.EventDate,
                       countryCode = id.CountryCode,
                       eventName = id.EventName })
                   .ExecuteCommandAsync();

        /// <summary>
        /// return next seed id by seed type
        /// </summary>
        /// <param name="seedType"></param>
        /// <returns></returns>
        public async Task<int> GetNextSeedIdAsync(string seedType)
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetNextSeedId)
                   .SetParameters(new { seedType })
                   .ExecuteScalarAsync<int>();

        /// <summary>
        /// return current seed id for selected seed type
        /// </summary>
        /// <param name="seedType"></param>
        /// <returns></returns>
        public async Task<int> GetCurrentSeedIdAsync(string seedType)
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetCurrentSeedId)
                   .SetParameters(new { seedType })
                   .ExecuteScalarAsync<int>();

        /// <summary>
        /// return list of economic calendar events for selected event date
        /// </summary>
        /// <param name="eventDate"></param>
        /// <returns></returns>
        public async Task<EconomicCalendarReadModel> GetEconomicCalendarAsync(EconomicCalendarId id )
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetEconomicCalendarById)
                   .SetParameters(new { 
                       eventDate = id.EventDate, 
                       countryCode = id.CountryCode, 
                       eventName = id.EventName })
                   .ExecuteSingleAsync<EconomicCalendarReadModel>();

        /// <summary>
        /// return list of economic calendar events for selected event date
        /// </summary>
        /// <param name="eventDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<EconomicCalendarReadModel>> GetEconomicCalendarAsync(DateTime eventDate, string countryCode)
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetEconomicCalendar)
                   .SetParameters(new { 
                       startDate = eventDate.Date, 
                       endDate = eventDate.Date.AddDays(1).AddMilliseconds(-1),
                       countryCode
                   })
                   .ExecuteQueryAsync<EconomicCalendarReadModel>();

        /// <summary>
        /// return list of all economic calendar events 
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync()
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetEconomicCalendarsAll)
                   .ExecuteQueryAsync<EconomicCalendarReadModel>();

        /// <summary>
        /// return list of economic calendar events for selected date range
        /// </summary>
        /// <param name="eventDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode)
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetEconomicCalendar)
                   .SetParameters(new { 
                       startDate , 
                       endDate,
                       countryCode
                   })
                   .ExecuteQueryAsync<EconomicCalendarReadModel>();

        /// <summary>
        /// return list of economic calendar country codes
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync()
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetEconomicCalendarCountryCodes)
                   .ExecuteQueryAsync<EconomicCalendarCountryCodeReadModel>();

        /// <summary>
        /// return lookup type from lookup type id
        /// </summary>
        /// <param name="lookupTypeId"
        /// <returns></returns>
        public async Task<LookupTypeReadModel> GetLookupTypeAsync(LookupTypeId lookupTypeId)
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetLookupTypeById)
                   .SetParameters(new { 
                       lookupTypeName = lookupTypeId.LookupTypeName, 
                       orderId = lookupTypeId.OrderId })
                   .ExecuteSingleAsync<LookupTypeReadModel>();

        /// <summary>
        /// return lookup types from lookup type name
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName)
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetLookupType)
                   .SetParameters(new { lookupTypeName })
                   .ExecuteQueryAsync<LookupTypeReadModel>();
    
        /// <summary>
        /// return all lookup types 
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<LookupTypeReadModel>> GetLookupTypesAsync()
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetLookupTypes)
                   .ExecuteQueryAsync<LookupTypeReadModel>();

        /// <summary>
        /// return all lookup type names
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<string>> GetLookupTypeNamesAsync()
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetLookupTypeNames)
                   .ExecuteQueryAsync<string>(e => e.Get("LookupTypeName").As<string>());

        /// <summary>
        /// return all lookup type short codes by lookup type
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName)
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetLookupTypeShortCodes)
                   .SetParameters(new { lookupTypeName })
                   .ExecuteQueryAsync<LookupTypeShortCodeReadModel>();

        /// <summary>
        /// return scheduled job id
        /// </summary>
        /// <param name="scheduledJobName"></param>
        /// <returns></returns>
        public async Task<int> GetScheduledJobIdAsync(string scheduledJobName)
           => await _dbFactory.ReferenceDb
                .Use(StoredProcedure.spGetScheduledJobId)
                .SetParameters(new { jobName = scheduledJobName })
                .ExecuteScalarAsync<int>();

        /// <summary>
        /// return list of scheduled jobs
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<ScheduledJobReadModel>> GetScheduledJobsAsync()
        {
            var scheduledJobs = await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetScheduledJobs)
                   .ExecuteQueryAsync<ScheduledJobReadModel>();
            foreach(var scheduledJob in scheduledJobs)
            {
                var jobDaysInWeek = await GetScheduledJobDaysAsync(scheduledJob.JobId);
                if (jobDaysInWeek != null)
                    scheduledJob.DaysOfWeek = jobDaysInWeek;
            }
            return scheduledJobs;

            async Task<ScheduledJobDaysOfWeekReadModel> GetScheduledJobDaysAsync(int jobId)
                => await _dbFactory.ReferenceDb
                    .Use(StoredProcedure.spGetScheduledJobDays)
                    .SetParameters(new { jobId })
                    .ExecuteSingleAsync<ScheduledJobDaysOfWeekReadModel>();
        }

        /// <summary>
        /// return strike price volatility
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<StrikePriceVolatilityReadModel>> GetStrikePriceVolatilityAsync(string symbol, TradeType tradeType)
           => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetStrikePriceVolatility)
                   .SetParameters(new { 
                       symbol, 
                       tradeType })
                   .ExecuteQueryAsync<StrikePriceVolatilityReadModel>();

        /// <summary>
        /// return mdi forward loss ratio data
        /// </summary>
        /// <param name="trendDirection"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spGetMDIForwardLossRatios)
                   .SetParameters(new {
                       trendDirection = $"{trendDirection}",
                       tradeType = $"{tradeType}" })
                   .ExecuteQueryAsync<MDIForwardLossRatioReadModel>();

        /// <summary>
        /// insert lookup types
        /// </summary>
        /// <param name="lookupTypes"></param>
        /// <returns></returns>
        public async Task InsertLookupTypeAsync(LookupTypeReadModel e)
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spInsertLookupType)
                   .SetParameters(new  {
                       lookupTypeName = e.LookupTypeName,
                       shortCode = e.ShortCode,
                       orderId = e.OrderId,
                       description = e.Description,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
                   .ExecuteCommandAsync();

        /// <summary>
        /// insert strike price volatility
        /// </summary>
        /// <param name="e">StrikePriceVolatilityReadModel</param>
        /// <returns></returns>
        public async Task InsertStrikePriceVolatilityAsync(StrikePriceVolatilityReadModel e)
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spInsertStrikePriceVolatility)
                   .SetParameters(new {
                       symbol = e.Symbol,
                       tradeType = $"{e.TradeType}",
                       marketTrend = $"{e.MarketTrend}",
                       marketVolatility = $"{e.MarketVolatility}",
                       marketVolatilityTrend = $"{e.MarketVolatilityTrend}",
                       delta = e.Delta,
                       strikePriceOffset = e.StrikePriceOffset  })
                   .ExecuteCommandAsync();

        /// <summary>
        /// insert economic calendar
        /// </summary>
        /// <param name="e"></param>
         public async Task InsertEconomicCalendarAsync(EconomicCalendarReadModel e)
            => await _dbFactory.ReferenceDb
                   .Use(StoredProcedure.spInsertEconomicCalendar)
                   .SetParameters(new {
                       eventDate = e.EventDate,
                       countryCode = e.CountryCode,
                       eventName = e.EventName,
                       actual = e.Actual,
                       forecast = e.Forecast,
                       prior = e.Prior,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
                   .ExecuteCommandAsync().ConfigureAwait(false);

        /// <summary>
        /// insert collection of economic calendars into storage
        /// </summary>
        /// <param name="economicCalendars"></param>
        /// <returns></returns>
        public async Task InsertEconomicCalendarsAsync(ICollection<EconomicCalendarReadModel> economicCalendars)
        {
            var queuedCommands = new List<object>();
            var db = _dbFactory.ReferenceDb;
            foreach (var e in economicCalendars)
                queuedCommands.Add(db.Use(StoredProcedure.spInsertEconomicCalendar)
                    .SetParameters(new {
                        eventDate = e.EventDate,
                        countryCode = e.CountryCode,
                        eventName = e.EventName,
                        actual = e.Actual,
                        forecast = e.Forecast,
                        prior = e.Prior,
                        createdOn = e.CreatedOn,
                        createdBy = e.CreatedBy })
                    .QueueCommand());
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

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
            db.Use(StoredProcedure.spInsertScheduledJob)
                  .SetParameters(new {
                      jobId,
                      jobName = e.JobName,
                      jobSchedule = $"{e.JobSchedule}",
                      jobScheduleDate = e.JobScheduleDate,
                      jobScheduleInterval = e.JobScheduleInterval,
                      taskName = e.TaskName,
                      taskEnabled = e.TaskEnabled,
                      createdOn = e.CreatedOn,
                      createdBy = e.CreatedBy })
                  .QueueCommand());
            if (e.DaysOfWeek != null)
            {
                queuedCommands.Add(
                    db.Use(StoredProcedure.spInsertScheduledJobDays)
                          .SetParameters(new
                          {
                              jobId,
                              monday = e.DaysOfWeek!.Monday,
                              tuesday = e.DaysOfWeek!.Tuesday,
                              wednesday = e.DaysOfWeek.Wednesday,
                              thursday = e.DaysOfWeek.Thursday,
                              friday = e.DaysOfWeek.Friday,
                              saturday = e.DaysOfWeek.Saturday,
                              sunday = e.DaysOfWeek.Sunday
                          })
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
            db.Use(StoredProcedure.spDeleteScheduledJob)
                    .SetParameters(new { jobId = e.JobId })
                    .QueueCommand());

            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertScheduledJob)
                   .SetParameters(new {
                       jobId = e.JobId,
                       jobName = e.JobName,
                       jobSchedule = $"{e.JobSchedule}",
                       jobScheduleDate = e.JobScheduleDate,
                       jobScheduleInterval = e.JobScheduleInterval,
                       taskName = e.TaskName,
                       taskEnabled = e.TaskEnabled,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
                   .QueueCommand());

            if (e.DaysOfWeek != null)
            {
                queuedCommands.Add(
            db.Use(StoredProcedure.spInsertScheduledJobDays)
                  .SetParameters(new
                  {
                      jobId = e.JobId,
                      monday = e.DaysOfWeek.Monday,
                      tuesday = e.DaysOfWeek.Tuesday,
                      wednesday = e.DaysOfWeek.Wednesday,
                      thursday = e.DaysOfWeek.Thursday,
                      friday = e.DaysOfWeek.Friday,
                      saturday = e.DaysOfWeek.Saturday,
                      sunday = e.DaysOfWeek.Sunday
                  })
                  .QueueCommand());
            }
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        /// <summary>
        /// insert scheduled job
        /// </summary>
        /// <param name="e">scheduled job</param>
        /// <returns></returns>
        public async Task UpdateStrikePriceVolatilityAsync(StrikePriceVolatilityId id, StrikePriceVolatilityReadModel e)
        {
            var queuedCommands = new List<object>();    
            var db = _dbFactory.ReferenceDb;

            queuedCommands.Add(
            db.Use(StoredProcedure.spDeleteStrikePriceVolatility)
                    .SetParameters(new {
                        symbol = id.Symbol,
                        tradeType = $"{id.TradeType}",
                        marketTrend = $"{id.MarketTrend}",
                        marketVolatility = $"{id.MarketVolatility}" })
                    .QueueCommand());

            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertStrikePriceVolatility)
                   .SetParameters(new {
                       symbol = e.Symbol,
                       tradeType = $"{e.TradeType}",
                       marketTrend = $"{e.MarketTrend}",
                       marketVolatility = $"{e.MarketVolatility}",
                       delta = e.Delta,
                       strikePriceOffset = e.StrikePriceOffset })
                  .QueueCommand());

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
            db.Use(StoredProcedure.spDeleteEconomicCalendar)
                   .SetParameters(new {
                       eventDate = id.EventDate,
                       countryCode = id.CountryCode,
                       eventName = id.EventName })
                   .QueueCommand());

            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertEconomicCalendar)
                   .SetParameters(new {
                       eventDate = e.EventDate,
                       countryCode = e.CountryCode,
                       eventName = e.EventName,
                       actual = e.Actual,
                       forecast = e.Forecast,
                       prior = e.Prior,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
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
            db.Use(StoredProcedure.spDeleteLookupType)
                   .SetParameters(new { 
                       lookupTypeName = e.LookupTypeName, 
                       shortCode = e.ShortCode })
                   .QueueCommand());

            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertLookupType)
                   .SetParameters(new {
                       lookupTypeName = e.LookupTypeName,
                       shortCode = e.ShortCode,
                       orderId = e.OrderId,
                       description = e.Description,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
                   .QueueCommand());
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        /// <summary>
        /// backup reference database
        /// </summary>
        /// <param name="backupType"></param>
        /// <returns></returns>
        public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
            => await _dbFactory.ReferenceDb
                .Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new { backupType = $"{backupType}" })
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);

       
    }
}
