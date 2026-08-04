using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ReferenceDatabaseNonParallelCollection
    {
        public const string Name = "Reference database non-parallel";
    }

    public class ReferenceDatabaseFixture : IDisposable
    {

        public ReferenceDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                             .Add("ReferenceDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=reference_test_db", "System.Data.ScyllaDb");

            var diContainer = new Dictionary<Type, ReferenceDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var logger = Substitute.For<ILogger<DbProvider>>();
            logger.When(_ => { }).Do(_ => { });
            new TomasAI.IFM.Application.Storage.ReferenceDb.Schema.ReferenceSchemaDb(dbConn, logger)
                .CreateAllAsync().GetAwaiter().GetResult();
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


    [Collection(ReferenceDatabaseNonParallelCollection.Name)]
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
            var dbReader = db as IReferenceDbReadContext;
            var dbWriter = db as IReferenceDbWriteContext;
            var scheduledJob = SampleData.ScheduledJob;
            await DeleteScheduledJobsNamedAsync(db, scheduledJob.JobName);

            try
            {
                await dbWriter.InsertScheduledJobAsync(scheduledJob);
                var scheduledJobId = await dbReader.GetScheduledJobIdAsync(scheduledJob.JobName);
                scheduledJobId.Should().BeGreaterThan(0);

                var scheduledJobs = await dbReader.GetScheduledJobsAsync();
                scheduledJobs.Should().Contain(job =>
                    job.JobId == scheduledJobId && job.JobName == scheduledJob.JobName);
            }
            finally
            {
                await DeleteScheduledJobsNamedAsync(db, scheduledJob.JobName);
            }
        }

        [Fact]
        [Trait("economic calendar projection", "ReferenceDb")]
        public async Task EconomicCalendarProjection_UsesCountryMonthBucketsAndInclusiveEndDate()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var countryCode = $"projection-{suffix}";
            var otherCountryCode = $"projection-other-{suffix}";
            var createdOn = new DateTime(2044, 12, 1);
            var startDate = new DateTime(2045, 1, 31, 12, 0, 0);
            var endDate = new DateTime(2045, 3, 1, 0, 0, 0);
            EconomicCalendarReadModel[] rows =
            [
                new(startDate, countryCode, $"projection-start-{suffix}", "1", "1", "1", createdOn, "test"),
                new(new DateTime(2045, 2, 1, 12, 0, 0), countryCode, $"projection-middle-{suffix}", "2", "2", "2", createdOn, "test"),
                new(endDate.AddMilliseconds(-1), countryCode, $"projection-before-end-{suffix}", "3", "3", "3", createdOn, "test"),
                new(endDate, countryCode, $"projection-end-boundary-{suffix}", "4", "4", "4", createdOn, "test"),
                new(new DateTime(2045, 2, 1, 13, 0, 0), otherCountryCode, $"projection-country-distractor-{suffix}", "5", "5", "5", createdOn, "test")
            ];

            try
            {
                await dbWriter.InsertEconomicCalendarsAsync(rows);

                var result = await dbReader.GetEconomicCalendarsAsync(
                    startDate,
                    endDate,
                    countryCode);

                result.Select(static row => row.EventName)
                    .Should().Equal(
                        $"projection-end-boundary-{suffix}",
                        $"projection-before-end-{suffix}",
                        $"projection-middle-{suffix}",
                        $"projection-start-{suffix}");
            }
            finally
            {
                foreach (var row in rows)
                    await dbWriter.DeleteEconomicCalendarAsync(row.Id);
                await DeleteEconomicCalendarProjectionStateAsync(db);
            }
        }

        [Fact]
        [Trait("reject ambiguous reference batches", "ReferenceDb")]
        public async Task ReferenceBatches_RejectPhysicalKeyDuplicatesBeforeSubmission()
        {
            var dbWriter = (IReferenceDbWriteContext)_testFixture.DbFactory.ReferenceDb;
            var eventDate = new DateTime(2045, 2, 1, 12, 0, 0, DateTimeKind.Utc)
                .AddTicks(1);
            var calendar = new EconomicCalendarReadModel(
                eventDate,
                "duplicate-country",
                "duplicate-event",
                "1", "1", "1", DateTime.UtcNow, "test");
            var sameScyllaMillisecond = calendar with
            {
                EventDate = eventDate.AddTicks(1),
                Actual = "different-payload"
            };

            await FluentActions.Awaiting(() => dbWriter.InsertEconomicCalendarsAsync(
                    new[] { calendar, sameScyllaMillisecond }))
                .Should().ThrowAsync<ArgumentException>(
                    "Scylla timestamp keys serialize at UTC millisecond precision");

            var ratio = new MDIForwardLossRatioReadModel(
                12,
                global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.IntrinsicTimeTrendType.UpTrend,
                global::TomasAI.IFM.Domain.Trade.Shared.TradeType.LongCall,
                0.25,
                "test",
                DateTime.UtcNow,
                "test",
                DateTime.UtcNow);
            await FluentActions.Awaiting(() => dbWriter.InsertMDIForwardLossRatiosAsync(
                    new[] { ratio, ratio with { ForwardLossRatio = 0.75 } }))
                .Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        [Trait("economic calendar projection scope", "ReferenceDb")]
        public async Task EconomicCalendarProjection_DisjointBucketWriteKeepsExistingScopeReady()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var first = new EconomicCalendarReadModel(
                new DateTime(2048, 1, 12, 12, 0, 0),
                $"scope-first-{suffix}",
                $"scope-first-event-{suffix}",
                "1", "1", "1", DateTime.UtcNow, "test");
            var second = new EconomicCalendarReadModel(
                new DateTime(2048, 2, 12, 12, 0, 0),
                $"scope-second-{suffix}",
                $"scope-second-event-{suffix}",
                "2", "2", "2", DateTime.UtcNow, "test");
            await dbWriter.BackfillQueryProjectionsV2Async(batchSize: 32);

            try
            {
                await dbWriter.InsertEconomicCalendarAsync(first);
                var firstScope = ReferenceDbContext.GetEconomicCalendarProjectionScope(
                    first.CountryCode,
                    204801);
                var firstGeneration = await db.Use(ReferenceDbCql.GetReferenceProjectionStateV3)
                    .SetParameters(new GetReferenceProjectionStateV3(firstScope))
                    .ExecuteSingleAsync(static row => new
                    {
                        Generation = row.GetGuid(0),
                        Completed = row.GetBool(1)
                    });
                firstGeneration.Should().NotBeNull();
                firstGeneration!.Completed.Should().BeTrue();

                await dbWriter.InsertEconomicCalendarAsync(second);
                var firstAfterSecondWrite = await db.Use(ReferenceDbCql.GetReferenceProjectionStateV3)
                    .SetParameters(new GetReferenceProjectionStateV3(firstScope))
                    .ExecuteSingleAsync(static row => new
                    {
                        Generation = row.GetGuid(0),
                        Completed = row.GetBool(1)
                    });
                firstAfterSecondWrite.Should().BeEquivalentTo(firstGeneration);

                var secondScope = ReferenceDbContext.GetEconomicCalendarProjectionScope(
                    second.CountryCode,
                    204802);
                var secondCompleted = await db.Use(ReferenceDbCql.GetReferenceProjectionStateV3)
                    .SetParameters(new GetReferenceProjectionStateV3(secondScope))
                    .ExecuteSingleAsync(static row => row.GetBool(1));
                secondCompleted.Should().BeTrue();
            }
            finally
            {
                await dbWriter.DeleteEconomicCalendarAsync(first.Id);
                await dbWriter.DeleteEconomicCalendarAsync(second.Id);
            }
        }

        [Fact]
        [Trait("recover a stale reference projection mutation", "ReferenceDb")]
        public async Task ReferenceBackfill_ExplicitUtcCutoffRecoversJournaledGroupAndScopeMutations()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbWriter = (IReferenceDbWriteContext)db;
            const string projectionName = "economic_calendar_by_country_month_v2";
            var scopeName = ReferenceDbContext.GetEconomicCalendarProjectionScope(
                $"stale-{Guid.NewGuid():N}",
                204901);
            var groupMutationId = Guid.NewGuid();
            var scopeMutationId = Guid.NewGuid();
            var cutoffUtc = DateTime.UtcNow.AddMinutes(-5);
            var startedOn = cutoffUtc.AddMinutes(-1);
            await dbWriter.BackfillQueryProjectionsV2Async(batchSize: 32);

            foreach (var mutation in new[]
            {
                (ProjectionName: projectionName, MutationId: groupMutationId),
                (ProjectionName: scopeName, MutationId: scopeMutationId)
            })
            {
                await db.Use(ReferenceDbCql.InsertReferenceProjectionMutationV3)
                    .SetParameters(new InsertReferenceProjectionMutationV3(
                        mutation.ProjectionName,
                        mutation.MutationId,
                        startedOn))
                    .ExecuteCommandAsync();
                _ = await db.Use(ReferenceDbCql.ClaimReferenceProjectionOwnershipV3)
                    .SetParameters(new ClaimReferenceProjectionOwnershipV3(
                        mutation.ProjectionName,
                        mutation.MutationId,
                        startedOn))
                    .ExecuteScalarAsync(static row => row.GetBool(0));
            }

            try
            {
                await dbWriter.BackfillQueryProjectionsV2Async(
                    batchSize: 32,
                    cancellationToken: CancellationToken.None,
                    staleOperationCutoffUtc: cutoffUtc);

                foreach (var projectionScope in new[] { projectionName, scopeName })
                {
                    (await db.Use(ReferenceDbCql.GetReferenceProjectionMutationsV3)
                            .SetParameters(new GetReferenceProjectionMutationsV3(projectionScope))
                            .ExecuteQueryAsync(static row => row.GetGuid(0)))
                        .Should().BeEmpty();
                }
            }
            finally
            {
                foreach (var mutation in new[]
                {
                    (ProjectionName: projectionName, MutationId: groupMutationId),
                    (ProjectionName: scopeName, MutationId: scopeMutationId)
                })
                {
                    _ = await db.Use(ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)
                        .SetParameters(new ReleaseReferenceProjectionOwnershipV3(
                            mutation.ProjectionName,
                            mutation.MutationId))
                        .ExecuteScalarAsync(static row => row.GetBool(0));
                    await db.Use(ReferenceDbCql.DeleteReferenceProjectionMutationV3)
                        .SetParameters(new DeleteReferenceProjectionMutationV3(
                            mutation.ProjectionName,
                            mutation.MutationId))
                        .ExecuteCommandAsync();
                }
            }
        }

        [Fact]
        [Trait("economic calendar projection migration", "ReferenceDb")]
        public async Task EconomicCalendarProjection_IncompleteStateUsesCanonicalRowsWithoutPublishingCutover()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var countryCode = $"migration-{suffix}";
            var createdOn = new DateTime(2049, 12, 1);
            var early = new EconomicCalendarReadModel(
                new DateTime(2050, 1, 2, 12, 0, 0), countryCode, $"migration-early-{suffix}",
                "1", "1", "1", createdOn, "test");
            var late = new EconomicCalendarReadModel(
                new DateTime(2050, 1, 28, 12, 0, 0), countryCode, $"migration-late-{suffix}",
                "2", "2", "2", createdOn, "test");

            await InvalidateEconomicCalendarProjectionStateAsync(db);
            await DeleteEconomicCalendarProjectionAsync(db, early, 205001);
            await DeleteEconomicCalendarProjectionAsync(db, late, 205001);
            await InsertCanonicalEconomicCalendarAsync(db, early);
            await InsertCanonicalEconomicCalendarAsync(db, late);
            await InsertEconomicCalendarProjectionAsync(db, early, 205001);

            try
            {
                var narrow = await dbReader.GetEconomicCalendarsAsync(
                    new DateTime(2050, 1, 1),
                    new DateTime(2050, 1, 10),
                    countryCode);
                narrow.Select(static row => row.EventName).Should().Equal(early.EventName);

                var state = await db.Use(ReferenceDbCql.GetReferenceProjectionStateV3)
                    .SetParameters(new GetReferenceProjectionStateV3("economic_calendar_by_country_month_v2"))
                    .ExecuteSingleAsync(static record => new
                    {
                        Generation = record.GetGuid(0),
                        Completed = record.GetBool(1)
                    });
                state.Should().NotBeNull();
                state!.Completed.Should().BeFalse();

                // An incomplete projection is never self-published by a reader. Removing this
                // canonical row must therefore remove it from the next fallback result too.
                await DeleteCanonicalEconomicCalendarAsync(db, late);

                var broad = await dbReader.GetEconomicCalendarsAsync(
                    new DateTime(2050, 1, 1),
                    new DateTime(2050, 2, 1),
                    countryCode);
                broad.Select(static row => row.EventName).Should().Equal(early.EventName);
            }
            finally
            {
                await DeleteCanonicalEconomicCalendarAsync(db, early);
                await DeleteCanonicalEconomicCalendarAsync(db, late);
                await DeleteEconomicCalendarProjectionAsync(db, early, 205001);
                await DeleteEconomicCalendarProjectionAsync(db, late, 205001);
                await DeleteEconomicCalendarProjectionStateAsync(db);
            }
        }

        [Fact]
        [Trait("economic calendar projection migration", "ReferenceDb")]
        public async Task EconomicCalendarProjection_EmptyFallbackDoesNotPublishCutover()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var countryCode = $"empty-migration-{Guid.NewGuid():N}";
            await InvalidateEconomicCalendarProjectionStateAsync(db);

            try
            {
                var rows = await dbReader.GetEconomicCalendarsAsync(
                    new DateTime(2051, 2, 1),
                    new DateTime(2051, 3, 1),
                    countryCode);
                rows.Should().BeEmpty();

                var stateCompleted = await db.Use(ReferenceDbCql.GetReferenceProjectionStateV3)
                    .SetParameters(new GetReferenceProjectionStateV3("economic_calendar_by_country_month_v2"))
                    .ExecuteSingleAsync(static record => record.GetBool(1));
                stateCompleted.Should().BeFalse();
            }
            finally
            {
                await DeleteEconomicCalendarProjectionStateAsync(db);
            }
        }

        [Fact]
        [Trait("economic calendar projection reconciliation", "ReferenceDb")]
        public async Task EconomicCalendarProjection_ReconciliationDetectsWrongMonthBucket()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var row = new EconomicCalendarReadModel(
                new DateTime(2088, 1, 15, 12, 0, 0),
                $"reconcile-{suffix}",
                $"reconcile-wrong-month-{suffix}",
                "1", "1", "1", new DateTime(2087, 12, 1), "test");
            const int wrongMonthBucket = 208802;
            var before = await dbWriter.ReconcileQueryProjectionsV2Async();

            await InsertCanonicalEconomicCalendarAsync(db, row);
            await InsertEconomicCalendarProjectionAsync(db, row, wrongMonthBucket);
            try
            {
                var direct = await ((IReferenceDbReadContext)db).GetEconomicCalendarAsync(row.Id);
                direct.Should().NotBeNull("the canonical insert must be visible before reconciliation starts");
                direct!.Id.Should().Be(row.Id);

                var materializedKeys = await db.Use(ReferenceDbCql.GetEconomicCalendarKeysAll)
                    .ExecuteQueryAsync(static record => new EconomicCalendarId(
                        record.GetDateTime(1),
                        record.GetString(0),
                        record.GetString(2)));
                materializedKeys.Should().Contain(row.Id);

                long streamedKeyCount = 0;
                var streamContainsInsertedKey = false;
                using var streamTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                await foreach (var key in db.Use(ReferenceDbCql.GetEconomicCalendarKeysAll)
                    .ExecuteStreamAsync(static record => new EconomicCalendarId(
                        record.GetDateTime(1),
                        record.GetString(0),
                        record.GetString(2)), streamTimeout.Token))
                {
                    streamedKeyCount++;
                    streamContainsInsertedKey |= key == row.Id;
                }
                streamContainsInsertedKey.Should().BeTrue(
                    "the streaming provider must fetch every page, including the inserted canonical key");
                streamedKeyCount.Should().Be(materializedKeys.Count,
                    "streaming and auto-paged materialization must observe the same canonical rows");

                var after = await dbWriter.ReconcileQueryProjectionsV2Async();

                after.SourceEconomicCalendars.Should().Be(before.SourceEconomicCalendars + 1);
                after.ProjectedEconomicCalendars.Should().Be(before.ProjectedEconomicCalendars + 1);
                after.MissingEconomicCalendars.Should().Be(before.MissingEconomicCalendars + 1);
                after.UnexpectedEconomicCalendars.Should().Be(before.UnexpectedEconomicCalendars + 1);
            }
            finally
            {
                await DeleteCanonicalEconomicCalendarAsync(db, row);
                await DeleteEconomicCalendarProjectionAsync(db, row, wrongMonthBucket);
            }
        }

        [Fact]
        [Trait("scheduled job projection", "ReferenceDb")]
        public async Task ScheduledJobNameProjection_RenameRemovesOldNameAndDeleteRemovesNewName()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var original = SampleData.ScheduledJob with { JobName = $"projection-job-{suffix}" };
            var renamed = $"projection-job-renamed-{suffix}";

            await dbWriter.InsertScheduledJobAsync(original);
            var jobId = await dbReader.GetScheduledJobIdAsync(original.JobName);
            var deleted = false;
            try
            {
                jobId.Should().BeGreaterThan(0);
                await dbWriter.UpdateScheduledJobAsync(original with { JobId = jobId, JobName = renamed });
                (await dbReader.GetScheduledJobIdAsync(original.JobName)).Should().Be(0);
                (await dbReader.GetScheduledJobIdAsync(renamed)).Should().Be(jobId);

                await dbWriter.DeleteScheduledJobAsync(jobId);
                deleted = true;
                (await dbReader.GetScheduledJobIdAsync(renamed)).Should().Be(0);
            }
            finally
            {
                if (jobId > 0 && !deleted)
                    await dbWriter.DeleteScheduledJobAsync(jobId);
                await DeleteScheduledJobProjectionAsync(db, original.JobName);
                await DeleteScheduledJobProjectionAsync(db, renamed);
            }
        }

        [Fact]
        [Trait("scheduled job distributed ownership", "ReferenceDb")]
        public async Task ScheduledJobWrite_HeldIdAndNameScopesRejectContendersThenReleaseForRetry()
        {
            var context = (ReferenceDbContext)_testFixture.ReferenceDb;
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var original = SampleData.ScheduledJob with { JobName = $"ownership-original-{suffix}" };
            var renamedName = $"ownership-renamed-{suffix}";
            var ownershipHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resumeUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var jobId = 0;

            try
            {
                await dbWriter.InsertScheduledJobAsync(original);
                jobId = await dbReader.GetScheduledJobIdAsync(original.JobName);
                jobId.Should().BeGreaterThan(0);

                context.ScheduledJobCanonicalMutationSubmittingForTestingAsync = async () =>
                {
                    ownershipHeld.TrySetResult();
                    await resumeUpdate.Task;
                };
                var update = dbWriter.UpdateScheduledJobAsync(original with
                {
                    JobId = jobId,
                    JobName = renamedName
                });
                await ownershipHeld.Task.WaitAsync(TimeSpan.FromSeconds(15));

                await FluentActions.Awaiting(() => dbWriter.DeleteScheduledJobAsync(jobId))
                    .Should().ThrowAsync<StorageException>("the job-ID scope is held by the rename");
                await FluentActions.Awaiting(() => dbWriter.InsertScheduledJobAsync(
                        original with { JobName = renamedName }))
                    .Should().ThrowAsync<StorageException>("the destination name scope is held by the rename");

                resumeUpdate.TrySetResult();
                await update;
                context.ScheduledJobCanonicalMutationSubmittingForTestingAsync = null;

                (await dbReader.GetScheduledJobIdAsync(renamedName)).Should().Be(jobId);
                await dbWriter.DeleteScheduledJobAsync(jobId);
                jobId = 0;
                (await dbReader.GetScheduledJobIdAsync(renamedName)).Should().Be(0,
                    "successful completion releases ownership for the retrying delete");
            }
            finally
            {
                resumeUpdate.TrySetResult();
                context.ScheduledJobCanonicalMutationSubmittingForTestingAsync = null;
                if (jobId > 0)
                {
                    await db.Use(ReferenceDbCql.DeleteScheduledJob)
                        .SetParameters(new DeleteScheduledJob(jobId))
                        .ExecuteCommandAsync();
                    await db.Use(ReferenceDbCql.DeleteScheduledJobDays)
                        .SetParameters(new DeleteScheduledJobDays(jobId))
                        .ExecuteCommandAsync();
                }
                await DeleteScheduledJobProjectionAsync(db, original.JobName);
                await DeleteScheduledJobProjectionAsync(db, renamedName);
            }
        }

        [Fact]
        [Trait("scheduled job rename ownership cleanup", "ReferenceDb")]
        public async Task ScheduledJobRename_OccupiedDestinationFailsWithoutWedgingOwnership()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var first = SampleData.ScheduledJob with { JobName = $"occupied-source-{suffix}" };
            var second = SampleData.ScheduledJob with { JobName = $"occupied-target-{suffix}" };
            var firstId = 0;
            var secondId = 0;

            try
            {
                await dbWriter.InsertScheduledJobAsync(first);
                await dbWriter.InsertScheduledJobAsync(second);
                firstId = await dbReader.GetScheduledJobIdAsync(first.JobName);
                secondId = await dbReader.GetScheduledJobIdAsync(second.JobName);

                await FluentActions.Awaiting(() => dbWriter.UpdateScheduledJobAsync(first with
                    {
                        JobId = firstId,
                        JobName = second.JobName
                    }))
                    .Should().ThrowAsync<StorageException>();

                (await dbReader.GetScheduledJobIdAsync(first.JobName)).Should().Be(firstId);
                (await dbReader.GetScheduledJobIdAsync(second.JobName)).Should().Be(secondId);

                // Both ordinary follow-ups must acquire the scopes touched by the
                // failed rename; success proves its exact ownership was released.
                await dbWriter.DeleteScheduledJobAsync(firstId);
                firstId = 0;
                await dbWriter.DeleteScheduledJobAsync(secondId);
                secondId = 0;
            }
            finally
            {
                if (firstId > 0)
                    await dbWriter.DeleteScheduledJobAsync(firstId);
                if (secondId > 0)
                    await dbWriter.DeleteScheduledJobAsync(secondId);
                await DeleteScheduledJobProjectionAsync(db, first.JobName);
                await DeleteScheduledJobProjectionAsync(db, second.JobName);
            }
        }

        [Fact]
        [Trait("online scheduled job projection backfill", "ReferenceDb")]
        public async Task ScheduledJobBackfill_PreservesProjectionOnlyReservationAndReportsItUnexpected()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbWriter = (IReferenceDbWriteContext)db;
            var jobName = $"projection-inflight-{Guid.NewGuid():N}";
            var jobId = Random.Shared.Next(1_500_000_000, 2_000_000_000);
            var reservationToken = Guid.NewGuid();
            await dbWriter.BackfillQueryProjectionsV2Async(batchSize: 32);

            await db.Use(ReferenceDbCql.InsertScheduledJobByNameV3)
                .SetParameters(new InsertScheduledJobByNameV3(jobName, jobId, reservationToken))
                .ExecuteCommandAsync();
            try
            {
                // This is the observable midpoint of a live insert: its LWT
                // reservation is acknowledged, but the canonical row is not yet visible.
                Func<Task> backfill = () => dbWriter.BackfillQueryProjectionsV2Async(batchSize: 32);
                var failure = await backfill.Should().ThrowAsync<StorageException>();
                failure.WithMessage("*jobs unexpected=1*");

                var reconciliation = await dbWriter.ReconcileQueryProjectionsV2Async();
                reconciliation.UnexpectedScheduledJobs.Should().Be(1);
                var reservedJobId = await db.Use(ReferenceDbCql.GetScheduledJobId)
                    .SetParameters(new GetScheduledJobId(jobName))
                    .ExecuteScalarAsync(static row => row.GetInt(0));
                reservedJobId.Should().Be(jobId,
                    "an online backfill must not release an in-flight writer's uniqueness reservation");
            }
            finally
            {
                await DeleteScheduledJobProjectionAsync(db, jobName);
                await dbWriter.BackfillQueryProjectionsV2Async(
                    batchSize: 32,
                    cancellationToken: CancellationToken.None,
                    staleOperationCutoffUtc: DateTime.UtcNow);
            }
        }

        [Fact]
        [Trait("scheduled job reservation epoch", "ReferenceDb")]
        public async Task ScheduledJobRecreate_RotatesSameOwnerTokenSoDelayedOldReleaseCannotDeleteNewIncarnation()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var job = SampleData.ScheduledJob with { JobName = $"projection-epoch-{suffix}" };
            var jobId = 0;
            var recreated = false;

            try
            {
                await dbWriter.InsertScheduledJobAsync(job);
                jobId = await dbReader.GetScheduledJobIdAsync(job.JobName);
                jobId.Should().BeGreaterThan(0);
                var oldReservationToken = await db.Use(ReferenceDbCql.GetScheduledJobReservationV3)
                    .SetParameters(new GetScheduledJobReservationV3(job.JobName))
                    .ExecuteSingleAsync(static row => row.GetGuid(1));

                // Simulate a positively acknowledged canonical delete whose old
                // conditional reservation release is still delayed in the network.
                await db.Use(ReferenceDbCql.DeleteScheduledJob)
                    .SetParameters(new DeleteScheduledJob(jobId))
                    .ExecuteCommandAsync();
                await db.Use(ReferenceDbCql.DeleteScheduledJobDays)
                    .SetParameters(new DeleteScheduledJobDays(jobId))
                    .ExecuteCommandAsync();

                await dbWriter.InsertScheduledJobAsync(job with
                {
                    TaskName = $"recreated-{suffix}"
                });
                recreated = true;

                var currentReservationToken = await db.Use(ReferenceDbCql.GetScheduledJobReservationV3)
                    .SetParameters(new GetScheduledJobReservationV3(job.JobName))
                    .ExecuteSingleAsync(static row => row.GetGuid(1));
                currentReservationToken.Should().NotBe(oldReservationToken);

                var delayedOldReleaseApplied = await db.Use(ReferenceDbCql.ReleaseScheduledJobNameV3)
                    .SetParameters(new ReleaseScheduledJobNameV3(
                        job.JobName,
                        jobId,
                        oldReservationToken))
                    .ExecuteScalarAsync(static row => row.GetBool(0));
                delayedOldReleaseApplied.Should().BeFalse();
                (await dbReader.GetScheduledJobIdAsync(job.JobName)).Should().Be(jobId);
            }
            finally
            {
                if (jobId > 0 && recreated)
                    await dbWriter.DeleteScheduledJobAsync(jobId);
                else if (jobId > 0)
                {
                    await db.Use(ReferenceDbCql.DeleteScheduledJob)
                        .SetParameters(new DeleteScheduledJob(jobId))
                        .ExecuteCommandAsync();
                    await db.Use(ReferenceDbCql.DeleteScheduledJobDays)
                        .SetParameters(new DeleteScheduledJobDays(jobId))
                        .ExecuteCommandAsync();
                }
                await DeleteScheduledJobProjectionAsync(db, job.JobName);
            }
        }

        [Fact]
        [Trait("online scheduled job projection backfill", "ReferenceDb")]
        public async Task ScheduledJobBackfill_CompensatesItsReservationWhenCanonicalRowIsDeletedAfterLwt()
        {
            var context = (ReferenceDbContext)_testFixture.ReferenceDb;
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var jobName = $"projection-overlap-{suffix}";
            var jobId = Random.Shared.Next(1_500_000_000, 1_700_000_000);
            var replacementJobId = jobId + 1;
            var job = SampleData.ScheduledJob with { JobId = jobId, JobName = jobName };
            await dbWriter.BackfillQueryProjectionsV2Async(batchSize: 32);
            await db.Use(ReferenceDbCql.InsertScheduledJob)
                .SetParameters(new InsertScheduledJob(
                    job.JobId,
                    job.JobName,
                    job.JobSchedule.ToString(),
                    job.JobScheduleDate,
                    job.JobScheduleInterval,
                    job.TaskName,
                    job.TaskEnabled,
                    job.CreatedOn,
                    job.CreatedBy))
                .ExecuteCommandAsync();

            context.ScheduledJobBackfillReservationInsertedForTestingAsync = async (candidateName, candidateId) =>
            {
                if (!string.Equals(candidateName, jobName, StringComparison.Ordinal) || candidateId != jobId)
                    return;
                await dbWriter.DeleteScheduledJobAsync(jobId);
            };
            try
            {
                Func<Task> overlappedBackfill = () =>
                    dbWriter.BackfillQueryProjectionsV2Async(batchSize: 32);
                await overlappedBackfill.Should().ThrowAsync<StorageException>(
                    "the overlapping delete must supersede global cutover");

                (await db.Use(ReferenceDbCql.GetScheduledJobId)
                        .SetParameters(new GetScheduledJobId(jobName))
                        .ExecuteScalarAsync(static row => row.GetInt(0)))
                    .Should().Be(0);

                var replacementToken = Guid.NewGuid();
                var replacementApplied = await db.Use(ReferenceDbCql.InsertScheduledJobByNameV3)
                    .SetParameters(new InsertScheduledJobByNameV3(
                        jobName,
                        replacementJobId,
                        replacementToken))
                    .ExecuteSingleAsync(static row => row.GetBool(0));
                replacementApplied.Should().BeTrue(
                    "the stale backfill candidate must not block a later different-owner reservation");
            }
            finally
            {
                context.ScheduledJobBackfillReservationInsertedForTestingAsync = null;
                await db.Use(ReferenceDbCql.DeleteScheduledJob)
                    .SetParameters(new DeleteScheduledJob(jobId))
                    .ExecuteCommandAsync();
                await DeleteScheduledJobProjectionAsync(db, jobName);
                await dbWriter.BackfillQueryProjectionsV2Async(
                    batchSize: 32,
                    cancellationToken: CancellationToken.None,
                    staleOperationCutoffUtc: DateTime.UtcNow);
            }
        }

        [Fact]
        [Trait("scheduled job projection uniqueness", "ReferenceDb")]
        public async Task ScheduledJobNameProjection_EnforcesExactCaseSensitiveUniqueness()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var exactName = $"projection-case-{suffix}";
            var caseVariantName = exactName.ToUpperInvariant();
            var exact = SampleData.ScheduledJob with { JobName = exactName };
            var caseVariant = SampleData.ScheduledJob with { JobName = caseVariantName };

            try
            {
                await dbWriter.InsertScheduledJobAsync(exact);
                var exactJobId = await dbReader.GetScheduledJobIdAsync(exactName);
                exactJobId.Should().BeGreaterThan(0);

                await dbWriter.InsertScheduledJobAsync(caseVariant);
                var caseVariantJobId = await dbReader.GetScheduledJobIdAsync(caseVariantName);
                caseVariantJobId.Should().BeGreaterThan(0).And.NotBe(exactJobId);

                Func<Task> duplicateInsert = () => dbWriter.InsertScheduledJobAsync(
                    exact with { TaskName = $"duplicate-{suffix}" });
                await duplicateInsert.Should().ThrowAsync<StorageException>();

                (await dbReader.GetScheduledJobIdAsync(exactName)).Should().Be(exactJobId);
                (await dbReader.GetScheduledJobIdAsync(caseVariantName)).Should().Be(caseVariantJobId);
            }
            finally
            {
                await DeleteScheduledJobsNamedAsync(db, exactName, caseVariantName);
            }
        }

        [Fact]
        [Trait("scheduled job projection uniqueness", "ReferenceDb")]
        public async Task ScheduledJobNameProjection_RenameToOccupiedNameIsRejectedWithoutChangingEitherMapping()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var dbWriter = (IReferenceDbWriteContext)db;
            var suffix = Guid.NewGuid().ToString("N");
            var firstName = $"projection-first-{suffix}";
            var occupiedName = $"projection-occupied-{suffix}";
            var first = SampleData.ScheduledJob with { JobName = firstName };
            var occupied = SampleData.ScheduledJob with { JobName = occupiedName };

            try
            {
                await dbWriter.InsertScheduledJobAsync(first);
                await dbWriter.InsertScheduledJobAsync(occupied);
                var firstJobId = await dbReader.GetScheduledJobIdAsync(firstName);
                var occupiedJobId = await dbReader.GetScheduledJobIdAsync(occupiedName);
                firstJobId.Should().BeGreaterThan(0);
                occupiedJobId.Should().BeGreaterThan(0).And.NotBe(firstJobId);

                Func<Task> renameToOccupied = () => dbWriter.UpdateScheduledJobAsync(
                    first with { JobId = firstJobId, JobName = occupiedName });
                await renameToOccupied.Should().ThrowAsync<StorageException>();

                (await dbReader.GetScheduledJobIdAsync(firstName)).Should().Be(firstJobId);
                (await dbReader.GetScheduledJobIdAsync(occupiedName)).Should().Be(occupiedJobId);
                var jobs = await dbReader.GetScheduledJobsAsync();
                jobs.Should().Contain(job => job.JobId == firstJobId && job.JobName == firstName);
                jobs.Should().Contain(job => job.JobId == occupiedJobId && job.JobName == occupiedName);
            }
            finally
            {
                await DeleteScheduledJobsNamedAsync(db, firstName, occupiedName);
            }
        }

        [Fact]
        [Trait("seed id lwt", "ReferenceDb")]
        public async Task SeedIdV2_ConcurrentCallsReturnDistinctContiguousIds()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var dbReader = (IReferenceDbReadContext)db;
            var seedType = $"seed-lwt-{Guid.NewGuid():N}";
            await DeleteSeedIdV2Async(db, seedType);
            await db.Use(ReferenceDbCql.InsertSeedIdV2IfNotExists)
                .SetParameters(new InsertSeedIdV2IfNotExists(seedType, 1000))
                .ExecuteCommandAsync();

            try
            {
                var values = await Task.WhenAll(
                    Enumerable.Range(0, 32).Select(_ => dbReader.GetNextSeedIdAsync(seedType)));

                values.Should().OnlyHaveUniqueItems();
                values.Should().BeEquivalentTo(Enumerable.Range(1001, 32));
                (await dbReader.GetCurrentSeedIdAsync(seedType)).Should().Be(1032);
            }
            finally
            {
                await DeleteSeedIdV2Async(db, seedType);
            }
        }

        static Task InsertCanonicalEconomicCalendarAsync(IReferenceDbContext db, EconomicCalendarReadModel row)
            => db.Use(ReferenceDbCql.InsertEconomicCalendar)
                .SetParameters(new InsertEconomicCalendar(
                    row.EventDate,
                    row.CountryCode,
                    row.EventName,
                    row.Actual,
                    row.Forecast,
                    row.Prior,
                    row.CreatedOn,
                    row.CreatedBy))
                .ExecuteCommandAsync();

        static Task InsertEconomicCalendarProjectionAsync(
            IReferenceDbContext db,
            EconomicCalendarReadModel row,
            int monthBucket)
            => db.Use(ReferenceDbCql.InsertEconomicCalendarByCountryMonthV2)
                .SetParameters(new InsertEconomicCalendarByCountryMonthV2(
                    row.CountryCode,
                    monthBucket,
                    row.EventDate,
                    row.EventName,
                    row.Actual,
                    row.Forecast,
                    row.Prior,
                    row.CreatedOn,
                    row.CreatedBy))
                .ExecuteCommandAsync();

        static Task DeleteCanonicalEconomicCalendarAsync(IReferenceDbContext db, EconomicCalendarReadModel row)
            => db.Use(ReferenceDbCql.DeleteEconomicCalendar)
                .SetParameters(new DeleteEconomicCalendar(row.EventDate, row.CountryCode, row.EventName))
                .ExecuteCommandAsync();

        static Task DeleteEconomicCalendarProjectionAsync(
            IReferenceDbContext db,
            EconomicCalendarReadModel row,
            int monthBucket)
            => db.Use(ReferenceDbCql.DeleteEconomicCalendarByCountryMonthV2)
                .SetParameters(new DeleteEconomicCalendarByCountryMonthV2(
                    row.CountryCode,
                    monthBucket,
                    row.EventDate,
                    row.EventName))
                .ExecuteCommandAsync();

        static Task DeleteEconomicCalendarProjectionStateAsync(IReferenceDbContext db)
            => db.Use(ReferenceDbCql.DeleteReferenceProjectionStateV3)
                .SetParameters(new DeleteReferenceProjectionStateV3("economic_calendar_by_country_month_v2"))
                .ExecuteCommandAsync();

        static Task InvalidateEconomicCalendarProjectionStateAsync(IReferenceDbContext db)
            => db.Use(ReferenceDbCql.InvalidateReferenceProjectionStateV3)
                .SetParameters(new InvalidateReferenceProjectionStateV3(
                    Guid.NewGuid(),
                    "economic_calendar_by_country_month_v2"))
                .ExecuteCommandAsync();

        static Task DeleteScheduledJobProjectionAsync(IReferenceDbContext db, string jobName)
            => db.Use(ReferenceDbCql.DeleteScheduledJobByNameV3ForOfflineRepair)
                .SetParameters(new DeleteScheduledJobByNameV3ForOfflineRepair(jobName))
                .ExecuteCommandAsync();

        static async Task DeleteScheduledJobsNamedAsync(IReferenceDbContext db, params string[] jobNames)
        {
            var names = jobNames.ToHashSet(StringComparer.Ordinal);
            var jobs = await ((IReferenceDbReadContext)db).GetScheduledJobsAsync();
            foreach (var job in jobs.Where(job => names.Contains(job.JobName)))
                await ((IReferenceDbWriteContext)db).DeleteScheduledJobAsync(job.JobId);
            foreach (var jobName in names)
                await DeleteScheduledJobProjectionAsync(db, jobName);
        }

        static Task DeleteSeedIdV2Async(IReferenceDbContext db, string seedType)
            => db.Use("""
                DELETE FROM seed_id_v2
                WHERE SeedType = :seedType;
                """)
                .SetParameters(new GetNextSeedIdV2(seedType))
                .ExecuteCommandAsync();

    }
}
