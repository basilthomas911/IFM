using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Event.Denormalizer.UnitTests
{
    public class OptionPricerDatabaseFixture : IDisposable
    {
        private OptionPricerDbContext _db;

 
        public OptionPricerDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                .Add("OptionPricerDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=optionpricertestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, OptionPricerDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            DbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<OptionPricerDbContext>), new OptionPricerDbContext(dbConn, DbFactory));
            Db = DbFactory.OptionPricerDb as OptionPricerDbContext;
        }

        public OptionPricerDbContext Db { get; }
        public DbContextFactory DbFactory { get; }

        public void Dispose()
        {
        }
    }

    public class OptionPricerEventDenormalizerTests : IClassFixture<OptionPricerDatabaseFixture>
    {

        private OptionPricerDatabaseFixture _fixture;

        public OptionPricerEventDenormalizerTests(OptionPricerDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        /*
            IAsyncEventHandler<SpreadDistributionInsertedEvent>,
            IAsyncEventHandler<SpreadDistributionJobCreatedEvent>,
            IAsyncEventHandler<SpreadDistributionJobCompletedEvent>,
            IAsyncEventHandler<SpreadDistributionJobFailedEvent>
         */
        [Fact]
        public async Task ExecuteSpreadDistributionInsertedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution where TradeId = {TestDataDefaults.SpreadDistribution.TradeId}").ExecuteCommandAsync();
            var completedEvent = default(SpreadDistributionInsertedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionInsertedCompleteEvent>()))
                .Callback<SpreadDistributionInsertedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());

            var spreadDistributionInserted = new SpreadDistributionInsertedEvent
            {
                CommandId = Guid.NewGuid(),
                SpreadDistribution = TestDataDefaults.SpreadDistribution
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionInserted);

            // then...
            var spreadDistribution = await (_fixture.Db.Use($"select *  from spread_distribution where TradeId = {TestDataDefaults.SpreadDistribution.TradeId}")).ExecuteSingleAsync<SpreadDistributionReadModel>();

            spreadDistribution.Should().NotBeNull();
            spreadDistribution.TradeId.Should().Be(TestDataDefaults.SpreadDistribution.TradeId);
            spreadDistribution.TradeType.Should().Be(TestDataDefaults.SpreadDistribution.TradeType);
            spreadDistribution.TradeStatus.Should().Be(TestDataDefaults.SpreadDistribution.TradeStatus);
            spreadDistribution.ValueDate.Year.Should().Be(TestDataDefaults.SpreadDistribution.ValueDate.Year);
            spreadDistribution.ValueDate.Month.Should().Be(TestDataDefaults.SpreadDistribution.ValueDate.Month);
            spreadDistribution.ValueDate.Day.Should().Be(TestDataDefaults.SpreadDistribution.ValueDate.Day);
            spreadDistribution.DaysToExpiry.Should().Be(TestDataDefaults.SpreadDistribution.DaysToExpiry);
            spreadDistribution.ShortVolatility.ToString("F4").Should().Be(TestDataDefaults.SpreadDistribution.ShortVolatility.ToString("F4"));
            spreadDistribution.LongVolatility.ToString("F4").Should().Be(TestDataDefaults.SpreadDistribution.LongVolatility.ToString("F4"));
            spreadDistribution.LossProbability.ToString("F4").Should().Be(TestDataDefaults.SpreadDistribution.LossProbability.ToString("F4"));
            spreadDistribution.LossThreshold.Should().Be(TestDataDefaults.SpreadDistribution.LossThreshold);
            spreadDistribution.LossThresholdCount.Should().Be(TestDataDefaults.SpreadDistribution.LossThresholdCount);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(spreadDistributionInserted.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(spreadDistributionInserted.CommandId);
        }

        [Fact]
        public async Task ExecuteSpreadDistributionInsertedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution where TradeId = {TestDataDefaults.SpreadDistribution.TradeId}").ExecuteCommandAsync();
            var failedEvent = default(SpreadDistributionInsertedFailEvent);
            var denormalizerFailedEvent = default(DenormalizerExceptionEvent);
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionInsertedFailEvent>()))
                .Callback<SpreadDistributionInsertedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerFailedEvent = e);
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());

            var spreadDistributionInserted = new SpreadDistributionInsertedEvent
            {
                SpreadDistribution = TestDataDefaults.SpreadDistribution
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionInserted);

            // then...
            var spreadDistribution = await (_fixture.Db.Use($"select *  from spread_distribution where TradeId = {TestDataDefaults.SpreadDistribution.TradeId}")).ExecuteSingleAsync<SpreadDistributionReadModel>();
            spreadDistribution.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(spreadDistributionInserted.CommandId);
            denormalizerFailedEvent.Should().NotBeNull();
            denormalizerFailedEvent.CommandId.Should().Be(spreadDistributionInserted.CommandId);
        }

        [Fact]
        public async Task ExecuteSpreadDistributionJobCreatedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}").ExecuteCommandAsync();
            var completedEvent = default(SpreadDistributionJobCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobCreatedCompleteEvent>()))
                .Callback<SpreadDistributionJobCreatedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());
            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                SpreadDistributionJob = TestDataDefaults.SpreadDistributionJob
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionJobCreated);

            // then...
            var spreadDistributionJob = await (_fixture.Db.Use($"select *  from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}")).ExecuteSingleAsync<SpreadDistributionJobReadModel>();
            spreadDistributionJob.Should().NotBeNull();
            spreadDistributionJob.OrderId.Should().Be(TestDataDefaults.SpreadDistributionJob.OrderId);
            spreadDistributionJob.TradeId.Should().Be(TestDataDefaults.SpreadDistributionJob.TradeId);
            spreadDistributionJob.TradeType.Should().Be(TestDataDefaults.SpreadDistributionJob.TradeType);
            spreadDistributionJob.TradeStatus.Should().Be(TestDataDefaults.SpreadDistributionJob.TradeStatus);
            spreadDistributionJob.ValueDate.Year.Should().Be(TestDataDefaults.SpreadDistributionJob.ValueDate.Year);
            spreadDistributionJob.ValueDate.Month.Should().Be(TestDataDefaults.SpreadDistributionJob.ValueDate.Month);
            spreadDistributionJob.ValueDate.Day.Should().Be(TestDataDefaults.SpreadDistributionJob.ValueDate.Day);
            spreadDistributionJob.DaysToExpiry.Should().Be(TestDataDefaults.SpreadDistributionJob.DaysToExpiry);
            spreadDistributionJob.OptionStyle.Should().Be(TestDataDefaults.SpreadDistributionJob.OptionStyle);
            spreadDistributionJob.OptionType.Should().Be(TestDataDefaults.SpreadDistributionJob.OptionType);
            spreadDistributionJob.LossProbabilityFactor.ToString("F4").Should().Be(TestDataDefaults.SpreadDistributionJob.LossProbabilityFactor.ToString("F4"));
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(spreadDistributionJobCreated.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(spreadDistributionJobCreated.CommandId);
        }

        [Fact]
        public async Task ExecuteSpreadDistributionJobCreatedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}").ExecuteCommandAsync();
            var failedEvent = default(SpreadDistributionJobCreatedFailEvent);
            var denormalizerFailedEvent = default(DenormalizerExceptionEvent);
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobCreatedFailEvent>()))
                .Callback<SpreadDistributionJobCreatedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerFailedEvent = e);
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());
            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                SpreadDistributionJob = TestDataDefaults.SpreadDistributionJob
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionJobCreated);

            // then...
            var spreadDistributionJob = await (_fixture.Db.Use($"select *  from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}")).ExecuteSingleAsync<SpreadDistributionJobReadModel>();
            spreadDistributionJob.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(spreadDistributionJobCreated.CommandId);
            denormalizerFailedEvent.Should().NotBeNull();
            denormalizerFailedEvent.CommandId.Should().Be(spreadDistributionJobCreated.CommandId);
        }

        [Fact]
        public async Task ExecuteSpreadDistributionJobSucceededEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobCreatedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());

            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                SpreadDistributionJob = TestDataDefaults.SpreadDistributionJob
            };
            await denormalizer.ExecuteAsync(spreadDistributionJobCreated);

            var completedEvent = default(SpreadDistributionJobSucceededCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobSucceededCompleteEvent>()))
                .Callback<SpreadDistributionJobSucceededCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());
            var spreadDistributionJobSucceeded = new SpreadDistributionJobSucceededEvent
            {
                CommandId = Guid.NewGuid(),
                JobId = TestDataDefaults.SpreadDistributionJob.JobId,
                JobStatus = TestDataDefaults.SpreadDistributionJob.JobStatus,
                JobCompleted = TestDataDefaults.SpreadDistributionJob.JobCompleted.Value
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionJobSucceeded);

            // then...
            var spreadDistributionJob = await (_fixture.Db.Use($"select *  from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}")).ExecuteSingleAsync<SpreadDistributionJobReadModel>();
            spreadDistributionJob.Should().NotBeNull();
            spreadDistributionJob.JobStatus.Should().Be(TestDataDefaults.SpreadDistributionJob.JobStatus);
            spreadDistributionJob.JobCompleted.Value.Year.Should().Be(TestDataDefaults.SpreadDistributionJob.JobCompleted.Value.Year);
            spreadDistributionJob.JobCompleted.Value.Month.Should().Be(TestDataDefaults.SpreadDistributionJob.JobCompleted.Value.Month);
            spreadDistributionJob.JobCompleted.Value.Day.Should().Be(TestDataDefaults.SpreadDistributionJob.JobCompleted.Value.Day);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(spreadDistributionJobSucceeded.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(spreadDistributionJobSucceeded.CommandId);
        }

        [Fact]
        public async Task ExecuteSpreadDistributionJobSucceededEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobCreatedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());

            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                SpreadDistributionJob = TestDataDefaults.SpreadDistributionJob
            };
            await denormalizer.ExecuteAsync(spreadDistributionJobCreated);

            var failedEvent = default(SpreadDistributionJobSucceededFailEvent);
            var denormalizerFailedEvent = default(DenormalizerExceptionEvent);
            mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobSucceededFailEvent>()))
                .Callback<SpreadDistributionJobSucceededFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerFailedEvent = e);
            denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());
            var spreadDistributionJobSucceeded = new SpreadDistributionJobSucceededEvent
            {
                JobId = TestDataDefaults.SpreadDistributionJob.JobId,
                JobStatus = TestDataDefaults.SpreadDistributionJob.JobStatus,
                JobCompleted = TestDataDefaults.SpreadDistributionJob.JobCompleted.Value
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionJobSucceeded);

            // then...
            var spreadDistributionJob = await (_fixture.Db.Use($"select *  from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}")).ExecuteSingleAsync<SpreadDistributionJobReadModel>();
            spreadDistributionJob.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(spreadDistributionJobSucceeded.CommandId);
            denormalizerFailedEvent.Should().NotBeNull();
            denormalizerFailedEvent.CommandId.Should().Be(spreadDistributionJobSucceeded.CommandId);

        }

        [Fact]
        public async Task ExecuteSpreadDistributionJobFaultedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobCreatedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());

            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                SpreadDistributionJob = TestDataDefaults.SpreadDistributionJob
            };
            await denormalizer.ExecuteAsync(spreadDistributionJobCreated);

            var completedEvent = default(SpreadDistributionJobFaultedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobFaultedCompleteEvent>()))
                .Callback<SpreadDistributionJobFaultedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());
            var spreadDistributionJobFaulted = new SpreadDistributionJobFaultedEvent
            {
                CommandId = Guid.NewGuid(),
                JobId = TestDataDefaults.SpreadDistributionJob.JobId,
                JobStatus = TestDataDefaults.SpreadDistributionJob.JobStatus,
                JobFailed = TestDataDefaults.SpreadDistributionJob.JobFailed.Value
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionJobFaulted);

            // then...
            var spreadDistributionJob = await (_fixture.Db.Use($"select *  from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}")).ExecuteSingleAsync<SpreadDistributionJobReadModel>();

            spreadDistributionJob.Should().NotBeNull();
            spreadDistributionJob.JobStatus.Should().Be(TestDataDefaults.SpreadDistributionJob.JobStatus);
            spreadDistributionJob.JobFailed.Value.Year.Should().Be(TestDataDefaults.SpreadDistributionJob.JobFailed.Value.Year);
            spreadDistributionJob.JobFailed.Value.Month.Should().Be(TestDataDefaults.SpreadDistributionJob.JobFailed.Value.Month);
            spreadDistributionJob.JobFailed.Value.Day.Should().Be(TestDataDefaults.SpreadDistributionJob.JobFailed.Value.Day);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(spreadDistributionJobFaulted.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(spreadDistributionJobFaulted.CommandId);
        }

        [Fact]
        public async Task ExecuteSpreadDistributionJobFaultedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobCreatedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());

            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                SpreadDistributionJob = TestDataDefaults.SpreadDistributionJob
            };
            await denormalizer.ExecuteAsync(spreadDistributionJobCreated);

            var failedEvent = default(SpreadDistributionJobFaultedFailEvent);
            var denormalizerFailedEvent = default(DenormalizerExceptionEvent);
            mockOptionPricerEventProducer = new Mock<IOptionPricerEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<SpreadDistributionJobFaultedFailEvent>()))
                .Callback<SpreadDistributionJobFaultedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerFailedEvent = e);
            denormalizer = new OptionPricerEventDenormalizer(_fixture.DbFactory, mockOptionPricerEventProducer.Object, GetLogger<OptionPricerEventDenormalizer>());
            var spreadDistributionJobFaulted = new SpreadDistributionJobFaultedEvent
            {
                JobId = TestDataDefaults.SpreadDistributionJob.JobId,
                JobStatus = TestDataDefaults.SpreadDistributionJob.JobStatus,
                JobFailed = TestDataDefaults.SpreadDistributionJob.JobFailed.Value
            };

            // when...
            await denormalizer.ExecuteAsync(spreadDistributionJobFaulted);

            // then...
            var spreadDistributionJob = await (_fixture.Db.Use($"select *  from spread_distribution_job where JobId = {TestDataDefaults.SpreadDistributionJob.JobId}")).ExecuteSingleAsync<SpreadDistributionJobReadModel>();
            spreadDistributionJob.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(spreadDistributionJobFaulted.CommandId);
            denormalizerFailedEvent.Should().NotBeNull();
            denormalizerFailedEvent.CommandId.Should().Be(spreadDistributionJobFaulted.CommandId);
        }

        private ILogger<TLogger> GetLogger<TLogger>()
        {
            var mockLogger = new Mock<ILogger<TLogger>>();
            mockLogger
                .Setup(e => e.LogInformation(It.IsAny<string>()));
            mockLogger
                .Setup(e => e.LogError(It.IsAny<Exception>(), It.IsAny<string>()));
            return mockLogger.Object;
        }
    }
}
