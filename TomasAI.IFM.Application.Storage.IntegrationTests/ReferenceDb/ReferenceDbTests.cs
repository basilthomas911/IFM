using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
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
using TomasAI.IFM.Framework.SequenceId;

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
            var nextSequenceId = 0L;
            var sequenceIdGenerator = Substitute.For<ISequenceIdGenerator>();
            sequenceIdGenerator
                .GetSequenceIdAsync(Arg.Any<SequenceName>(), Arg.Any<CancellationToken>())
                .Returns(_ => new ValueTask<long>(Interlocked.Increment(ref nextSequenceId)));
            sequenceIdGenerator
                .GetHighWatermarkAsync(Arg.Any<SequenceName>(), Arg.Any<CancellationToken>())
                .Returns(_ => new ValueTask<long>(Volatile.Read(ref nextSequenceId)));
            diContainer.Add(
                typeof(IObjectRepository<ReferenceDbContext>),
                new ReferenceDbContext(dbConn, DbFactory, sequenceIdGenerator, logger));
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

    }
}
