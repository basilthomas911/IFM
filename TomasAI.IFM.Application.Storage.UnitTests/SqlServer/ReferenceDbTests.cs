using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Model;
using TomasAI.IFM.Shared.TaskScheduler;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.UnitTests.SqlServer
{
    public class ReferenceDbTests : IClassFixture<ReferenceFixture>
    {
        public ReferenceDbTests(ReferenceFixture testFixture)
        {
            TestFixture = testFixture;
        }

        public ReferenceFixture TestFixture { get; }

        [Fact]
        public async Task InsertEconomicCalendarAsyncOk()
        {
            var db = TestFixture.Database;
            await db.Use("delete from economic_calendar").ExecuteCommandAsync();
            await db.InsertEconomicCalendarAsync(SampleData.EconomicCalendar);
            var rowCount = await db.Use($"select count(*) from economic_calendar where EventName = '{SampleData.EconomicCalendar.EventName}'").ExecuteScalarAsync<int>();
            Assert.True(rowCount == 1);

            // delete test rows...
            await db.Use($"delete from economic_calendar where EventName = '{SampleData.EconomicCalendar.EventName}'").ExecuteCommandAsync();
        }

        [Fact]
        public async Task DeleteEconomicCalendarAsyncOk()
        {
            var db = TestFixture.Database;
            await db.Use("delete from economic_calendar").ExecuteCommandAsync();
            await db.InsertEconomicCalendarAsync(SampleData.EconomicCalendar);
            var rowCount = await db.Use($"select count(*) from economic_calendar where EventName = '{SampleData.EconomicCalendar.EventName}'").ExecuteScalarAsync<int>();
            Assert.True(rowCount == 1);

            await db.DeleteEconomicCalendarAsync(SampleData.EconomicCalendar.Id);
            rowCount = await db.Use($"select count(*) from economic_calendar where EventName = '{SampleData.EconomicCalendar.EventName}'").ExecuteScalarAsync<int>();
            Assert.True(rowCount == 0);

            // delete test rows...
            await db.Use($"delete from economic_calendar where EventName = '{SampleData.EconomicCalendar.EventName}'").ExecuteCommandAsync();
        }

        [Fact]
        public async Task GetEconomicCalendarAsyncOk()
        {
            var db = TestFixture.Database;
            await db.Use("delete from economic_calendar").ExecuteCommandAsync();
            await db.InsertEconomicCalendarAsync(SampleData.EconomicCalendar);
            var result = await db.GetEconomicCalendarAsync(SampleData.EconomicCalendar.Id);
            result.Should().NotBeNull();
            result.EventName.Should().Be(SampleData.EconomicCalendar.EventName);

            // delete test rows...
            await db.Use($"delete from economic_calendar where EventName = '{SampleData.EconomicCalendar.EventName}'").ExecuteCommandAsync();
        }

        [Fact]
        public async Task GetEconomicCalendarsAsyncOk()
        {
            var db = TestFixture.Database;
            await db.Use("delete from economic_calendar").ExecuteCommandAsync();
            await db.InsertEconomicCalendarAsync(SampleData.EconomicCalendar);
            await db.InsertEconomicCalendarAsync(SampleData.EconomicCalendar with { EventDate = SampleData.EconomicCalendar.EventDate.AddDays(5), EventName = "FOMC Meeting" });
            var result = await db.GetEconomicCalendarsAsync(SampleData.EconomicCalendar.EventDate, SampleData.EconomicCalendar.EventDate.AddDays(5), SampleData.EconomicCalendar.CountryCode);
            result.Should().NotBeNull();
            result.Count.Should().Be(2);
            result.ElementAt(0).EventName.Should().Be(SampleData.EconomicCalendar.EventName);
            result.ElementAt(1).EventName.Should().Be("FOMC Meeting");

            // delete test rows...
            await db.Use($"delete from economic_calendar").ExecuteCommandAsync();
        }

        [Fact]
        public async Task InsertScheduledJobOk()
        {
            var jobScheduleDate = new DateTime(2019, 4, 10);
            var scheduledJob = new ScheduledJob(0, "JobTestName", JobScheduleType.Daily, jobScheduleDate, 0.0, "TaskTestName", true, DateTime.Now, "basilt", DateTime.Now, "basilt");
            scheduledJob.JobScheduleDaysOfWeek = new ScheduledJobDaysOfWeek(0, true, true, true, true, true, false, false);

            // insert test scheduled job into table...
            var db = TestFixture.Database;
            await db.InsertScheduledJobAsync(scheduledJob.ToViewModel());
            var rowCount = await db.Use("select count(*) from scheduled_job where JobName = 'JobTestName'").ExecuteScalarAsync<int>();
            Assert.True(rowCount == 1);

            // get job id and check that row exists in scheduled job days table...
            var jobId = await db.Use("select JobId from scheduled_job where JobName = 'JobTestName'").ExecuteScalarAsync<int>();
            rowCount = await db.Use($"select count(*) from scheduled_job_days where JobId = {jobId}").ExecuteScalarAsync<int>();
            rowCount.Should().Be(1);

            // delete test rows...
            await db.Use($"delete from scheduled_job_days where JobId = {jobId}").ExecuteCommandAsync();
        }

        [Fact]
        public async Task GetScheduledJobsOk()
        {
            var jobScheduleDate = new DateTime(2019, 4, 10);
            var scheduledJob1 = new ScheduledJob(0, "JobTestName1", JobScheduleType.Daily, jobScheduleDate, 0.0, "TaskTestName1", true, DateTime.Now, "basilt", DateTime.Now, "basilt");
            scheduledJob1.JobScheduleDaysOfWeek = new ScheduledJobDaysOfWeek(0, true, true, true, true, true, false, false);
            var scheduledJob2 = new ScheduledJob(0, "JobTestName2", JobScheduleType.Daily, jobScheduleDate, 0.0, "TaskTestName2", true, DateTime.Now, "basilt", DateTime.Now, "basilt");
            scheduledJob2.JobScheduleDaysOfWeek = new ScheduledJobDaysOfWeek(0, true, true, true, true, true, false, false);

            // make sure test jobs do not exists in scheduled jobs table...
            var db = TestFixture.Database;
            await db.Use($"delete from scheduled_job").ExecuteCommandAsync();

            // insert test scheduled jobs into table...
            await db.InsertScheduledJobAsync(scheduledJob1.ToViewModel());
            await db.InsertScheduledJobAsync(scheduledJob2.ToViewModel());
            var rowCount = await db.Use("select count(*) from scheduled_job where JobName in ('JobTestName1','JobTestName2')").ExecuteScalarAsync<int>();
            rowCount.Should().Be(2);

            // get job id and check that row exists in scheduled job days table...
            var jobId1 = await db.Use("select JobId from scheduled_job where JobName = 'JobTestName1'").ExecuteScalarAsync<int>();
            rowCount = await db.Use($"select count(*) from scheduled_job_days where JobId = {jobId1}").ExecuteScalarAsync<int>();
            rowCount.Should().Be(1);

            var jobId2 = await db.Use("select JobId from scheduled_job where JobName = 'JobTestName2'").ExecuteScalarAsync<int>();
            rowCount = await db.Use($"select count(*) from scheduled_job_days where JobId = {jobId2}").ExecuteScalarAsync<int>();
            rowCount.Should().Be(1);

            var scheduledJobs = await db.GetScheduledJobsAsync();
            scheduledJobs.Count.Should().Be(2);

            var scheduledJob = scheduledJobs.Where(e => e.JobName == "JobTestName1").SingleOrDefault();
            scheduledJob.JobName.Should().Be(scheduledJob1.JobName);
            scheduledJob.JobSchedule.Should().Be(scheduledJob1.JobSchedule);
            scheduledJob.JobScheduleDate.Should().Be(scheduledJob1.JobScheduleDate);
            scheduledJob.JobScheduleInterval.Should().Be(scheduledJob1.JobScheduleInterval);
            scheduledJob.TaskName.Should().Be(scheduledJob1.TaskName);
            scheduledJob.TaskEnabled.Should().Be(scheduledJob1.TaskEnabled);

            scheduledJob = scheduledJobs.Where(e => e.JobName == "JobTestName2").SingleOrDefault();
            scheduledJob.JobName.Should().Be(scheduledJob2.JobName);
            scheduledJob.JobSchedule.Should().Be(scheduledJob2.JobSchedule);
            scheduledJob.JobScheduleDate.Should().Be(scheduledJob2.JobScheduleDate);
            scheduledJob.JobScheduleInterval.Should().Be(scheduledJob2.JobScheduleInterval);
            scheduledJob.TaskName.Should().Be(scheduledJob2.TaskName);
            scheduledJob.TaskEnabled.Should().Be(scheduledJob2.TaskEnabled);

            // delete test rows...
            await db.Use($"delete from scheduled_job where JobId = {jobId1}").ExecuteCommandAsync();
            await db.Use($"delete from scheduled_job where JobId = {jobId2}").ExecuteCommandAsync();
        }

        [Fact]
        public async Task DeleteScheduledJobOk()
        {
            var jobScheduleDate = new DateTime(2019, 4, 10);
            var scheduledJob = new ScheduledJob(0, "JobTestName", JobScheduleType.Daily, jobScheduleDate, 0.0, "TaskTestName", true, DateTime.Now, "basilt", DateTime.Now, "basilt");
            scheduledJob.JobScheduleDaysOfWeek = new ScheduledJobDaysOfWeek(0, true, true, true, true, true, false, false);

            var db = TestFixture.Database;
            await db.Use($"delete from scheduled_job").ExecuteCommandAsync();

            // insert test scheduled job into table...
            await db.InsertScheduledJobAsync(scheduledJob.ToViewModel());
            var rowCount = await db.Use("select count(*) from scheduled_job where JobName = 'JobTestName'").ExecuteScalarAsync<int>();
            rowCount.Should().Be(1);

            // get job id and check that row exists in scheduled job days table...
            var jobId = await db.Use("select JobId from scheduled_job where JobName = 'JobTestName'").ExecuteScalarAsync<int>();
            rowCount = await db.Use($"select count(*) from scheduled_job_days where JobId = {jobId}").ExecuteScalarAsync<int>();
            rowCount.Should().Be(1);

            // delete test rows...
            await db.DeleteScheduledJobAsync(jobId);
            rowCount = await db.Use($"select count(*) from scheduled_job_days where JobId = {jobId}").ExecuteScalarAsync<int>();
            rowCount.Should().Be(0);
        }



    }

    public class ReferenceFixture : IDisposable
    {
        public ReferenceFixture()
        {
            var dbConn = new DbConnectionSettings()
                 .Add("ReferenceDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=referencetestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, ReferenceDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            DbFactory = new DbContextFactory(dbResolver);
            var dbCache = new DbCache();
            var logger = Substitute.For<ILogger<ReferenceDbContext>>();
            logger.When(_ => { }).Do(_ => { });
            diContainer.Add(typeof(IObjectRepository<ReferenceDbContext>), new ReferenceDbContext(dbConn, DbFactory, logger));
            Database = DbFactory.ReferenceDb as ReferenceDbContext;
        }

        public ReferenceDbContext Database { get; }
        public IDbContextFactory DbFactory { get; }

        public void Dispose()
        {
            // Do "global" teardown here; Only called once.
        }

    }

}
