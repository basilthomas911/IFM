using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Caching;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.ReferenceDb
{
    public class ReferenceDatabaseFixture : IDisposable
    {

        public ReferenceDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                             .Add("ReferenceDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=reference_test_db", "System.Data.ScyllaDb");

            var diContainer = new Dictionary<Type, ReferenceDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var logger = Substitute.For<ILogger<DbProvider>>();
            logger.When(_ => { }).Do(_ => { });
            var redisCache = Substitute.For<IRedisCache>();
            var redisCacheMap = new Dictionary<string, string>();
            redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
            redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
            var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
            DbFactory = new DbContextFactory(dbResolver);
            var dbCache = new DbCache();
            diContainer.Add(typeof(IObjectRepository<ReferenceDbContext>), new ReferenceDbContext(dbConn, DbFactory, logger));
            ReferenceDb = DbFactory.ReferenceDb as ReferenceDbContext;
        }

        public IReferenceDbContext ReferenceDb { get; }

        public IDbContextFactory DbFactory { get; }

        public void Dispose()
        {
        }
    }


    public class ReferenceDbTests : IClassFixture<ReferenceDatabaseFixture>
    {
        readonly ReferenceDatabaseFixture _testFixture;

        public ReferenceDbTests(ReferenceDatabaseFixture testFixture)
        {
            _testFixture = testFixture;
        }

        [Fact]
        [Trait("get next seed id", "ReferenceDb")]
        public async Task GetNextSeedIdAsyncOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = db as IReferenceDbReadContext;
            var nextSeedId = await dbReader.GetNextSeedIdAsync("FundId");
            nextSeedId.Should().BeGreaterThan(0);
        }

        [Fact]
        [Trait("insert lookup type", "ReferenceDb")]
        public async Task InsertLookupTypeAsyncOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            await db.Use($"delete from lookup_type where lookupTypeName = '{SampleData.LookupType.LookupTypeName}' ").ExecuteCommandAsync();
            var dbReader = db as IReferenceDbReadContext;
            var dbWriter = db as IReferenceDbWriteContext;
            await dbWriter.InsertLookupTypeAsync(SampleData.LookupType);
            var lookupType = await dbReader.GetLookupTypeAsync(SampleData.LookupType.Id);
            lookupType.Should().NotBeNull();
            lookupType.Should().BeEquivalentTo(SampleData.LookupType);
            lookupType.LookupTypeName.Should().Be(SampleData.LookupType.LookupTypeName);
            lookupType.ShortCode.Should().Be(SampleData.LookupType.ShortCode);
        }

        [Fact]
        [Trait("get all lookup type", "ReferenceDb")]
        public async Task GetLookupTypesAsyncOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            await db.Use($"delete from lookup_type where lookupTypeName = '{SampleData.LookupType.LookupTypeName}' ").ExecuteCommandAsync();
            var dbReader = db as IReferenceDbReadContext;
            var dbWriter = db as IReferenceDbWriteContext;
            await dbWriter.InsertLookupTypeAsync(SampleData.LookupType);
            var lookupTypes = await dbReader.GetLookupTypesAsync();
            lookupTypes.Should().NotBeNull();
            lookupTypes.Count.Should().BeGreaterThan(0);
            lookupTypes.Where(lt => lt.LookupTypeName == SampleData.LookupType.LookupTypeName).SingleOrDefault().Should().NotBeNull();
        }


        [Fact]
        [Trait("get lookup type by lookupTypeName", "ReferenceDb")]
        public async Task GetLookupTypeAsyncOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            await db.Use($"delete from lookup_type where lookupTypeName = '{SampleData.LookupType.LookupTypeName}' ").ExecuteCommandAsync();
            var dbReader = db as IReferenceDbReadContext;
            var dbWriter = db as IReferenceDbWriteContext;
            await dbWriter.InsertLookupTypeAsync(SampleData.LookupType);
            var lookupTypes = await dbReader.GetLookupTypeAsync(SampleData.LookupType.LookupTypeName);
            lookupTypes.Should().NotBeNull();
            lookupTypes.Count.Should().BeGreaterThan(0);
            lookupTypes.Where(lt => lt.LookupTypeName == SampleData.LookupType.LookupTypeName).SingleOrDefault().Should().NotBeNull();
        }

        [Fact]
        [Trait("get all lookup type names", "ReferenceDb")]
        public async Task GetLookupTypeNamesAsyncOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            await db.Use($"delete from lookup_type where lookupTypeName = '{SampleData.LookupType.LookupTypeName}' ").ExecuteCommandAsync();
            var dbReader = db as IReferenceDbReadContext;
            var dbWriter = db as IReferenceDbWriteContext;
            await dbWriter.InsertLookupTypeAsync(SampleData.LookupType);
            var lookupTypeNames = await dbReader.GetLookupTypeNamesAsync();
            lookupTypeNames.Should().NotBeNull();
            lookupTypeNames.Count.Should().BeGreaterThan(0);
            lookupTypeNames.Where(e => e == SampleData.LookupType.LookupTypeName).SingleOrDefault().Should().NotBeNull();
        }

        [Fact]
        [Trait("delete lookup type", "ReferenceDb")]
        public async Task DeleteLookupTypeAsyncOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            await db.Use($"delete from lookup_type where lookupTypeName = '{SampleData.LookupType.LookupTypeName}' ").ExecuteCommandAsync();
            var dbReader = db as IReferenceDbReadContext;
            var dbWriter = db as IReferenceDbWriteContext;
            await dbWriter.InsertLookupTypeAsync(SampleData.LookupType);
            await dbWriter.InsertLookupTypeAsync(SampleData.LookupType with { OrderId = 2, ShortCode = "ST2"});
            await dbWriter.DeleteLookupTypeAsync(SampleData.LookupType.Id);
            var updatedLookupType = SampleData.LookupType with { OrderId = 0 };
            var lookupType = await dbReader.GetLookupTypeAsync(updatedLookupType.Id);
            lookupType.Should().NotBeNull();
            lookupType.LookupTypeName.Should().Be(SampleData.LookupType.LookupTypeName);
            lookupType.ShortCode.Should().Be("ST2");
            lookupType.OrderId.Should().Be(0);  
            lookupType.Id.OrderId.Should().Be(0);
        }

        [Fact]
        [Trait("insert scheduled job", "ReferenceDb")]
        public async Task InsertScheduleJobAsyncOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            await db.Use($"delete from scheduled_job where jobId = {SampleData.ScheduledJob.JobId} ").ExecuteCommandAsync();
            var dbReader = db as IReferenceDbReadContext;
            var dbWriter = db as IReferenceDbWriteContext;
            await dbWriter.InsertScheduledJobAsync(SampleData.ScheduledJob);
            var scheduledJobs = await dbReader.GetScheduledJobsAsync();
            scheduledJobs.Should().NotBeNull();
            scheduledJobs.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetEconomicCalendarsFromCsvFileOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var resultSet = await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\economic-calendars.csv"))
               .ReadAsync<EconomicCalendarReadModel>(MapToEconomicCalendar);
            resultSet.Should().NotBeNull();
            resultSet.Count.Should().BeGreaterThan(0);
            await db.Use($"truncate economic_calendar").ExecuteCommandAsync();
            var dbRef = db as IReferenceDbContext;
            await dbRef.InsertEconomicCalendarsAsync(resultSet);
            return;

            static EconomicCalendarReadModel MapToEconomicCalendar(IObjectMapReader<EconomicCalendarReadModel> o)
                => new(
                    o.Get(e => e.EventDate),
                    o.Get(e => e.CountryCode),
                    o.Get(e => e.EventName),
                    o.Get(e => e.Actual),
                    o.Get(e => e.Forecast),
                    o.Get(e => e.Prior),
                    o.Get(e => e.CreatedOn),
                    o.Get(e => e.CreatedBy)
                );  
        }
    }
}
