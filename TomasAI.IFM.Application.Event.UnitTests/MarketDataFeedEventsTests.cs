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
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Event.UnitTests
{
    public class MarketDataFeedEventsTests
    {
        [Fact]
        public async Task FuturesEodDataInsertedEventOk()
        {
            var futuresContract = default(FuturesContractViewModel);
            var futuresTickData = default(FuturesTickDataViewModel[]);
            var createdOn = default(DateTime);
            var createdBy = default(string);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.FuturesEodDataUpdatedAsync(It.IsAny<FuturesEodDataUpdatedEvent>()))
                .ReturnsAsync(new ServiceOk());

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesEodDataInsertedEvent>()))
                .Returns(Task.CompletedTask);

            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesTickDataInsertedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesTickDataInsertedEvent>(e => {
                    futuresContract = e.Contract;
                    futuresTickData = e.TickData;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesEodDataInsertedEvent>()))
                .Returns(Task.CompletedTask);

            var futuresEodDataParam = new FuturesEodDataParametersReadModel {
                FuturesEodDataToday = TestDataDefaults.FuturesEodDataValues[0],
                FuturesEodDataRange = TestDataDefaults.FuturesEodDataValues,
                NormalCurveTable = new NormalCurveTableReadModel { NormalCurveTable = new NormalCurveDataReadModel[]
                {
                    new NormalCurveDataReadModel
                    (
                        index: 1,
                        percent: 0.45
                    )
                } }
            };
            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            mockMarketDataFeedQueryApi
                .Setup(e => e.GetFuturesEodDataParametersAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new ServiceOk<FuturesEodDataParametersReadModel>(futuresEodDataParam));
            mockMarketDataFeedQueryApi
                .Setup(e => e.GetFuturesEodDataAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new ServiceOk<FuturesEodDataViewModel>(TestDataDefaults.FuturesEodDataValues[0]));

            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            mockMarketDataFeedCommandApi
              .Setup(e => e.InsertFuturesEodDataAsync(It.IsAny<DateTime>(), It.IsAny<FuturesTickDataViewModel>(), It.IsAny<FuturesContractViewModel>(), It.IsAny<FuturesEodDataViewModel>(), It.IsAny<ICollection<FuturesEodDataViewModel>>(), It.IsAny<NormalCurveTableReadModel>(), It.IsAny<int>()))
              .ReturnsAsync(new ServiceOk());

            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var futuresTickDataInserted = new FuturesTickDataInsertedEvent
            {
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesTickData,
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019,10,10)
            };
            await mdfEvents.ExecuteAsync(futuresTickDataInserted);
            Assert.NotNull(futuresContract);
            Assert.NotNull(futuresTickData);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(10, createdOn.Month);
            Assert.Equal(10, createdOn.Day);
        }

        [Fact]
        public async Task FuturesEodDataInsertedEventWithError()
        {
            var futuresEodData = default(FuturesEodDataViewModel);
            var updatedOn = default(DateTime);
            var updatedBy = default(string);
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.FuturesEodDataUpdatedAsync(It.IsAny<FuturesEodDataUpdatedEvent>()))
                .ReturnsAsync(new ServiceOk())
                .Callback<FuturesEodDataUpdatedEvent>(e => {
                    futuresEodData = e.FuturesEodData;
                    updatedBy = e.UpdatedBy;
                    updatedOn = e.UpdatedOn;
                });

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesEodDataInsertedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesEodDataInsertedEvent>(e => {
                    futuresEodData = e.FuturesEodData;
                    updatedBy = e.CreatedBy;
                    updatedOn = e.CreatedOn;
                });

            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesTickDataInsertedEvent>()))
                .Throws<InvalidOperationException>();

            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesEodDataInsertedEvent>()))
                .Returns(Task.CompletedTask);

            var futuresEodDataParam = new FuturesEodDataParametersReadModel
            {
                FuturesEodDataToday = TestDataDefaults.FuturesEodDataValues[0],
                FuturesEodDataRange = TestDataDefaults.FuturesEodDataValues,
                NormalCurveTable = new NormalCurveTableReadModel
                {
                    NormalCurveTable = new NormalCurveDataReadModel[]
                {
                    new NormalCurveDataReadModel
                    (
                        index: 1,
                        percent: 0.45
                    )
                }
                }
            };
            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            mockMarketDataFeedQueryApi
                .Setup(e => e.GetFuturesEodDataParametersAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new ServiceOk<FuturesEodDataParametersReadModel>(futuresEodDataParam));
            mockMarketDataFeedQueryApi
                .Setup(e => e.GetFuturesEodDataAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new ServiceOk<FuturesEodDataViewModel>(TestDataDefaults.FuturesEodDataValues[0]));

            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            mockMarketDataFeedCommandApi
              .Setup(e => e.InsertFuturesEodDataAsync(It.IsAny<DateTime>(), It.IsAny<FuturesTickDataViewModel>(), It.IsAny<FuturesContractViewModel>(), It.IsAny<FuturesEodDataViewModel>(), It.IsAny<ICollection<FuturesEodDataViewModel>>(), It.IsAny<NormalCurveTableReadModel>(), It.IsAny<int>()))
              .ReturnsAsync(new ServiceOk());

            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var futuresTickDataInserted = new FuturesTickDataInsertedEvent
            {
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesTickData,
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 10, 10)
            };
            await mdfEvents.ExecuteAsync(futuresTickDataInserted);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);


        }

        [Fact]
        public async Task FuturesOptionTickDataInsertedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var futuresContract = default(FuturesContractViewModel);
            var tickData = default(FuturesOptionTickDataViewModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
  
            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesOptionTickDataInsertedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesOptionTickDataInsertedEvent>(e => {
                    futuresContract = e.Contract;
                    tickData = e.TickData;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var futuresOptionTickDataInserted = new FuturesOptionTickDataInsertedEvent
            {
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesOptionTickData,
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 10, 12)
            };
            await mdfEvents.ExecuteAsync(futuresOptionTickDataInserted);
            Assert.NotNull(futuresContract);
            Assert.Equal(futuresContract.ContractId, TestDataDefaults.FuturesContract.ContractId);
            Assert.NotNull(tickData);
            Assert.Equal(tickData.ContractId, TestDataDefaults.FuturesOptionTickData.ContractId);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(10, createdOn.Month);
            Assert.Equal(12, createdOn.Day);
        }

        [Fact]
        public async Task FuturesOptionTickDataInsertedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.ExecuteAsync(It.IsAny<FuturesOptionTickDataInsertedEvent>()))
                .Throws<InvalidOperationException>();

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var futuresOptionTickDataInserted = new FuturesOptionTickDataInsertedEvent
            {
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesOptionTickData,
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 10, 12)
            };
            await mdfEvents.ExecuteAsync(futuresOptionTickDataInserted);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task MarketDataFeedStartedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var startedBy = default(string);
            var startedOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.MarketDataFeedStartedAsync(It.IsAny<MarketDataFeedStartedEvent>()))
                .ReturnsAsync(new ServiceOk())
                .Callback<MarketDataFeedStartedEvent>(e => {
                    startedBy = e.StartedBy;
                    startedOn = e.StartedOn;
                });

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);
  
            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var marketDataFeedStarted = new MarketDataFeedStartedEvent
            {
                StartedBy = "basilt",
                StartedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(marketDataFeedStarted);
            Assert.Equal("basilt", startedBy);
            Assert.Equal(2019, startedOn.Year);
            Assert.Equal(10, startedOn.Month);
            Assert.Equal(12, startedOn.Day);
        }

        [Fact]
        public async Task MarketDataFeedStartedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.MarketDataFeedStartedAsync(It.IsAny<MarketDataFeedStartedEvent>()))
                .Throws<InvalidOperationException>();

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var marketDataFeedStarted = new MarketDataFeedStartedEvent
            {
                StartedBy = "basilt",
                StartedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(marketDataFeedStarted);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task MarketDataFeedStoppedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var stoppedBy = default(string);
            var stoppedOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.MarketDataFeedStoppedAsync(It.IsAny<MarketDataFeedStoppedEvent>()))
                .ReturnsAsync(new ServiceOk())
                .Callback<MarketDataFeedStoppedEvent>(e => {
                    stoppedBy = e.StoppedBy;
                    stoppedOn = e.StoppedOn;
                });

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var marketDataFeedStopped = new MarketDataFeedStoppedEvent
            {
                StoppedBy = "basilt",
                StoppedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(marketDataFeedStopped);
            Assert.Equal("basilt", stoppedBy);
            Assert.Equal(2019, stoppedOn.Year);
            Assert.Equal(10, stoppedOn.Month);
            Assert.Equal(12, stoppedOn.Day);
        }

        [Fact]
        public async Task MarketDataFeedStoppedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.MarketDataFeedStoppedAsync(It.IsAny<MarketDataFeedStoppedEvent>()))
                .Throws<InvalidOperationException>();

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var marketDataFeedStopped = new MarketDataFeedStoppedEvent
            {
                StoppedBy = "basilt",
                StoppedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(marketDataFeedStopped);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task MarketDataFeedResetEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var resetBy = default(string);
            var resetOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.MarketDataFeedResetAsync(It.IsAny<MarketDataFeedResetEvent>()))
                .ReturnsAsync(new ServiceOk())
                .Callback<MarketDataFeedResetEvent>(e => {
                    resetBy = e.ResetBy;
                    resetOn = e.ResetOn;
                });

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var marketDataFeedReset = new MarketDataFeedResetEvent
            {
                ResetBy = "basilt",
                ResetOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(marketDataFeedReset);
            Assert.Equal("basilt", resetBy);
            Assert.Equal(2019, resetOn.Year);
            Assert.Equal(10, resetOn.Month);
            Assert.Equal(12, resetOn.Day);
        }

        [Fact]
        public async Task MarketDataFeedResetEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);
            marketDataFeedServiceApi
                .Setup(e => e.MarketDataFeedResetAsync(It.IsAny<MarketDataFeedResetEvent>()))
                .Throws<InvalidOperationException>();

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var marketDataFeedReset = new MarketDataFeedResetEvent
            {
                ResetBy = "basilt",
                ResetOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(marketDataFeedReset);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task TradeLiveFeedAddedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            int orderId = default(int);
            int tradeId = default(int);
            var addedBy = default(string);
            var addedOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.AddTradeLiveFeedAsync(It.IsAny<TradeLiveFeedAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeLiveFeedAddedEvent>(e => {
                    orderId = e.OrderId;
                    tradeId = e.TradeId;
                    addedBy = e.AddedBy;
                    addedOn = e.AddedOn;
                });

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedAdded = new TradeLiveFeedAddedEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                AddedBy = "basilt",
                AddedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedAdded);
            Assert.Equal(2002, orderId);
            Assert.Equal(3003, tradeId);
            Assert.Equal("basilt", addedBy);
            Assert.Equal(2019, addedOn.Year);
            Assert.Equal(10, addedOn.Month);
            Assert.Equal(12, addedOn.Day);
        }

        [Fact]
        public async Task TradeLiveFeedAddedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.AddTradeLiveFeedAsync(It.IsAny<TradeLiveFeedAddedEvent>()))
                .Throws<InvalidOperationException>();

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedAdded = new TradeLiveFeedAddedEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                AddedBy = "basilt",
                AddedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedAdded);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task TradeLiveFeedRemovedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            int orderId = default(int);
            int tradeId = default(int);
            var removedBy = default(string);
            var removedOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.RemoveTradeLiveFeedAsync(It.IsAny<TradeLiveFeedRemovedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeLiveFeedRemovedEvent>(e => {
                    orderId = e.OrderId;
                    tradeId = e.TradeId;
                    removedBy = e.RemovedBy;
                    removedOn = e.RemovedOn;
                });

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedRemoved = new TradeLiveFeedRemovedEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                RemovedBy = "basilt",
                RemovedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedRemoved);
            Assert.Equal(2002, orderId);
            Assert.Equal(3003, tradeId);
            Assert.Equal("basilt", removedBy);
            Assert.Equal(2019, removedOn.Year);
            Assert.Equal(10, removedOn.Month);
            Assert.Equal(12, removedOn.Day);
        }

        [Fact]
        public async Task TradeLiveFeedRemovedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.RemoveTradeLiveFeedAsync(It.IsAny<TradeLiveFeedRemovedEvent>()))
                .Throws<InvalidOperationException>();

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedRemoved = new TradeLiveFeedRemovedEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                RemovedBy = "basilt",
                RemovedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedRemoved);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task TradeLiveFeedTurnedOnEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            int orderId = default(int);
            int tradeId = default(int);
            var updatedBy = default(string);
            var updatedOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.TurnTradeLiveFeedOnAsync(It.IsAny<TradeLiveFeedTurnedOnEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeLiveFeedTurnedOnEvent>(e => {
                    orderId = e.OrderId;
                    tradeId = e.TradeId;
                    updatedBy = e.UpdatedBy;
                    updatedOn = e.UpdatedOn;
                });

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedRemoved = new TradeLiveFeedTurnedOnEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedRemoved);
            Assert.Equal(2002, orderId);
            Assert.Equal(3003, tradeId);
            Assert.Equal("basilt", updatedBy);
            Assert.Equal(2019, updatedOn.Year);
            Assert.Equal(10, updatedOn.Month);
            Assert.Equal(12, updatedOn.Day);
        }

        [Fact]
        public async Task TradeLiveFeedTurnedOnEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.TurnTradeLiveFeedOnAsync(It.IsAny<TradeLiveFeedTurnedOnEvent>()))
                .Throws<InvalidOperationException>();

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedRemoved = new TradeLiveFeedTurnedOnEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedRemoved);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task TradeLiveFeedTurnedOffEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            int orderId = default(int);
            int tradeId = default(int);
            var updatedBy = default(string);
            var updatedOn = default(DateTime);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.TurnTradeLiveFeedOffAsync(It.IsAny<TradeLiveFeedTurnedOffEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeLiveFeedTurnedOffEvent>(e => {
                    orderId = e.OrderId;
                    tradeId = e.TradeId;
                    updatedBy = e.UpdatedBy;
                    updatedOn = e.UpdatedOn;
                });

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedTurnedOff = new TradeLiveFeedTurnedOffEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedTurnedOff);
            Assert.Equal(2002, orderId);
            Assert.Equal(3003, tradeId);
            Assert.Equal("basilt", updatedBy);
            Assert.Equal(2019, updatedOn.Year);
            Assert.Equal(10, updatedOn.Month);
            Assert.Equal(12, updatedOn.Day);
        }

        [Fact]
        public async Task TradeLiveFeedTurnedOffEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var marketDataFeedServiceApi = new Mock<IMarketDataFeedServiceApi>(MockBehavior.Strict);

            var mockEventDenormalizer = new Mock<IMarketDataFeedEventDenormalizerApi>(MockBehavior.Strict);

            var mockMarketDataFeedQueryApi = new Mock<IMarketDataFeedQueryApi>(MockBehavior.Strict);
            var mockMarketDataFeedCommandApi = new Mock<IMarketDataFeedCommandApi>(MockBehavior.Strict);
            var mockTradeDenormalizer = new Mock<ITradeEventDenormalizerApi>(MockBehavior.Strict);
            mockTradeDenormalizer
                .Setup(e => e.TurnTradeLiveFeedOffAsync(It.IsAny<TradeLiveFeedTurnedOffEvent>()))
                .Throws<InvalidOperationException>();

            var mdfEvents = new MarketDataFeedEvents(marketDataFeedServiceApi.Object, mockEventDenormalizer.Object, mockMarketDataFeedQueryApi.Object, mockMarketDataFeedCommandApi.Object, mockTradeDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeLiveFeedTurnedOff = new TradeLiveFeedTurnedOffEvent
            {
                OrderId = 2002,
                TradeId = 3003,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2019, 10, 12)
            };

            await mdfEvents.ExecuteAsync(tradeLiveFeedTurnedOff);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdfEvents.ErrorCode, logVM.StatusCode);

        }
    }
}
