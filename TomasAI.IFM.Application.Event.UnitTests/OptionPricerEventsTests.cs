using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketData.ViewModels;


namespace TomasAI.IFM.Application.Event.UnitTests
{
    public class OptionPricerEventsTests
    {
        [Fact]
        public async Task SpreadDistributionInsertedEventOk()
        {
            var spreadDistribution = default(SpreadDistributionReadModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IOptionPricerEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertSpreadDistributionAsync(It.IsAny<SpreadDistributionInsertedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<SpreadDistributionInsertedEvent>(e => spreadDistribution = e.SpreadDistribution);

            var opEvents = new OptionPricerEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var spreadDistributionInserted = new SpreadDistributionInsertedEvent
            {
                SpreadDistribution = new SpreadDistributionReadModel {
                    DaysToExpiry = 5,
                    ForwardPrice = 4.56,
                    LongVolatility = 0.00234,
                    ShortVolatility = 0.1056,
                    TradeId = 1234,
                    TradeStatus = TradeStatus.IntraDay,
                    TradeType = TradeType.IronCondor,
                    ValueDate = new DateTime(2019,10,12)
                },
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await opEvents.ExecuteAsync(spreadDistributionInserted);

            Assert.NotNull(spreadDistribution);
            Assert.Equal(1234, spreadDistribution.TradeId);
            Assert.Equal(TradeStatus.IntraDay, spreadDistribution.TradeStatus);
            Assert.Equal(TradeType.IronCondor, spreadDistribution.TradeType);
            Assert.Equal(2019, spreadDistribution.ValueDate.Year);
            Assert.Equal(10, spreadDistribution.ValueDate.Month);
            Assert.Equal(12, spreadDistribution.ValueDate.Day);

        }

        [Fact]
        public async Task SpreadDistributionInsertedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IOptionPricerEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertSpreadDistributionAsync(It.IsAny<SpreadDistributionInsertedEvent>()))
                .Throws<InvalidOperationException>();

            var opEvents = new OptionPricerEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var spreadDistributionInserted = new SpreadDistributionInsertedEvent
            {
                SpreadDistribution = new SpreadDistributionReadModel
                {
                    DaysToExpiry = 5,
                    ForwardPrice = 4.56,
                    LongVolatility = 0.00234,
                    ShortVolatility = 0.1056,
                    TradeId = 1234,
                    TradeStatus = TradeStatus.IntraDay,
                    TradeType = TradeType.IronCondor,
                    ValueDate = new DateTime(2019, 10, 12)
                },
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await opEvents.ExecuteAsync(spreadDistributionInserted);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(opEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task SpreadDistributionJobCreatedEventOk()
        {
            var spreadDistributionJob = default(SpreadDistributionJobReadModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IOptionPricerEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertSpreadDistributionJobAsync(It.IsAny<SpreadDistributionJobCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<SpreadDistributionJobCreatedEvent>(e => spreadDistributionJob = e.SpreadDistributionJob);

            var opEvents = new OptionPricerEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                SpreadDistributionJob = new SpreadDistributionJobReadModel
                {
                    JobId = 1234,
                    OrderId = 5678,
                    TradeId = 1234,
                    TradeType = TradeType.IronCondor,
                    TradeStatus = TradeStatus.IntraDay,
                    ValueDate = new DateTime(2019, 10, 12),
                    DaysToExpiry = 5,
                    OptionStyle = OptionStyle.American,
                    OptionType = OptionType.Call,
                    JobSubmitted = new DateTime(2019,10,12),
                    JobStatus = "Job Status",
                    JobCompleted = null,
                    JobFailed = null,
                    InProgress = true,
                    CallSpreadDistribution = null,
                    PutSpreadDistribution = null,
                    Duration = 0.44,
                    LossProbabilityFactor = 0.23
                }
            };
            await opEvents.ExecuteAsync(spreadDistributionJobCreated);

            Assert.NotNull(spreadDistributionJob);
            Assert.Equal(1234, spreadDistributionJob.JobId);
            Assert.Equal(5678, spreadDistributionJob.OrderId);
            Assert.Equal(1234, spreadDistributionJob.TradeId);
            Assert.Equal(TradeType.IronCondor, spreadDistributionJob.TradeType);
            Assert.Equal(TradeStatus.IntraDay, spreadDistributionJob.TradeStatus);
            Assert.Equal(2019, spreadDistributionJob.ValueDate.Year);
            Assert.Equal(10, spreadDistributionJob.ValueDate.Month);
            Assert.Equal(12, spreadDistributionJob.ValueDate.Day);
            Assert.Equal(5, spreadDistributionJob.DaysToExpiry);
            Assert.Equal(OptionStyle.American, spreadDistributionJob.OptionStyle);
            Assert.Equal(OptionType.Call, spreadDistributionJob.OptionType);
            Assert.Equal("Job Status", spreadDistributionJob.JobStatus);

        }

        [Fact]
        public async Task SpreadDistributionJobCreatedEventWithError()
        {
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IOptionPricerEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertSpreadDistributionJobAsync(It.IsAny<SpreadDistributionJobCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var opEvents = new OptionPricerEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var spreadDistributionJobCreated = new SpreadDistributionJobCreatedEvent
            {
                SpreadDistributionJob = new SpreadDistributionJobReadModel
                {
                    JobId = 1234,
                    OrderId = 5678,
                    TradeId = 1234,
                    TradeType = TradeType.IronCondor,
                    TradeStatus = TradeStatus.IntraDay,
                    ValueDate = new DateTime(2019, 10, 12),
                    DaysToExpiry = 5,
                    OptionStyle = OptionStyle.American,
                    OptionType = OptionType.Call,
                    JobSubmitted = new DateTime(2019, 10, 12),
                    JobStatus = "Job Status",
                    JobCompleted = null,
                    JobFailed = null,
                    InProgress = true,
                    CallSpreadDistribution = null,
                    PutSpreadDistribution = null,
                    Duration = 0.44,
                    LossProbabilityFactor = 0.23
                }
            };
            await opEvents.ExecuteAsync(spreadDistributionJobCreated);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(opEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task SpreadDistributionJobCompletedEventOk()
        {
            var jobCompleted = default(DateTime);
            var jobId = default(int);
            var jobStatus = default(string);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IOptionPricerEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateSpreadDistributionJobCompletedAsync(It.IsAny<SpreadDistributionJobCompletedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<SpreadDistributionJobCompletedEvent>(e => {
                    jobCompleted = e.JobCompleted;
                    jobId = e.JobId;
                    jobStatus = e.JobStatus;
                });

            var opEvents = new OptionPricerEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var spreadDistributionJobCompleted = new SpreadDistributionJobCompletedEvent
            {
               JobCompleted = new DateTime(2019,10,12),
               JobId = 1234,
               JobStatus = "Job Status"
            };
            await opEvents.ExecuteAsync(spreadDistributionJobCompleted);

            Assert.Equal(2019, jobCompleted.Year);
            Assert.Equal(10, jobCompleted.Month);
            Assert.Equal(12, jobCompleted.Day);
            Assert.Equal(1234, jobId);
            Assert.Equal("Job Status", jobStatus);
        }

    }
}
